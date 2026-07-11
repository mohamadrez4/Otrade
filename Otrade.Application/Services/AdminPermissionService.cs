using Microsoft.EntityFrameworkCore;
using Otrade.Application.Common;
using Otrade.Domain.Enums;
using Otrade.Persistence.Context;

namespace Otrade.Application.Services;

public class AdminPermissionService
{
    private readonly OtradeDbContext _context;

    public AdminPermissionService(OtradeDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<bool>> EnsurePermissionAsync(
        long userId,
        AdminPermission permission)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new
            {
                x.UserId,
                x.IsAdmin,
                x.IsOwner,
                x.AdminRole
            })
            .FirstOrDefaultAsync();

        if (user == null)
            return ResponseFactory.Fail<bool>("User not found");

        if (!user.IsAdmin && !user.IsOwner)
            return ResponseFactory.Fail<bool>("Admin access required");

        if (user.IsOwner)
            return ResponseFactory.Success(true);

        if (user.AdminRole == AdminRole.SuperAdmin)
            return ResponseFactory.Success(true);

        var allowed = user.AdminRole switch
        {
            AdminRole.Finance => permission is
                AdminPermission.ManageDeposits or
                AdminPermission.ManageWithdrawals or
                AdminPermission.ViewReports,

            AdminRole.Support => permission is
                AdminPermission.ManageKyc or
                AdminPermission.ManageTickets or
                AdminPermission.ManageUsers,

            AdminRole.Backend => permission is
                AdminPermission.ManageHangfire or
                AdminPermission.ManageSettings ,
            _ => false
        };

        if (!allowed)
            return ResponseFactory.Fail<bool>("You do not have permission for this action");

        return ResponseFactory.Success(true);
    }
}