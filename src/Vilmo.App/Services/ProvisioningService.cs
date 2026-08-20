using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vilmo.Data;
using Vilmo.Domain;
using Vilmo.Security;

namespace Vilmo.Services;

public sealed class ProvisioningService(AppDbContext db)
{
    readonly PasswordHasher<AppUser> _hasher = new();

    public async Task<Company> CreateCompanyAsync(CreateCompanyRequest req, CancellationToken ct)
    {
        var cnpj = Cnpj.Digits(req.Cnpj);
        if (!Cnpj.IsValid(cnpj)) throw new ArgumentException("InvalidCnpj");
        if (!Cep.IsValid(req.Cep)) throw new ArgumentException("InvalidCep");
        if (await db.Companies.AnyAsync(c => c.Cnpj == cnpj, ct)) throw new InvalidOperationException("DuplicateCnpj");
        var company = new Company
        {
            Id = Guid.NewGuid(),
            LegalName = req.LegalName.Trim(),
            TradeName = req.TradeName,
            Cnpj = cnpj,
            Ie = req.Ie,
            Im = req.Im,
            Email = req.Email,
            Phone = req.Phone,
            Street = req.Street,
            Number = req.Number,
            Complement = req.Complement,
            Neighborhood = req.Neighborhood,
            City = req.City,
            Uf = req.Uf.ToUpperInvariant(),
            Cep = Cep.Digits(req.Cep),
            Status = CompanyStatuses.Draft,
            TaxRegime = req.TaxRegime,
            NfeSerie = req.NfeSerie ?? "1",
            NfeEnvironment = req.NfeEnvironment ?? "Homologation"
        };
        db.Companies.Add(company);
        if (req.MarketplaceCodes is { Count: > 0 })
        {
            foreach (var code in req.MarketplaceCodes.Distinct())
            {
                if (!await db.Marketplaces.AnyAsync(m => m.Code == code, ct)) throw new ArgumentException("UnknownMarketplace");
                db.CompanyMarketplaceConfigs.Add(new CompanyMarketplaceConfig
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    MarketplaceCode = code,
                    IsEnabled = true,
                    LinkStatus = LinkStatuses.PendingConnect
                });
            }
        }
        await db.SaveChangesAsync(ct);
        return company;
    }

    public async Task<AppUser> CreateCompanyUserAsync(Guid companyId, CreateUserRequest req, CancellationToken ct)
    {
        if (req.MarketplaceCodes is not { Count: > 0 }) throw new ArgumentException("MarketplaceCodesRequired");
        var enabled = await db.CompanyMarketplaceConfigs.Where(c => c.CompanyId == companyId && c.IsEnabled).Select(c => c.MarketplaceCode).ToListAsync(ct);
        foreach (var code in req.MarketplaceCodes)
            if (!enabled.Contains(code)) throw new ArgumentException("CompanyMarketplaceNotEnabled");
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            user = new AppUser
            {
                Id = Guid.NewGuid(),
                Email = email,
                Name = req.Name,
                Phone = req.Phone,
                Status = string.IsNullOrEmpty(req.Password) ? UserStatuses.Invited : UserStatuses.Active
            };
            user.PasswordHash = _hasher.HashPassword(user, string.IsNullOrEmpty(req.Password) ? Guid.NewGuid().ToString("N") : req.Password);
            db.Users.Add(user);
        }
        if (!await db.UserCompanies.AnyAsync(x => x.UserId == user.Id && x.CompanyId == companyId, ct))
            db.UserCompanies.Add(new UserCompany { Id = Guid.NewGuid(), UserId = user.Id, CompanyId = companyId, Profile = req.Profile ?? UserProfiles.CompanyAdmin });
        foreach (var code in req.MarketplaceCodes.Distinct())
        {
            if (await db.UserCompanyMarketplaces.AnyAsync(x => x.CompanyId == companyId && x.UserId == user.Id && x.MarketplaceCode == code, ct)) continue;
            var cfg = await db.CompanyMarketplaceConfigs.FirstAsync(c => c.CompanyId == companyId && c.MarketplaceCode == code, ct);
            db.UserCompanyMarketplaces.Add(new UserCompanyMarketplace
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                UserId = user.Id,
                MarketplaceCode = code,
                Status = cfg.LinkStatus == LinkStatuses.Linked ? LinkStatuses.Linked : LinkStatuses.PendingConnect
            });
        }
        await db.SaveChangesAsync(ct);
        return user;
    }

    public async Task<AppUser> CreateVendorAsync(Guid companyId, CreateVendorRequest req, CancellationToken ct)
    {
        var codes = req.MarketplaceCodes;
        if (codes is null || codes.Count == 0 || codes.Contains("*"))
            codes = await db.CompanyMarketplaceConfigs.Where(c => c.CompanyId == companyId && c.IsEnabled).Select(c => c.MarketplaceCode).ToListAsync(ct);
        if (codes.Count == 0) throw new ArgumentException("MarketplaceCodesRequired");
        var enabled = await db.CompanyMarketplaceConfigs.Where(c => c.CompanyId == companyId && c.IsEnabled).Select(c => c.MarketplaceCode).ToListAsync(ct);
        foreach (var code in codes)
            if (!enabled.Contains(code)) throw new ArgumentException("CompanyMarketplaceNotEnabled");

        var email = req.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            user = new AppUser
            {
                Id = Guid.NewGuid(),
                Email = email,
                Name = req.Name,
                Phone = req.Phone,
                Status = UserStatuses.Active
            };
            user.PasswordHash = _hasher.HashPassword(user, string.IsNullOrEmpty(req.Password) ? "VilmoVendor!2026" : req.Password);
            db.Users.Add(user);
        }
        if (!await db.UserCompanies.AnyAsync(x => x.UserId == user.Id && x.CompanyId == companyId, ct))
            db.UserCompanies.Add(new UserCompany { Id = Guid.NewGuid(), UserId = user.Id, CompanyId = companyId, Profile = UserProfiles.Vendor });
        if (!await db.UsersDetails.AnyAsync(x => x.CompanyId == companyId && x.UserId == user.Id, ct))
            db.UsersDetails.Add(new UsersDetail
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                UserId = user.Id,
                DisplayName = req.Name,
                Document = req.Document,
                Phone = req.Phone
            });
        foreach (var code in codes.Distinct())
        {
            if (await db.UserDetailMarketplaces.AnyAsync(x => x.CompanyId == companyId && x.UserId == user.Id && x.MarketplaceCode == code, ct)) continue;
            db.UserDetailMarketplaces.Add(new UserDetailMarketplace
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                UserId = user.Id,
                MarketplaceCode = code,
                LinkStatus = LinkStatuses.PendingConnect
            });
        }
        await db.SaveChangesAsync(ct);
        return user;
    }
}

public sealed class CreateCompanyRequest
{
    public string LegalName { get; set; } = "";
    public string? TradeName { get; set; }
    public string Cnpj { get; set; } = "";
    public string? Ie { get; set; }
    public string? Im { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Street { get; set; } = "";
    public string Number { get; set; } = "";
    public string? Complement { get; set; }
    public string Neighborhood { get; set; } = "";
    public string City { get; set; } = "";
    public string Uf { get; set; } = "";
    public string Cep { get; set; } = "";
    public string? TaxRegime { get; set; }
    public string? NfeSerie { get; set; }
    public string? NfeEnvironment { get; set; }
    public List<string>? MarketplaceCodes { get; set; }
}

public sealed class CreateUserRequest
{
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string? Password { get; set; }
    public string? Profile { get; set; }
    public List<string> MarketplaceCodes { get; set; } = [];
}

public sealed class CreateVendorRequest
{
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string? Password { get; set; }
    public string? Document { get; set; }
    public List<string>? MarketplaceCodes { get; set; }
}
