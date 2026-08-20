using Microsoft.EntityFrameworkCore;

namespace Vilmo.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<UserCompany> UserCompanies => Set<UserCompany>();
    public DbSet<CompanyCertificate> CompanyCertificates => Set<CompanyCertificate>();
    public DbSet<Marketplace> Marketplaces => Set<Marketplace>();
    public DbSet<MarketplaceParameterDefinition> MarketplaceParameterDefinitions => Set<MarketplaceParameterDefinition>();
    public DbSet<CompanyMarketplaceConfig> CompanyMarketplaceConfigs => Set<CompanyMarketplaceConfig>();
    public DbSet<CompanyMarketplaceParameter> CompanyMarketplaceParameters => Set<CompanyMarketplaceParameter>();
    public DbSet<UsersDetail> UsersDetails => Set<UsersDetail>();
    public DbSet<UserDetailMarketplace> UserDetailMarketplaces => Set<UserDetailMarketplace>();
    public DbSet<UserCompanyMarketplace> UserCompanyMarketplaces => Set<UserCompanyMarketplace>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<InventoryBalance> InventoryBalances => Set<InventoryBalance>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<Advertisement> Advertisements => Set<Advertisement>();
    public DbSet<AdvertisementItem> AdvertisementItems => Set<AdvertisementItem>();
    public DbSet<AdvertisementAttribute> AdvertisementAttributes => Set<AdvertisementAttribute>();
    public DbSet<MarketplaceListingFieldDefinition> MarketplaceListingFieldDefinitions => Set<MarketplaceListingFieldDefinition>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<SaleMarketplaceAttribute> SaleMarketplaceAttributes => Set<SaleMarketplaceAttribute>();
    public DbSet<NfeDocument> NfeDocuments => Set<NfeDocument>();
    public DbSet<NfeIngestLog> NfeIngestLogs => Set<NfeIngestLog>();
    public DbSet<ShipmentLabel> ShipmentLabels => Set<ShipmentLabel>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Company>(e =>
        {
            e.ToTable("company");
            e.HasIndex(x => x.Cnpj).IsUnique();
        });
        b.Entity<AppUser>(e =>
        {
            e.ToTable("app_user");
            e.HasIndex(x => x.Email).IsUnique();
        });
        b.Entity<UserCompany>(e =>
        {
            e.ToTable("user_company");
            e.HasIndex(x => new { x.UserId, x.CompanyId }).IsUnique();
        });
        b.Entity<CompanyCertificate>().ToTable("company_certificate");
        b.Entity<Marketplace>(e =>
        {
            e.ToTable("marketplace");
            e.HasKey(x => x.Code);
        });
        b.Entity<MarketplaceParameterDefinition>(e =>
        {
            e.ToTable("marketplace_parameter_definition");
            e.HasIndex(x => new { x.MarketplaceCode, x.ParameterKey, x.Scope }).IsUnique();
        });
        b.Entity<CompanyMarketplaceConfig>(e =>
        {
            e.ToTable("company_marketplace_config");
            e.HasIndex(x => new { x.CompanyId, x.MarketplaceCode }).IsUnique();
        });
        b.Entity<CompanyMarketplaceParameter>(e =>
        {
            e.ToTable("company_marketplace_parameter");
            e.HasIndex(x => new { x.ConfigId, x.ParameterKey }).IsUnique();
        });
        b.Entity<UsersDetail>(e =>
        {
            e.ToTable("users_detail");
            e.HasIndex(x => new { x.CompanyId, x.UserId }).IsUnique();
        });
        b.Entity<UserDetailMarketplace>(e =>
        {
            e.ToTable("user_detail_marketplace");
            e.HasIndex(x => new { x.CompanyId, x.UserId, x.MarketplaceCode }).IsUnique();
        });
        b.Entity<UserCompanyMarketplace>(e =>
        {
            e.ToTable("user_company_marketplace");
            e.HasIndex(x => new { x.CompanyId, x.UserId, x.MarketplaceCode }).IsUnique();
        });
        b.Entity<Product>(e =>
        {
            e.ToTable("product");
            e.HasIndex(x => new { x.CompanyId, x.Sku }).IsUnique();
        });
        b.Entity<InventoryBalance>(e =>
        {
            e.ToTable("inventory_balance");
            e.HasIndex(x => new { x.CompanyId, x.Sku }).IsUnique();
        });
        b.Entity<InventoryMovement>(e =>
        {
            e.ToTable("inventory_movement");
            e.HasIndex(x => new { x.CompanyId, x.ChaveAcesso, x.NItem, x.Kind }).IsUnique();
            e.HasIndex(x => new { x.CompanyId, x.SaleId, x.Sku, x.Kind }).IsUnique();
        });
        b.Entity<Listing>(e =>
        {
            e.ToTable("listing");
            e.HasIndex(x => new { x.CompanyId, x.VendorUserId, x.Sku, x.MarketplaceCode }).IsUnique();
        });
        b.Entity<Advertisement>(e =>
        {
            e.ToTable("advertisement");
            e.HasIndex(x => new { x.CompanyId, x.VendorUserId, x.Sku }).IsUnique();
            e.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.AdvertisementId);
            e.HasMany(x => x.Attributes).WithOne().HasForeignKey(a => a.AdvertisementId);
        });
        b.Entity<AdvertisementItem>(e =>
        {
            e.ToTable("advertisement_item");
            e.HasIndex(x => new { x.AdvertisementId, x.Sku }).IsUnique();
        });
        b.Entity<AdvertisementAttribute>(e =>
        {
            e.ToTable("advertisement_attribute");
            e.HasIndex(x => new { x.AdvertisementId, x.MarketplaceCode, x.FieldName }).IsUnique();
        });
        b.Entity<MarketplaceListingFieldDefinition>(e =>
        {
            e.ToTable("marketplace_listing_field_definition");
            e.HasIndex(x => new { x.MarketplaceCode, x.FieldKey }).IsUnique();
        });
        b.Entity<Sale>(e =>
        {
            e.ToTable("sales");
            e.HasIndex(x => new { x.CompanyId, x.MarketplaceCode, x.RemoteOrderId }).IsUnique();
            e.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.SaleId);
            e.HasMany(x => x.Attributes).WithOne().HasForeignKey(a => a.SaleId);
        });
        b.Entity<SaleItem>().ToTable("sale_items");
        b.Entity<SaleMarketplaceAttribute>(e =>
        {
            e.ToTable("sale_marketplace_attributes");
            e.HasIndex(x => new { x.SaleId, x.FieldName }).IsUnique();
        });
        b.Entity<NfeDocument>(e =>
        {
            e.ToTable("nfe_documents");
            e.HasIndex(x => new { x.CompanyId, x.ChaveAcesso }).IsUnique();
        });
        b.Entity<NfeIngestLog>(e =>
        {
            e.ToTable("nfe_ingest_log");
            e.HasIndex(x => new { x.CompanyId, x.CreatedAt });
            e.HasIndex(x => new { x.CompanyId, x.ChaveAcesso, x.CreatedAt });
            e.HasIndex(x => x.RunId);
        });
        b.Entity<ShipmentLabel>(e =>
        {
            e.ToTable("shipment_labels");
            e.HasIndex(x => new { x.SaleId, x.Format }).IsUnique();
        });
        b.Entity<WorkItem>().ToTable("work_item");
        b.Entity<IdempotencyRecord>(e =>
        {
            e.ToTable("idempotency_record");
            e.HasIndex(x => new { x.CompanyId, x.Key }).IsUnique();
        });
        b.Entity<WebhookEvent>(e =>
        {
            e.ToTable("webhook_event");
            e.HasIndex(x => new { x.CompanyId, x.MarketplaceCode, x.EventId }).IsUnique();
        });
    }
}
