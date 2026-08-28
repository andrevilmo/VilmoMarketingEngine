using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vilmo.Data;
using Vilmo.Domain;
using Vilmo.Security;
using Vilmo.Services;

namespace Vilmo.Api;

public static class Endpoints
{
    static readonly Dictionary<string, RSA> MobileKeys = new();

    public static void MapVilmo(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Text("vilmo-api\n", "text/plain"));

        app.MapGet("/media/pictures/{stem}.{ext}", (string stem, string ext, AdvertisementPictureStore pics) =>
        {
            if (!pics.TryResolve($"{stem}.{ext}", out var path, out var mime))
                return Results.NotFound();
            return Results.File(path, mime);
        });

        app.MapPost("/auth/login", async (LoginBody body, AuthService auth, CancellationToken ct) =>
        {
            var (status, payload) = await auth.LoginAsync(body.Email ?? "", body.Password ?? "", ct);
            return Results.Json(payload, statusCode: status);
        });
        app.MapPost("/auth/logout", () => Results.Ok(new { ok = true })).RequireAuthorization();

        app.MapGet("/me", async (HttpContext http, AppDbContext db, AuthService auth, CancellationToken ct) =>
        {
            var ctx = await CompanyContext.ResolveAsync(http, db, ct);
            if (ctx is null) return Results.Unauthorized();
            var user = await db.Users.FirstAsync(u => u.Id == ctx.UserId, ct);
            var memberships = await (
                from uc in db.UserCompanies
                join c in db.Companies on uc.CompanyId equals c.Id
                where uc.UserId == user.Id
                select new MembershipDto(c.Id, c.Cnpj, c.LegalName, c.TradeName, uc.Profile, c.ReadyToList, c.ReadyToSyncSales, c.ReadyToInvoice)
            ).ToListAsync(ct);
            return Results.Ok(auth.MeDto(user, ctx.Level, memberships));
        }).RequireAuthorization();

        app.MapGet("/companies", async (HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var ctx = await Need(http, db, ct);
            if (ctx is IResult r) return r;
            var c = (CompanyContext)ctx;
            IQueryable<Company> q = db.Companies.AsNoTracking();
            if (!c.IsAdmin)
                q = q.Where(x => db.UserCompanies.Any(uc => uc.UserId == c.UserId && uc.CompanyId == x.Id));
            var rows = await q.OrderBy(x => x.TradeName).ToListAsync(ct);
            return Results.Ok(rows.Select(MapCompany));
        }).RequireAuthorization();

        app.MapPost("/companies", async (HttpContext http, AppDbContext db, ProvisioningService prov, CreateCompanyRequest body, CancellationToken ct) =>
        {
            var ctx = await NeedAdmin(http, db, ct);
            if (ctx is IResult r) return r;
            try
            {
                var company = await WithIdempotency(http, db, Guid.Empty, async () =>
                {
                    var created = await prov.CreateCompanyAsync(body, ct);
                    return (201, MapCompany(created));
                }, ct);
                return company;
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        }).RequireAuthorization();

        app.MapGet("/companies/{companyId:guid}", async (Guid companyId, HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var ctx = await Need(http, db, ct);
            if (ctx is IResult r) return r;
            var c = (CompanyContext)ctx;
            if (!c.IsAdmin && c.CompanyId != companyId) return Results.NotFound();
            var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == companyId, ct);
            return company is null ? Results.NotFound() : Results.Ok(MapCompany(company));
        }).RequireAuthorization();

