using Microsoft.EntityFrameworkCore;
using Otrade.Application.Common;
using Otrade.Application.DTOs.Admin;
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
                AdminPermission.ViewReports or
                AdminPermission.ManageBonus,

            AdminRole.Support => permission is
                AdminPermission.ManageKyc or
                AdminPermission.ManageTickets or
                AdminPermission.ViewReports,

            AdminRole.Backend => permission is
                AdminPermission.ManageHangfire or
                AdminPermission.ManageSettings or
                AdminPermission.ViewReports,
            _ => false
        };

        if (!allowed)
            return ResponseFactory.Fail<bool>("You do not have permission for this action");

        return ResponseFactory.Success(true);
    }
    public async Task<ApiResponse<AdminAccessDto>> GetMyAccessAsync(long userId)
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
            return ResponseFactory.Fail<AdminAccessDto>("User not found");

        if (!user.IsAdmin && !user.IsOwner)
            return ResponseFactory.Fail<AdminAccessDto>("Admin access required");

        bool Has(AdminPermission permission)
        {
            if (user.IsOwner)
                return true;

            if (user.AdminRole == AdminRole.SuperAdmin)
                return true;

            return user.AdminRole switch
            {
                AdminRole.Finance => permission is
                    AdminPermission.ManageDeposits or
                    AdminPermission.ManageWithdrawals or
                    AdminPermission.ViewReports or
                    AdminPermission.ManageBonus,

                AdminRole.Support => permission is
                    AdminPermission.ManageKyc or
                    AdminPermission.ManageTickets or
                    AdminPermission.ViewReports,

                AdminRole.Backend=>permission is
                    AdminPermission.ManageHangfire or
                    AdminPermission.ManageSettings or
                    AdminPermission.ViewReports,
                _ => false
            };
        }

        var dto = new AdminAccessDto
        {
            IsAdmin = user.IsAdmin,
            IsOwner = user.IsOwner,
            AdminRole = user.IsOwner
                ? "Owner"
                : user.AdminRole?.ToString() ?? "User",

            ManageUsers = Has(AdminPermission.ManageUsers),
            ManageSettings = Has(AdminPermission.ManageSettings),
            ManageDeposits = Has(AdminPermission.ManageDeposits),
            ManageWithdrawals = Has(AdminPermission.ManageWithdrawals),
            ManageKyc = Has(AdminPermission.ManageKyc),
            ViewReports = Has(AdminPermission.ViewReports),
            ManageRanks = Has(AdminPermission.ManageRanks),
            ManageTickets = Has(AdminPermission.ManageTickets),
            ManageHangfire = Has(AdminPermission.ManageHangfire),
            ManageAdminRoles = Has(AdminPermission.ManageAdminRoles),
            ManageBonus = Has(AdminPermission.ManageBonus)
        };

        return ResponseFactory.Success(dto);
    }
}