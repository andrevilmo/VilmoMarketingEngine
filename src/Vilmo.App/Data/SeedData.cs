using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vilmo.Data;
using Vilmo.Domain;
using Vilmo.Security;

namespace Vilmo.Data;

public static class SeedData
{
    public static async Task ApplyAsync(AppDbContext db, IConfiguration config, CancellationToken ct = default)
    {
        await SeedMarketplacesAsync(db, ct);
        await SeedFirstCompanyAsync(db, config, ct);
    }

    static async Task SeedMarketplacesAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.Marketplaces.AnyAsync(ct)) return;

        var rows = new (string Code, string Name, string Protocol, string Url, bool Active)[]
        {
            ("MercadoLivre", "Mercado Livre", "OAuth2", "https://api.mercadolibre.com", true),
            ("Shopee", "Shopee", "HmacSha256", "https://openplatform.shopee.com.br/api/v2/", true),
            ("Shein", "SHEIN", "HmacSha256", "https://openapi.sheincorp.com", false),
            ("Magalu", "Magalu", "OAuth2", "https://id.magalu.com", true)
        };
        foreach (var r in rows)
            db.Marketplaces.Add(new Marketplace { Code = r.Code, DisplayName = r.Name, AuthProtocolCode = r.Protocol, BaseUrl = r.Url, IsActive = r.Active });

        void Def(string code, string key, string scope, bool secret, bool oauth, string label, int order) =>
            db.MarketplaceParameterDefinitions.Add(new MarketplaceParameterDefinition
            {
                Id = Guid.NewGuid(),
                MarketplaceCode = code,
                ParameterKey = key,
                Scope = scope,
                IsSecret = secret,
                FilledByOauth = oauth,
                Label = label,
                SortOrder = order
            });

        Def("MercadoLivre", "ClientId", "company", false, false, "Client ID", 1);
        Def("MercadoLivre", "ClientSecret", "company", true, false, "Client Secret", 2);
        Def("MercadoLivre", "SiteId", "company", false, false, "Site ID", 3);
        Def("MercadoLivre", "AccessToken", "company", true, true, "Access Token", 10);
        Def("MercadoLivre", "RefreshToken", "company", true, true, "Refresh Token", 11);
        Def("MercadoLivre", "UserId", "company", false, true, "User ID", 12);

        Def("Shopee", "PartnerId", "company", false, false, "Partner ID", 1);
        Def("Shopee", "PartnerKey", "company", true, false, "Partner Key", 2);
        Def("Shopee", "ShopId", "company", false, true, "Shop ID", 10);
        Def("Shopee", "AccessToken", "company", true, true, "Access Token", 11);
        Def("Shopee", "RefreshToken", "company", true, true, "Refresh Token", 12);

        Def("Shein", "AppId", "company", false, false, "App ID", 1);
        Def("Shein", "AppSecret", "company", true, false, "App Secret", 2);
        Def("Shein", "OpenKeyId", "company", false, true, "Open Key ID", 10);
        Def("Shein", "SecretKey", "company", true, true, "Secret Key", 11);

        Def("Magalu", "ClientId", "company", false, false, "Client ID", 1);
        Def("Magalu", "ClientSecret", "company", true, false, "Client Secret", 2);
        Def("Magalu", "Scope", "company", false, false, "Scope", 3);
        Def("Magalu", "AccessToken", "company", true, true, "Access Token", 10);
        Def("Magalu", "RefreshToken", "company", true, true, "Refresh Token", 11);

        await db.SaveChangesAsync(ct);
    }

    static async Task SeedFirstCompanyAsync(AppDbContext db, IConfiguration config, CancellationToken ct)
    {
        const string cnpj = "68431371000161";
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Cnpj == cnpj, ct);
        if (company is null)
        {
            var jsonPath = config["FIRST_COMPANY_JSON"] ?? "MD/seed/first-company.json";
            CompanySeedFile? seed = null;
            foreach (var candidate in new[] { jsonPath, Path.GetFullPath(jsonPath), "/app/seed/first-company.json" })
            {
                if (File.Exists(candidate))
                {
                    seed = JsonSerializer.Deserialize<CompanySeedFile>(await File.ReadAllTextAsync(candidate, ct),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    break;
                }
            }

            company = new Company
            {
                Id = Guid.NewGuid(),
                LegalName = seed?.LegalName ?? "A. VILMO PINHEIRO CARDOSO TECNOLOGIA LTDA",
                TradeName = seed?.TradeName ?? "VILMO COMERCIO, REPRESENTACOES E INFORMATICA",
                Cnpj = cnpj,
                Email = seed?.Email ?? "andre.vilmo@gmail.com",
                Phone = seed?.Phone ?? "5180227183",
                Street = seed?.Address?.Street ?? "R VITOR KONDER",
                Number = seed?.Address?.Number ?? "223",
                Complement = seed?.Address?.Complement ?? "SALA 1108",
                Neighborhood = seed?.Address?.Neighborhood ?? "CENTRO",
                City = seed?.Address?.City ?? "FLORIANOPOLIS",
                Uf = seed?.Address?.Uf ?? "SC",
                Cep = seed?.Address?.Cep ?? "88015400",
                Status = CompanyStatuses.Active,
                TaxRegime = "Simples",
                NfeSerie = "1",
                NextNnf = 1
            };
            db.Companies.Add(company);
            await db.SaveChangesAsync(ct);
        }

        var hasher = new PasswordHasher<AppUser>();
        var adminEmail = "admin@vilmomkt.com";
        var admin = await db.Users.FirstOrDefaultAsync(u => u.Email == adminEmail, ct);
        var adminPassword = config["BOOTSTRAP_ADMIN_PASSWORD"] ?? "VilmoAdmin!2026";
        if (admin is null)
        {
            admin = new AppUser
            {
                Id = Guid.NewGuid(),
                Email = adminEmail,
                Name = "Admin Vilmo",
                PasswordHash = "",
                IsPlatformSuperUser = true,
                Status = UserStatuses.Active
            };
            admin.PasswordHash = hasher.HashPassword(admin, adminPassword);
            db.Users.Add(admin);
            db.UserCompanies.Add(new UserCompany
            {
                Id = Guid.NewGuid(),
                UserId = admin.Id,
                CompanyId = company.Id,
                Profile = UserProfiles.CompanyAdmin
            });
            await db.SaveChangesAsync(ct);
        }

        var companyEmail = "empresa@vilmomkt.com";
        if (!await db.Users.AnyAsync(u => u.Email == companyEmail, ct))
        {
            var u = new AppUser
            {
                Id = Guid.NewGuid(),
                Email = companyEmail,
                Name = "Empresa Vilmo",
                IsPlatformSuperUser = false,
                Status = UserStatuses.Active
            };
            u.PasswordHash = hasher.HashPassword(u, config["BOOTSTRAP_COMPANY_PASSWORD"] ?? adminPassword);
            db.Users.Add(u);
            db.UserCompanies.Add(new UserCompany { Id = Guid.NewGuid(), UserId = u.Id, CompanyId = company.Id, Profile = UserProfiles.CompanyAdmin });
            await db.SaveChangesAsync(ct);
        }

        var vendorEmail = "vendedor@vilmomkt.com";
        if (!await db.Users.AnyAsync(u => u.Email == vendorEmail, ct))
        {
            var u = new AppUser
            {
                Id = Guid.NewGuid(),
                Email = vendorEmail,
                Name = "Vendedor Vilmo",
                IsPlatformSuperUser = false,
                Status = UserStatuses.Active
            };
            u.PasswordHash = hasher.HashPassword(u, config["BOOTSTRAP_VENDOR_PASSWORD"] ?? adminPassword);
            db.Users.Add(u);
            db.UserCompanies.Add(new UserCompany { Id = Guid.NewGuid(), UserId = u.Id, CompanyId = company.Id, Profile = UserProfiles.Vendor });
            db.UsersDetails.Add(new UsersDetail
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                UserId = u.Id,
                DisplayName = "Vendedor Vilmo",
                Phone = "5180227183"
            });
            foreach (var code in new[] { "MercadoLivre", "Shopee", "Magalu" })
            {
                db.UserDetailMarketplaces.Add(new UserDetailMarketplace
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    UserId = u.Id,
                    MarketplaceCode = code,
                    LinkStatus = LinkStatuses.PendingConnect
                });
            }
            await db.SaveChangesAsync(ct);
        }

        var certPath = config["Nfe:CertificatesDirectory"] ?? "/certs";
        var pfx = Path.Combine(certPath, $"{cnpj}.pfx");
        if (File.Exists(pfx) && !await db.CompanyCertificates.AnyAsync(x => x.CompanyId == company.Id && x.IsActive, ct))
        {
            var passFile = config["COMPANY_68431371000161_A1_PASSWORD_FILE"];
            var password = config["COMPANY_68431371000161_A1_PASSWORD"] ?? "";
            if (string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(passFile) && File.Exists(passFile))
                password = (await File.ReadAllTextAsync(passFile, ct)).Trim();
            var protector = new SecretProtector(config);
            db.CompanyCertificates.Add(new CompanyCertificate
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Cnpj = cnpj,
                ContainerPath = $"/certs/{cnpj}.pfx",
                PasswordCipher = string.IsNullOrEmpty(password) ? "" : protector.Protect(password),
                IsActive = true,
                ValidFrom = DateTimeOffset.Parse("2026-08-13T17:48:12Z"),
                ValidTo = DateTimeOffset.Parse("2027-08-13T17:48:12Z")
            });
            company.ReadyToInvoice = !string.IsNullOrWhiteSpace(company.Ie);
            await db.SaveChangesAsync(ct);
        }

        if (!await db.Products.AnyAsync(p => p.CompanyId == company.Id, ct))
        {
            db.Products.Add(new Product
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Sku = "CAMISETA-001",
                Name = "Camiseta Vilmo",
                Ean = "7891234567895",
                Ncm = "61091000",
                Cfop = "5102",
                SalePrice = 89.90m
            });
            db.InventoryBalances.Add(new InventoryBalance
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Sku = "CAMISETA-001",
                OnHand = 10
            });
            await db.SaveChangesAsync(ct);
        }
    }

    sealed class CompanySeedFile
    {
        public string? LegalName { get; set; }
        public string? TradeName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public AddressSeed? Address { get; set; }
    }

    sealed class AddressSeed
    {
        public string? Street { get; set; }
        public string? Number { get; set; }
        public string? Complement { get; set; }
        public string? Neighborhood { get; set; }
        public string? City { get; set; }
        public string? Uf { get; set; }
        public string? Cep { get; set; }
    }
}