        app.MapPost("/companies/{companyId:guid}/users", async (Guid companyId, CreateUserRequest body, HttpContext http, AppDbContext db, ProvisioningService prov, CancellationToken ct) =>
        {
            var ctx = await NeedAdmin(http, db, ct);
            if (ctx is IResult r) return r;
            try
            {
                return await WithIdempotency(http, db, companyId, async () =>
                {
                    var user = await prov.CreateCompanyUserAsync(companyId, body, ct);
                    return (201, new { user.Id, user.Email, user.Name });
                }, ct);
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).RequireAuthorization();

        app.MapPost("/companies/{companyId:guid}/vendors", async (Guid companyId, CreateVendorRequest body, HttpContext http, AppDbContext db, ProvisioningService prov, CancellationToken ct) =>
        {
            var ctx = await NeedCompanyStaff(http, db, ct, companyId);
            if (ctx is IResult r) return r;
            try
            {
                return await WithIdempotency(http, db, companyId, async () =>
                {
                    var user = await prov.CreateVendorAsync(companyId, body, ct);
                    return (201, new { user.Id, user.Email, user.Name, profile = UserProfiles.Vendor });
                }, ct);
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).RequireAuthorization();

        app.MapGet("/companies/{companyId:guid}/vendors", async (Guid companyId, HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var ctx = await NeedCompanyStaff(http, db, ct, companyId);
            if (ctx is IResult r) return r;
            var rows = await (
                from uc in db.UserCompanies
                join u in db.Users on uc.UserId equals u.Id
                where uc.CompanyId == companyId && uc.Profile == UserProfiles.Vendor
                select new { u.Id, u.Email, u.Name, u.Phone, u.Status }
            ).ToListAsync(ct);
            return Results.Ok(rows);
        }).RequireAuthorization();

        app.MapGet("/companies/{companyId:guid}/vendors/{userId:guid}", async (Guid companyId, Guid userId, HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            var ctx = (CompanyContext)ctxr;
            if (!ctx.IsAdmin && ctx.CompanyId != companyId) return Results.NotFound();
            if (ctx.IsVendor && ctx.UserId != userId) return Results.NotFound();
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user is null) return Results.NotFound();
            var detail = await db.UsersDetails.AsNoTracking().FirstOrDefaultAsync(d => d.CompanyId == companyId && d.UserId == userId, ct);
            var subs = await db.UserDetailMarketplaces.AsNoTracking().Where(s => s.CompanyId == companyId && s.UserId == userId).ToListAsync(ct);
            return Results.Ok(new
            {
                user.Id, user.Email, user.Name, user.Phone,
                detail,
                marketplaces = subs.Select(s => new { s.MarketplaceCode, s.LinkStatus })
            });
        }).RequireAuthorization();

        app.MapPut("/companies/{companyId:guid}/vendors/{userId:guid}/detail", async (Guid companyId, Guid userId, UsersDetail body, HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var ctx = await NeedCompanyStaff(http, db, ct, companyId);
            if (ctx is IResult r) return r;
            var row = await db.UsersDetails.FirstOrDefaultAsync(d => d.CompanyId == companyId && d.UserId == userId, ct);
            if (row is null) return Results.NotFound();
            row.DisplayName = body.DisplayName;
            row.Document = body.Document;
            row.Phone = body.Phone;
            row.Address = body.Address;
            await db.SaveChangesAsync(ct);
            return Results.Ok(row);
        }).RequireAuthorization();

        app.MapPost("/companies/{companyId:guid}/certificate", async (Guid companyId, HttpContext http, AppDbContext db, SecretProtector protector, MarketplaceService markets, CancellationToken ct) =>
        {
            var ctx = await NeedCompanyStaff(http, db, ct, companyId);
            if (ctx is IResult r) return r;
            var form = await http.Request.ReadFormAsync(ct);
            var password = form["password"].ToString();
            var file = form.Files["file"];
            var company = await db.Companies.FirstAsync(c => c.Id == companyId, ct);
            var dir = http.RequestServices.GetRequiredService<IConfiguration>()["Nfe:CertificatesDirectory"] ?? "/certs";
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{company.Cnpj}.pfx");
            if (file is { Length: > 0 })
            {
                await using var fs = File.Create(path);
                await file.CopyToAsync(fs, ct);
            }
            foreach (var old in db.CompanyCertificates.Where(c => c.CompanyId == companyId && c.IsActive))
                old.IsActive = false;
            db.CompanyCertificates.Add(new CompanyCertificate
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Cnpj = company.Cnpj,
                ContainerPath = $"/certs/{company.Cnpj}.pfx",
                PasswordCipher = string.IsNullOrEmpty(password) ? "" : protector.Protect(password),
                IsActive = true
            });
            await db.SaveChangesAsync(ct);
            await markets.RecomputeReadinessAsync(companyId, ct);
            return Results.Ok(new { ok = true, path });
        }).RequireAuthorization();

        app.MapGet("/companies/{companyId:guid}/marketplaces", async (Guid companyId, HttpContext http, AppDbContext db, MarketplaceService svc, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            var ctx = (CompanyContext)ctxr;
            if (ctx.IsVendor) return Results.NotFound();
            if (!ctx.IsAdmin && ctx.CompanyId != companyId) return Results.NotFound();
            return Results.Ok(await svc.GetCompanyMarketplacesAsync(companyId, ct));
        }).RequireAuthorization();

        app.MapGet("/companies/{companyId:guid}/marketplaces/{code}", async (Guid companyId, string code, HttpContext http, AppDbContext db, MarketplaceService svc, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            var ctx = (CompanyContext)ctxr;
            if (ctx.IsVendor) return Results.NotFound();
            var all = (IEnumerable<object>)await svc.GetCompanyMarketplacesAsync(companyId, ct);
            var one = all.FirstOrDefault(x => ((dynamic)x).Code == code);
            return one is null ? Results.NotFound() : Results.Ok(one);
        }).RequireAuthorization();

        app.MapGet("/companies/{companyId:guid}/marketplaces/{code}/logs", async (Guid companyId, string code, HttpContext http, AppDbContext db, MarketplaceConnectLogService logs, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            var ctx = (CompanyContext)ctxr;
            if (ctx.IsVendor) return Results.NotFound();
            if (!ctx.IsAdmin && ctx.CompanyId != companyId) return Results.NotFound();
            return Results.Ok(new { items = await logs.ListAsync(companyId, code, ct) });
        }).RequireAuthorization();

        app.MapPut("/companies/{companyId:guid}/marketplaces/{code}", async (Guid companyId, string code, JsonElement body, HttpContext http, AppDbContext db, MarketplaceService svc, CancellationToken ct) =>
        {
            var ctx = await NeedCompanyStaff(http, db, ct, companyId);
            if (ctx is IResult r) return r;
            var enabled = body.TryGetProperty("isEnabled", out var en) && en.GetBoolean();
            var values = new Dictionary<string, string?>();
            if (body.TryGetProperty("fields", out var fields) && fields.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in fields.EnumerateObject()) values[p.Name] = p.Value.GetString();
            }
            return await WithIdempotency(http, db, companyId, async () =>
            {
                await svc.UpsertCompanyAsync(companyId, code, enabled, values, ct);
                return (200, await svc.GetCompanyMarketplacesAsync(companyId, ct));
            }, ct);
        }).RequireAuthorization();

        app.MapGet("/marketplaces", async (HttpContext http, AppDbContext db, MarketplaceService svc, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            var ctx = (CompanyContext)ctxr;
            return Results.Ok(await svc.CatalogAsync(ctx.IsAdmin, ct));
        }).RequireAuthorization();

        app.MapGet("/marketplaces/{code}/parameter-definitions", async (string code, AppDbContext db, CancellationToken ct) =>
        {
            var defs = await db.MarketplaceParameterDefinitions.AsNoTracking()
                .Where(d => d.MarketplaceCode == code && d.Scope == "company")
                .OrderBy(d => d.SortOrder).ToListAsync(ct);
            return Results.Ok(defs.Select(d => new { d.ParameterKey, d.Label, d.IsSecret, d.FilledByOauth, d.Scope }));
        }).RequireAuthorization();

        app.MapPost("/marketplaces", async (HttpContext http, AppDbContext db, JsonElement body, CancellationToken ct) =>
        {
            var ctx = await NeedAdmin(http, db, ct);
            if (ctx is IResult r) return r;
            var code = body.GetProperty("code").GetString() ?? "";
            if (await db.Marketplaces.AnyAsync(m => m.Code == code, ct))
                return Results.Ok(await db.Marketplaces.FirstAsync(m => m.Code == code, ct));
            var mkt = new Marketplace
            {
                Code = code,
                DisplayName = body.TryGetProperty("displayName", out var n) ? n.GetString() ?? code : code,
                AuthProtocolCode = body.TryGetProperty("authProtocolCode", out var p) ? p.GetString() ?? "OAuth2" : "OAuth2",
                BaseUrl = body.TryGetProperty("baseUrl", out var u) ? u.GetString() ?? "" : "",
                IsActive = true
            };
            db.Marketplaces.Add(mkt);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/marketplaces/{code}", mkt);
        }).RequireAuthorization();

        app.MapPost("/marketplaces/{code}/connect", async (string code, HttpContext http, AppDbContext db, MarketplaceService svc, IConfiguration config, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            var ctx = (CompanyContext)ctxr;
            if (ctx.IsVendor) return Results.NotFound();
            var companyId = ctx.CompanyId ?? throw new InvalidOperationException("CompanyRequired");
            var pub = config["PUBLIC_BASE_URL"] ?? $"{http.Request.Scheme}://{http.Request.Host}";
            return Results.Ok(await svc.ConnectUrlAsync(companyId, code, pub, ct));
        }).RequireAuthorization();

        app.MapGet("/oauth/{marketplaceCode}/callback", async (
            string marketplaceCode,
            Guid? companyId,
            string? state,
            string? code,
            string? error,
            string? error_description,
            int? demo,
            HttpContext http,
            MarketplaceService svc,
            IConfiguration config,
            CancellationToken ct) =>
        {
            if (!string.IsNullOrWhiteSpace(error))
                return Results.Redirect(MarketplaceOAuthRedirect(marketplaceCode, "denied", error_description ?? error));
            Guid tenant;
            if (companyId is Guid cid)
                tenant = cid;
            else if (!Guid.TryParse(state, out tenant))
                return Results.BadRequest(new { error = "companyId required" });
            var pub = config["PUBLIC_BASE_URL"] ?? $"{http.Request.Scheme}://{http.Request.Host}";
            var result = await svc.HandleOAuthCallbackAsync(marketplaceCode, tenant, code, demo == 1, pub, ct);
            if (!result.Ok)
                return Results.Redirect(MarketplaceOAuthRedirect(marketplaceCode, "error", result.Error ?? "OAuthFailed"));
            return Results.Redirect(MarketplaceOAuthRedirect(marketplaceCode, "ok", null));
        });

        app.MapPost("/webhooks/{code}", async (string code, JsonElement body, MarketplaceService svc, CancellationToken ct) =>
        {
            var ack = await svc.HandleWebhookAsync(code, body, ct);
            return Results.Ok(ack);
        });

        app.MapGet("/inventory", async (HttpContext http, AppDbContext db, InventoryService inv, CancellationToken ct) =>
        {
            var ctx = await NeedNotVendor(http, db, ct);
            if (ctx is IResult r) return r;
            var c = (CompanyContext)ctx;
            return Results.Ok(await inv.ListAsync(c.RequireCompany(), ct));
        }).RequireAuthorization();

        app.MapGet("/inventory/{sku}", async (string sku, HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var ctx = await NeedNotVendor(http, db, ct);
            if (ctx is IResult r) return r;
            var c = (CompanyContext)ctx;
            var bal = await db.InventoryBalances.AsNoTracking().FirstOrDefaultAsync(b => b.CompanyId == c.CompanyId && b.Sku == sku, ct);
            return bal is null ? Results.NotFound() : Results.Ok(bal);
        }).RequireAuthorization();

        app.MapPut("/products/{sku}/sale-price", async (string sku, JsonElement body, HttpContext http, AppDbContext db, InventoryService inv, CancellationToken ct) =>
        {
            var ctx = await NeedNotVendor(http, db, ct);
            if (ctx is IResult r) return r;
            var c = (CompanyContext)ctx;
            var price = body.GetProperty("salePrice").GetDecimal();
            return await WithIdempotency(http, db, c.RequireCompany(), async () =>
            {
                await inv.SetSalePriceAsync(c.RequireCompany(), sku, price, ct);
                return (200, new { sku, salePrice = price });
            }, ct);
        }).RequireAuthorization();

        app.MapGet("/products", async (HttpContext http, AppDbContext db, ProductService products, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            var ctx = (CompanyContext)ctxr;
            if (ctx.CompanyId is null) return Results.BadRequest(new { error = "CompanyRequired" });
            return Results.Ok(await products.ListAsync(ctx.RequireCompany(), ct));
        }).RequireAuthorization();

        app.MapPost("/products", async (ProductDraft body, HttpContext http, AppDbContext db, ProductService products, CancellationToken ct) =>
        {
            var ctx = await NeedNotVendor(http, db, ct);
            if (ctx is IResult r) return r;
            var c = (CompanyContext)ctx;
            return await WithIdempotency(http, db, c.RequireCompany(), async () =>
            {
                var p = await products.UpsertAsync(c.RequireCompany(), body, ct);
                return (201, p);
            }, ct);
        }).RequireAuthorization();

        app.MapGet("/products/{sku}", async (string sku, HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var ctx = await NeedNotVendor(http, db, ct);
            if (ctx is IResult r) return r;
            var c = (CompanyContext)ctx;
            var p = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.CompanyId == c.CompanyId && x.Sku == sku, ct);
            return p is null ? Results.NotFound() : Results.Ok(p);
        }).RequireAuthorization();

