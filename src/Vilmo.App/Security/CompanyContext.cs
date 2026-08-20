using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Vilmo.Data;
using Vilmo.Domain;

namespace Vilmo.Security;

public sealed class CompanyContext
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = "";
    public string Name { get; init; } = "";
    public bool IsAdmin { get; init; }
    public string Level { get; init; } = "";
    public Guid? CompanyId { get; init; }
    public string Profile { get; init; } = "";
    public bool IsVendor => Profile == UserProfiles.Vendor && !IsAdmin;

    public static async Task<CompanyContext?> ResolveAsync(HttpContext http, AppDbContext db, CancellationToken ct)
    {
        var userIdRaw = http.User.FindFirstValue("user_id");
        if (!Guid.TryParse(userIdRaw, out var userId)) return null;
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || user.Status == UserStatuses.Disabled) return null;

        var memberships = await db.UserCompanies.AsNoTracking().Where(x => x.UserId == user.Id).ToListAsync(ct);
        Guid? companyId = null;
        var header = http.Request.Headers["X-Company-Id"].FirstOrDefault();
        if (Guid.TryParse(header, out var fromHeader)) companyId = fromHeader;
        else if (Guid.TryParse(http.User.FindFirstValue("company_id"), out var fromClaim)) companyId = fromClaim;
        else if (!user.IsPlatformSuperUser && memberships.Count == 1) companyId = memberships[0].CompanyId;

        string profile = "";
        if (companyId is { } cid)
        {
            var mem = memberships.FirstOrDefault(m => m.CompanyId == cid);
            if (!user.IsPlatformSuperUser)
            {
                if (mem is null) return Forbidden();
                profile = mem.Profile;
            }
            else profile = mem?.Profile ?? UserProfiles.CompanyAdmin;
        }
        else if (!user.IsPlatformSuperUser && memberships.Count > 1)
        {
            http.Items["company_required"] = true;
        }

        var level = user.IsPlatformSuperUser
            ? LoginLevels.Admin
            : memberships.All(m => m.Profile == UserProfiles.Vendor)
                ? LoginLevels.Vendor
                : LoginLevels.Company;

        return new CompanyContext
        {
            UserId = user.Id,
            Email = user.Email,
            Name = user.Name,
            IsAdmin = user.IsPlatformSuperUser,
            Level = level,
            CompanyId = companyId,
            Profile = profile
        };

        CompanyContext? Forbidden()
        {
            http.Items["company_forbidden"] = true;
            return null;
        }
    }

    public Guid RequireCompany()
    {
        if (CompanyId is { } id) return id;
        throw new InvalidOperationException("CompanyRequired");
    }
}
