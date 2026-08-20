using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Vilmo.Data;
using Vilmo.Domain;

namespace Vilmo.Security;

public sealed class AuthService(AppDbContext db, IConfiguration config)
{
    readonly PasswordHasher<AppUser> _hasher = new();

    public async Task<(int Status, object Body)> LoginAsync(string email, string password, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email.Trim().ToLowerInvariant(), ct);
        if (user is null) return (401, Error("InvalidCredentials", "E-mail ou senha inválidos."));
        if (user.Status == UserStatuses.Disabled) return (403, Error("UserDisabled", "Usuário desativado."));
        if (user.LockoutEnd is { } until && until > DateTimeOffset.UtcNow)
            return (403, Error("Locked", "Muitas tentativas. Tente novamente em alguns minutos."));

        if (_hasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= 5)
            {
                user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15);
                user.FailedLoginCount = 0;
            }
            await db.SaveChangesAsync(ct);
            return (401, Error("InvalidCredentials", "E-mail ou senha inválidos."));
        }

        user.FailedLoginCount = 0;
        user.LockoutEnd = null;
        await db.SaveChangesAsync(ct);

        var memberships = await (
            from uc in db.UserCompanies
            join c in db.Companies on uc.CompanyId equals c.Id
            where uc.UserId == user.Id
            select new MembershipDto(c.Id, c.Cnpj, c.LegalName, c.TradeName, uc.Profile, c.ReadyToList, c.ReadyToSyncSales, c.ReadyToInvoice)
        ).ToListAsync(ct);

        var level = user.IsPlatformSuperUser
            ? LoginLevels.Admin
            : memberships.Any(m => m.Profile == UserProfiles.Vendor) && memberships.All(m => m.Profile == UserProfiles.Vendor)
                ? LoginLevels.Vendor
                : LoginLevels.Company;

        var token = IssueToken(user, memberships, level);
        return (200, new
        {
            accessToken = token,
            expiresIn = 8 * 3600,
            user = MeDto(user, level, memberships)
        });
    }

    public object MeDto(AppUser user, string level, IReadOnlyList<MembershipDto> memberships) => new
    {
        user.Id,
        user.Email,
        user.Name,
        user.IsPlatformSuperUser,
        level,
        memberships,
        readiness = memberships.Select(m => new { m.CompanyId, m.ReadyToList, m.ReadyToSyncSales, m.ReadyToInvoice })
    };

    public string IssueToken(AppUser user, IReadOnlyList<MembershipDto> memberships, string level)
    {
        var key = Encoding.UTF8.GetBytes(SigningKey(config));
        var claims = new List<Claim>
        {
            new("user_id", user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("is_platform_super_user", user.IsPlatformSuperUser ? "true" : "false"),
            new("level", level)
        };
        if (memberships.Count == 1)
            claims.Add(new("company_id", memberships[0].CompanyId.ToString()));

        var jwt = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    public static string SigningKey(IConfiguration config)
    {
        var k = config["JWT_SIGNING_KEY"] ?? config["Vilmo:JwtSigningKey"] ?? "vilmo-dev-jwt-signing-key-change-me-32";
        return k.Length >= 32 ? k : k.PadRight(32, 'x');
    }

    public string HashPassword(AppUser user, string password) => _hasher.HashPassword(user, password);

    static object Error(string code, string message) => new { error = code, message };
}

public sealed record MembershipDto(
    Guid CompanyId,
    string Cnpj,
    string LegalName,
    string? TradeName,
    string Profile,
    bool ReadyToList,
    bool ReadyToSyncSales,
    bool ReadyToInvoice);