        app.MapGet("/marketplaces/listing-fields", async (HttpContext http, AppDbContext db, AdvertisementService ads, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            return Results.Ok(await ads.ListingFieldsAsync(ct));
        }).RequireAuthorization();

        app.MapGet("/marketplaces/MercadoLivre/categories", async (HttpContext http, AppDbContext db, MercadoLivreCategoryService cats, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            return Results.Ok(await cats.ListRootsAsync(ct));
        }).RequireAuthorization();

        app.MapGet("/marketplaces/MercadoLivre/listing-types", async (HttpContext http, AppDbContext db, MercadoLivreCategoryService cats, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            return Results.Ok(await cats.ListListingTypesAsync(ct));
        }).RequireAuthorization();

        app.MapGet("/marketplaces/MercadoLivre/seller-status", async (HttpContext http, AppDbContext db, MercadoLivreCategoryService cats, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            var ctx = (CompanyContext)ctxr;
            return Results.Ok(await cats.SellerStatusAsync(ctx.RequireCompany(), ct));
        }).RequireAuthorization();

        app.MapGet("/marketplaces/MercadoLivre/categories/suggest", async (string? q, HttpContext http, AppDbContext db, MercadoLivreCategoryService cats, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            try { return Results.Ok(await cats.SuggestAsync(q, ct)); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).RequireAuthorization();

        app.MapGet("/marketplaces/MercadoLivre/categories/{categoryId}", async (string categoryId, HttpContext http, AppDbContext db, MercadoLivreCategoryService cats, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            var detail = await cats.GetAsync(categoryId, ct);
            return detail is null ? Results.NotFound(new { error = "CategoryNotFound" }) : Results.Ok(detail);
        }).RequireAuthorization();

        app.MapGet("/marketplaces/MercadoLivre/categories/{categoryId}/attributes", async (string categoryId, HttpContext http, AppDbContext db, MercadoLivreCategoryService cats, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            try { return Results.Ok(await cats.ListAttributesAsync(categoryId, ct)); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).RequireAuthorization();

        app.MapGet("/marketplaces/MercadoLivre/categories/{categoryId}/size-charts", async (
            string categoryId, string? genderId, string? genderName, string? brand,
            HttpContext http, AppDbContext db, MercadoLivreCategoryService cats, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            var ctx = (CompanyContext)ctxr;
            try { return Results.Ok(await cats.ListSizeChartsAsync(ctx.RequireCompany(), categoryId, genderId, genderName, brand, ct)); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).RequireAuthorization();

        app.MapGet("/marketplaces/MercadoLivre/size-charts/{chartId}", async (
            string chartId, HttpContext http, AppDbContext db, MercadoLivreCategoryService cats, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            var ctx = (CompanyContext)ctxr;
            try
            {
                var detail = await cats.GetSizeChartAsync(ctx.RequireCompany(), chartId, ct);
                return detail is null ? Results.NotFound(new { error = "SizeChartNotFound" }) : Results.Ok(detail);
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).RequireAuthorization();

        app.MapPost("/advertisements", Publish).RequireAuthorization();
        app.MapPost("/advertisements/pictures", async (
            HttpContext http, AppDbContext db, AdvertisementPictureStore pics, IConfiguration config, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            if (!http.Request.HasFormContentType)
                return Results.BadRequest(new { error = "InvalidPictureFile" });
            var form = await http.Request.ReadFormAsync(ct);
            var file = form.Files["file"] ?? form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "PictureEmpty" });
            try
            {
                await using var stream = file.OpenReadStream();
                var saved = await pics.SaveAsync(stream, ct);
                var url = AdvertisementPictureStore.PublicFileUrl(http, config, saved.FileName);
                return Results.Ok(new { url, fileName = saved.FileName, contentType = saved.ContentType });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();
        app.MapPost("/listings", Publish).RequireAuthorization();
        app.MapGet("/listings", async (HttpContext http, AppDbContext db, AdvertisementService ads, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            return Results.Ok(await ads.ListAsync((CompanyContext)ctxr, ct));
        }).RequireAuthorization();

        app.MapGet("/advertisements", async (HttpContext http, AppDbContext db, AdvertisementService ads, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            return Results.Ok(await ads.ListAsync((CompanyContext)ctxr, ct));
        }).RequireAuthorization();

        app.MapGet("/advertisements/{id:guid}", async (Guid id, HttpContext http, AppDbContext db, AdvertisementService ads, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            var mapped = await ads.GetAsync((CompanyContext)ctxr, id, ct);
            return mapped is null ? Results.NotFound(new { error = "AdvertisementNotFound" }) : Results.Ok(mapped);
        }).RequireAuthorization();

        app.MapPost("/advertisements/{id:guid}/refresh", async (Guid id, HttpContext http, AppDbContext db, AdvertisementService ads, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            var ctx = (CompanyContext)ctxr;
            return await WithIdempotency(http, db, ctx.RequireCompany(), async () =>
            {
                try { return (200, await ads.RefreshOnlineAsync(ctx, id, ct)); }
                catch (ArgumentException ex) { return (400, (object)new { error = ex.Message }); }
                catch (InvalidOperationException ex) { return (400, (object)new { error = ex.Message }); }
                catch (KeyNotFoundException ex) { return (404, (object)new { error = ex.Message }); }
            }, ct);
        }).RequireAuthorization();

        app.MapPost("/advertisements/{id:guid}/channels/{code}/publish", async (Guid id, string code, HttpContext http, AppDbContext db, AdvertisementService ads, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            var ctx = (CompanyContext)ctxr;
            return await WithIdempotency(http, db, ctx.RequireCompany(), async () =>
            {
                try { return (200, await ads.ProceedChannelAsync(ctx, id, code, ct)); }
                catch (ArgumentException ex) { return (400, (object)new { error = ex.Message }); }
                catch (InvalidOperationException ex) { return (400, (object)new { error = ex.Message }); }
                catch (KeyNotFoundException ex) { return (404, (object)new { error = ex.Message }); }
            }, ct);
        }).RequireAuthorization();

        app.MapPost("/advertisements/{id:guid}/channels/{code}/cancel", async (Guid id, string code, HttpContext http, AppDbContext db, AdvertisementService ads, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            var ctx = (CompanyContext)ctxr;
            return await WithIdempotency(http, db, ctx.RequireCompany(), async () =>
            {
                try { return (200, await ads.CancelChannelAsync(ctx, id, code, ct)); }
                catch (ArgumentException ex) { return (400, (object)new { error = ex.Message }); }
                catch (InvalidOperationException ex) { return (400, (object)new { error = ex.Message }); }
                catch (KeyNotFoundException ex) { return (404, (object)new { error = ex.Message }); }
            }, ct);
        }).RequireAuthorization();

        app.MapGet("/advertisements/{id:guid}/channels/{code}/logs", async (Guid id, string code, HttpContext http, AppDbContext db, AdvertisementService ads, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            try { return Results.Ok(await ads.ListChannelLogsAsync((CompanyContext)ctxr, id, code, ct)); }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
        }).RequireAuthorization();

        app.MapPost("/advertisements/imports/{code}", async (string code, JsonElement body, HttpContext http, AppDbContext db, ListingImportService imports, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            var ctx = (CompanyContext)ctxr;
            Guid? vendor = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("vendorUserId", out var v) && Guid.TryParse(v.GetString(), out var id)
                ? id : null;
            return await WithIdempotency(http, db, ctx.RequireCompany(), async () =>
            {
                try
                {
                    var result = await imports.EnqueueAsync(ctx, code, vendor, ct);
                    return (202, result);
                }
                catch (ArgumentException ex) { return (400, (object)new { error = ex.Message }); }
                catch (InvalidOperationException ex) { return (400, (object)new { error = ex.Message }); }
            }, ct);
        }).RequireAuthorization();

        app.MapGet("/advertisements/imports", async (string? marketplace, string? status, Guid? runId, HttpContext http, AppDbContext db, ListingImportService imports, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            return Results.Ok(await imports.ListAsync((CompanyContext)ctxr, marketplace, status, runId, ct));
        }).RequireAuthorization();

        app.MapGet("/advertisements/import-logs", async (Guid? runId, int? limit, HttpContext http, AppDbContext db, ListingImportService imports, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            return Results.Ok(await imports.ListLogsAsync((CompanyContext)ctxr, runId, limit, ct));
        }).RequireAuthorization();

        app.MapPost("/advertisements/imports/{id:guid}/link", async (Guid id, JsonElement body, HttpContext http, AppDbContext db, ListingImportService imports, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            var ctx = (CompanyContext)ctxr;
            var sku = body.TryGetProperty("sku", out var s) ? s.GetString() ?? "" : "";
            return await WithIdempotency(http, db, ctx.RequireCompany(), async () =>
            {
                try { return (200, await imports.LinkAsync(ctx, id, sku, ct)); }
                catch (ArgumentException ex) { return (400, (object)new { error = ex.Message }); }
                catch (InvalidOperationException ex) { return (400, (object)new { error = ex.Message }); }
                catch (KeyNotFoundException ex) { return (404, (object)new { error = ex.Message }); }
            }, ct);
        }).RequireAuthorization();

        app.MapPost("/advertisements/imports/{id:guid}/ignore", async (Guid id, HttpContext http, AppDbContext db, ListingImportService imports, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            var ctx = (CompanyContext)ctxr;
            return await WithIdempotency(http, db, ctx.RequireCompany(), async () =>
            {
                try { return (200, await imports.IgnoreAsync(ctx, id, ct)); }
                catch (InvalidOperationException ex) { return (400, (object)new { error = ex.Message }); }
                catch (KeyNotFoundException ex) { return (404, (object)new { error = ex.Message }); }
            }, ct);
        }).RequireAuthorization();

        app.MapPost("/inventory/{sku}/publish", async (string sku, JsonElement body, HttpContext http, AppDbContext db, AdvertisementService ads, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            var ctx = (CompanyContext)ctxr;
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                sku,
                kind = AdvertisementKinds.Product,
                marketplaceCodes = ReadCodes(body),
                vendorUserId = ctx.IsVendor ? ctx.UserId : (body.TryGetProperty("vendorUserId", out var v) && Guid.TryParse(v.GetString(), out var id) ? id : ctx.UserId),
                items = new[] { new { sku, quantity = 1m } }
            }));
            try
            {
                return Results.Ok(await ads.PublishAsync(ctx.RequireCompany(), ctx.UserId, ctx.IsVendor, doc.RootElement.Clone(), ct));
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        }).RequireAuthorization();

        app.MapPost("/nfe/chaves/{chave}/ingest", async (string chave, JsonElement body, HttpContext http, AppDbContext db, NfeIngestService nfe, CancellationToken ct) =>
        {
            var ctx = await NeedNotVendor(http, db, ct);
            if (ctx is IResult r) return r;
            var c = (CompanyContext)ctx;
            var cnpj = body.TryGetProperty("cnpj", out var cn) ? cn.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(cnpj))
                cnpj = (await db.Companies.FirstAsync(x => x.Id == c.CompanyId, ct)).Cnpj;
            return await WithIdempotency(http, db, c.RequireCompany(), async () =>
            {
                var result = await nfe.EnqueueChaveAsync(c.RequireCompany(), cnpj, chave, ct);
                return (202, result);
            }, ct);
        }).RequireAuthorization();

        app.MapPost("/nfe/xml", async (HttpContext http, AppDbContext db, NfeIngestService nfe, CancellationToken ct) =>
        {
            var ctx = await NeedNotVendor(http, db, ct);
            if (ctx is IResult r) return r;
            var c = (CompanyContext)ctx;
            string xml;
            if (http.Request.HasFormContentType)
            {
                var form = await http.Request.ReadFormAsync(ct);
                var file = form.Files["file"] ?? form.Files.FirstOrDefault();
                if (file is null) return Results.BadRequest(new { error = "file required" });
                using var reader = new StreamReader(file.OpenReadStream());
                xml = await reader.ReadToEndAsync(ct);
            }
            else
            {
                using var reader = new StreamReader(http.Request.Body);
                xml = await reader.ReadToEndAsync(ct);
            }
            return await WithIdempotency(http, db, c.RequireCompany(), async () =>
            {
                var result = await nfe.IngestXmlAsync(c.RequireCompany(), xml, ct);
                return (200, result);
            }, ct);
        }).RequireAuthorization();

        app.MapGet("/nfe/chaves/{chave}", async (string chave, HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var ctx = await NeedNotVendor(http, db, ct);
            if (ctx is IResult r) return r;
            var c = (CompanyContext)ctx;
            var digits = ChaveAcesso.Digits(chave);
            var doc = await db.NfeDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.CompanyId == c.CompanyId && d.ChaveAcesso == digits, ct);
            return doc is null ? Results.NotFound() : Results.Ok(new { doc.Id, doc.ChaveAcesso, doc.Status, doc.Error, doc.Kind });
        }).RequireAuthorization();

        app.MapGet("/nfe/ingest-logs", async (string? chave, Guid? runId, int? limit, HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var ctx = await NeedNotVendor(http, db, ct);
            if (ctx is IResult r) return r;
            var c = (CompanyContext)ctx;
            var take = Math.Clamp(limit ?? 80, 1, 200);
            var q = db.NfeIngestLogs.AsNoTracking().Where(l => l.CompanyId == c.CompanyId);
            if (runId is Guid rid)
                q = q.Where(l => l.RunId == rid);
            var digits = string.IsNullOrWhiteSpace(chave) ? "" : ChaveAcesso.Digits(chave);
            if (digits.Length == 44)
                q = q.Where(l => l.ChaveAcesso == digits);
            // SQLite cannot ORDER BY DateTimeOffset; Postgres can.
            List<NfeIngestLog> items;
            if (db.Database.IsNpgsql())
            {
                items = await q.OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id).Take(take).ToListAsync(ct);
            }
            else
            {
                items = (await q.ToListAsync(ct))
                    .OrderByDescending(l => l.CreatedAt)
                    .ThenByDescending(l => l.Id)
                    .Take(take)
                    .ToList();
            }
            return Results.Ok(new
            {
                items = items.Select(l => new
                {
                    l.Id,
                    l.RunId,
                    l.NfeDocumentId,
                    l.ChaveAcesso,
                    l.StepCode,
                    l.Level,
                    l.UserMessage,
                    l.TechnicalJson,
                    l.CreatedAt
                })
            });
        }).RequireAuthorization();

        app.MapGet("/nfe/mobile/session", async (HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var ctx = await NeedNotVendor(http, db, ct);
            if (ctx is IResult r) return r;
            var rsa = RSA.Create(2048);
            var kid = Guid.NewGuid().ToString("N");
            lock (MobileKeys) MobileKeys[kid] = rsa;
            return Results.Ok(new { kid, publicKey = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()), alg = "RSA-OAEP-256" });
        }).RequireAuthorization();

        app.MapPost("/nfe/mobile/ingest", async (JsonElement body, HttpContext http, AppDbContext db, NfeIngestService nfe, CancellationToken ct) =>
        {
            var ctx = await NeedNotVendor(http, db, ct);
            if (ctx is IResult r) return r;
            var c = (CompanyContext)ctx;
            var kid = body.GetProperty("kid").GetString() ?? "";
            RSA? rsa;
            lock (MobileKeys) MobileKeys.TryGetValue(kid, out rsa);
            if (rsa is null) return Results.BadRequest(new { error = "UnknownSession" });
            var wrapped = Convert.FromBase64String(body.GetProperty("wrappedKey").GetString() ?? "");
            var aesKey = rsa.Decrypt(wrapped, RSAEncryptionPadding.OaepSHA256);
            var iv = Convert.FromBase64String(body.GetProperty("iv").GetString() ?? "");
            var tag = Convert.FromBase64String(body.GetProperty("tag").GetString() ?? "");
            var cipher = Convert.FromBase64String(body.GetProperty("ciphertext").GetString() ?? "");
            var plain = new byte[cipher.Length];
            using (var gcm = new AesGcm(aesKey, 16))
                gcm.Decrypt(iv, cipher, tag, plain);
            var payload = JsonDocument.Parse(plain).RootElement;
            var chave = payload.GetProperty("chave").GetString() ?? "";
            var cnpj = payload.GetProperty("cnpj").GetString() ?? "";
            var result = await nfe.EnqueueChaveAsync(c.RequireCompany(), cnpj, chave, ct);
            lock (MobileKeys) MobileKeys.Remove(kid);
            return Results.Json(result, statusCode: 202);
        }).RequireAuthorization();

        app.MapGet("/sales", async (string? status, string? marketplace_code, HttpContext http, AppDbContext db, SalesService sales, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            var ctx = (CompanyContext)ctxr;
            if (ctx.CompanyId is null && !ctx.IsAdmin) return Results.BadRequest(new { error = "CompanyRequired" });
            if (ctx.CompanyId is null) return Results.BadRequest(new { error = "CompanyRequired" });
            return Results.Ok(await sales.ListAsync(ctx, status, marketplace_code, ct));
        }).RequireAuthorization();

        app.MapGet("/sales/{saleId:guid}", async (Guid saleId, HttpContext http, AppDbContext db, SalesService sales, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            var sale = await sales.GetAsync((CompanyContext)ctxr, saleId, ct);
            return sale is null ? Results.NotFound() : Results.Ok(sales.Map(sale));
        }).RequireAuthorization();

        app.MapPost("/sales/{saleId:guid}/sync", async (Guid saleId, HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var ctx = await NeedNotVendor(http, db, ct);
            if (ctx is IResult r) return r;
            var c = (CompanyContext)ctx;
            var sale = await db.Sales.FirstOrDefaultAsync(s => s.Id == saleId && s.CompanyId == c.CompanyId, ct);
            if (sale is null) return Results.NotFound();
            db.WorkItems.Add(new WorkItem
            {
                Id = Guid.NewGuid(),
                CompanyId = sale.CompanyId,
                Kind = WorkKinds.SaleImport,
                PayloadJson = $"{{\"resource\":\"{sale.RemoteOrderId}\",\"marketplace\":\"{sale.MarketplaceCode}\"}}"
            });
            await db.SaveChangesAsync(ct);
            return Results.Accepted($"/sales/{saleId}", new { queued = true });
        }).RequireAuthorization();

        app.MapPost("/sales/{saleId:guid}/nfe", async (Guid saleId, HttpContext http, AppDbContext db, SalesService sales, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            var ctx = (CompanyContext)ctxr;
            if (ctx.IsVendor) { /* vendors may request emit on own sales */ }
            try
            {
                var result = await sales.EmitNfeAsync(ctx, saleId, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        }).RequireAuthorization();

        app.MapGet("/sales/{saleId:guid}/nfe", async (Guid saleId, HttpContext http, AppDbContext db, SalesService sales, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            var sale = await sales.GetAsync((CompanyContext)ctxr, saleId, ct);
            if (sale is null) return Results.NotFound();
            var doc = await db.NfeDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.SaleId == saleId && d.Kind == NfeDocumentKinds.Outbound, ct);
            return doc is null ? Results.NotFound() : Results.Ok(new { doc.ChaveAcesso, doc.Status, doc.Protocol, xml = doc.Xml });
        }).RequireAuthorization();

        app.MapPost("/sales/{saleId:guid}/label", async (Guid saleId, JsonElement body, HttpContext http, AppDbContext db, SalesService sales, LabelService labels, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            var ctx = (CompanyContext)ctxr;
            var sale = await sales.GetAsync(ctx, saleId, ct);
            if (sale is null) return Results.NotFound();
            var format = body.TryGetProperty("format", out var f) ? f.GetString() ?? "Mm100x150" : "Mm100x150";
            var (pdf, used) = await labels.CreateAsync(sale.CompanyId, saleId, format, ct);
            return Results.File(pdf, "application/pdf", $"label-{saleId}.pdf");
        }).RequireAuthorization();

        app.MapGet("/sales/{saleId:guid}/label.pdf", async (Guid saleId, string? format, HttpContext http, AppDbContext db, SalesService sales, LabelService labels, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            var sale = await sales.GetAsync((CompanyContext)ctxr, saleId, ct);
            if (sale is null) return Results.NotFound();
            var (pdf, _) = await labels.CreateAsync(sale.CompanyId, saleId, format ?? "Mm100x150", ct);
            return Results.File(pdf, "application/pdf");
        }).RequireAuthorization();

        app.MapPost("/sales/{saleId:guid}/commit-stock", async (Guid saleId, HttpContext http, AppDbContext db, InventoryService inv, CancellationToken ct) =>
        {
            var ctx = await NeedNotVendor(http, db, ct);
            if (ctx is IResult r) return r;
            var c = (CompanyContext)ctx;
            var sale = await db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == saleId && s.CompanyId == c.CompanyId, ct);
            if (sale is null) return Results.NotFound();
            var (ok, err) = await inv.ApplySalePaidAsync(sale, ct);
            return ok ? Results.Ok(new { status = sale.Status }) : Results.Conflict(new { error = err });
        }).RequireAuthorization();

        app.MapGet("/users", async (HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            var ctx = (CompanyContext)ctxr;
            if (ctx.IsVendor) return Results.NotFound();
            var q =
                from uc in db.UserCompanies
                join u in db.Users on uc.UserId equals u.Id
                select new { uc.CompanyId, u.Id, u.Email, u.Name, u.Phone, u.Status, uc.Profile, u.IsPlatformSuperUser };
            if (!ctx.IsAdmin) q = q.Where(x => x.CompanyId == ctx.CompanyId);
            else if (ctx.CompanyId is { } cid) q = q.Where(x => x.CompanyId == cid);
            return Results.Ok(await q.ToListAsync(ct));
        }).RequireAuthorization();

        app.MapGet("/dashboard", async (HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            var ctx = (CompanyContext)ctxr;
            var sales = db.Sales.AsQueryable();
            if (ctx.IsVendor) sales = sales.Where(s => s.VendorUserId == ctx.UserId);
            if (ctx.CompanyId is { } cid) sales = sales.Where(s => s.CompanyId == cid);
            var byStatus = await sales.GroupBy(s => s.Status).Select(g => new { status = g.Key, count = g.Count() }).ToListAsync(ct);
            var companies = ctx.IsAdmin ? await db.Companies.CountAsync(ct) : 1;
            var users = ctx.IsAdmin
                ? await db.Users.CountAsync(ct)
                : await db.UserCompanies.CountAsync(x => x.CompanyId == ctx.CompanyId, ct);
            var lowStock = ctx.IsVendor ? 0 : await db.InventoryBalances.CountAsync(b => b.CompanyId == ctx.CompanyId && b.OnHand <= 2, ct);
            return Results.Ok(new { ctx.Level, companies, users, lowStock, salesByStatus = byStatus });
        }).RequireAuthorization();

        static async Task<IResult> Publish(JsonElement body, HttpContext http, AppDbContext db, AdvertisementService ads, CancellationToken ct)
        {
            var ctxr = await Need(http, db, ct);
            if (ctxr is IResult r) return r;
            var ctx = (CompanyContext)ctxr;
            return await WithIdempotency(http, db, ctx.RequireCompany(), async () =>
            {
                try
                {
                    var created = await ads.PublishAsync(ctx.RequireCompany(), ctx.UserId, ctx.IsVendor, body, ct);
                    return (201, created);
                }
                catch (ArgumentException ex) { return (400, (object)new { error = ex.Message }); }
                catch (InvalidOperationException ex) { return (400, (object)new { error = ex.Message }); }
                catch (KeyNotFoundException ex) { return (404, (object)new { error = ex.Message }); }
            }, ct);
        }
    }

    static List<string>? ReadCodes(JsonElement body)
    {
        if (!body.TryGetProperty("marketplaceCodes", out var arr) || arr.ValueKind != JsonValueKind.Array) return null;
        return arr.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x != "").ToList();
    }

    static object MapCompany(Company c) => new
    {
        c.Id, c.LegalName, c.TradeName, c.Cnpj, cnpjFormatted = Cnpj.Format(c.Cnpj),
        c.Ie, c.Im, c.Email, c.Phone,
        address = new { c.Street, c.Number, c.Complement, c.Neighborhood, c.City, c.Uf, c.Cep },
        c.Status, c.TaxRegime, c.NfeSerie, c.NfeEnvironment,
        c.ReadyToList, c.ReadyToSyncSales, c.ReadyToInvoice,
        readyToOperate = c.ReadyToList && c.ReadyToSyncSales && c.ReadyToInvoice
    };

    static async Task<object> Need(HttpContext http, AppDbContext db, CancellationToken ct)
    {
        var ctx = await CompanyContext.ResolveAsync(http, db, ct);
        if (http.Items.ContainsKey("company_forbidden")) return Results.Json(new { error = "ForbiddenCompany" }, statusCode: 403);
        if (http.Items.ContainsKey("company_required")) return Results.BadRequest(new { error = "CompanyRequired" });
        if (ctx is null) return Results.Unauthorized();
        return ctx;
    }

    static async Task<object> NeedAdmin(HttpContext http, AppDbContext db, CancellationToken ct)
    {
        var ctx = await Need(http, db, ct);
        if (ctx is IResult) return ctx;
        if (!((CompanyContext)ctx).IsAdmin) return Results.Json(new { error = "AdminOnly" }, statusCode: 403);
        return ctx;
    }

    static async Task<object> NeedNotVendor(HttpContext http, AppDbContext db, CancellationToken ct)
    {
        var ctx = await Need(http, db, ct);
        if (ctx is IResult) return ctx;
        if (((CompanyContext)ctx).IsVendor) return Results.NotFound();
        if (((CompanyContext)ctx).CompanyId is null) return Results.BadRequest(new { error = "CompanyRequired" });
        return ctx;
    }

    static async Task<object> NeedCompanyStaff(HttpContext http, AppDbContext db, CancellationToken ct, Guid companyId)
    {
        var ctx = await Need(http, db, ct);
        if (ctx is IResult) return ctx;
        var c = (CompanyContext)ctx;
        if (c.IsVendor) return Results.NotFound();
        if (!c.IsAdmin && c.CompanyId != companyId) return Results.NotFound();
        return c;
    }

    static async Task<IResult> WithIdempotency(HttpContext http, AppDbContext db, Guid companyId, Func<Task<(int Status, object Body)>> run, CancellationToken ct)
    {
        var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key))
        {
            var (status, body) = await run();
            return Results.Json(body, statusCode: status);
        }
        var scopedCompany = companyId == Guid.Empty ? Guid.Empty : companyId;
        var existing = await db.IdempotencyRecords.FirstOrDefaultAsync(x => x.CompanyId == scopedCompany && x.Key == key, ct);
        if (existing is not null)
            return Results.Json(JsonSerializer.Deserialize<object>(existing.Body), statusCode: existing.StatusCode);
        var (st, payload) = await run();
        db.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            CompanyId = scopedCompany,
            Key = key,
            StatusCode = st,
            Body = JsonSerializer.Serialize(payload)
        });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { /* raced */ }
        return Results.Json(payload, statusCode: st);
    }

    static string MarketplaceOAuthRedirect(string marketplaceCode, string oauth, string? error)
    {
        var url = $"/web/#/marketplaces?connected={Uri.EscapeDataString(marketplaceCode)}&oauth={Uri.EscapeDataString(oauth)}";
        if (string.IsNullOrWhiteSpace(error)) return url;
        var clipped = error.Length > 180 ? error[..180] : error;
        return url + "&error=" + Uri.EscapeDataString(clipped);
    }

    public sealed record LoginBody(string? Email, string? Password);
}
