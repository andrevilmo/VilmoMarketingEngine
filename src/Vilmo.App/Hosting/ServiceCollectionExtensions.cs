using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Vilmo.Data;
using Vilmo.Security;
using Vilmo.Services;
using Vilmo.Workers;

namespace Vilmo.Hosting;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVilmo(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(o =>
        {
            if (string.Equals(config["DB_PROVIDER"], "sqlite", StringComparison.OrdinalIgnoreCase))
            {
                var sqlite = config.GetConnectionString("Sqlite") ?? "Data Source=vilmo.test.db";
                o.UseSqlite(sqlite);
            }
            else
            {
                var cs = config.GetConnectionString("Postgres")
                         ?? config["POSTGRES_CONNECTION"]
                         ?? "Host=postgres;Port=5432;Database=vilmo;Username=vilmo;Password=vilmo";
                o.UseNpgsql(cs);
            }
            o.UseSnakeCaseNamingConvention();
        });
        services.AddSingleton<SecretProtector>();
        services.AddScoped<AuthService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<NfeIngestService>();
        services.AddScoped<SalesService>();
        services.AddScoped<LabelService>();
        services.AddScoped<MarketplaceService>();
        services.AddScoped<ProvisioningService>();
        services.AddScoped<ProductService>();
        services.AddScoped<WorkProcessor>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AuthService.SigningKey(config))),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });
        services.AddAuthorization();
        return services;
    }

    public static WebApplicationBuilder AddVilmoAuth(this WebApplicationBuilder builder)
    {
        builder.Services.AddVilmo(builder.Configuration);
        return builder;
    }
}
