using Microsoft.EntityFrameworkCore;
using Otrade.Persistence.Context;
using System.Text.RegularExpressions;

namespace Otrade.Application.Services;

public class PaymentTransactionGuardService
{
    private readonly OtradeDbContext _context;

    private static readonly Regex TxHashRegex = new(
        @"^[A-Za-z0-9]{10,200}$",
        RegexOptions.Compiled);

    public PaymentTransactionGuardService(OtradeDbContext context)
    {
        _context = context;
    }

    public string NormalizeTxId(string? txId)
    {
        return txId?.Trim() ?? string.Empty;
    }

    public string? ValidateTxId(string txId)
    {
        if (string.IsNullOrWhiteSpace(txId))
            return "Transaction hash is required";

        if (txId.Length < 10)
            return "Transaction hash is too short";

        if (txId.Length > 200)
            return "Transaction hash is too long";

        if (txId.Any(char.IsWhiteSpace))
            return "Transaction hash cannot contain spaces";

        if (!TxHashRegex.IsMatch(txId))
            return "Transaction hash format is invalid";

        return null;
    }

    public async Task<bool> IsTxIdUsedAsync(
        string txId,
        long? excludeTemporaryRegistrationId = null)
    {
        var normalizedTxId = NormalizeTxId(txId);

        if (string.IsNullOrWhiteSpace(normalizedTxId))
            return false;

        var normalizedLower = normalizedTxId.ToLower();

        var usedInDeposits = await _context.Deposits
            .AsNoTracking()
            .AnyAsync(x =>
                x.TxId != null &&
                x.TxId.ToLower() == normalizedLower);

        if (usedInDeposits)
            return true;

        var temporaryQuery = _context.TemporaryRegistrations
            .AsNoTracking()
            .Where(x =>
                x.DepositTxId != null &&
                x.DepositTxId.ToLower() == normalizedLower);

        if (excludeTemporaryRegistrationId.HasValue)
        {
            temporaryQuery = temporaryQuery
                .Where(x => x.Id != excludeTemporaryRegistrationId.Value);
        }

        return await temporaryQuery.AnyAsync();
    }
}