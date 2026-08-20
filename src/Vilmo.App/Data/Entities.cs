namespace Vilmo.Data;

public class Company
{
    public Guid Id { get; set; }
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
    public string Status { get; set; } = "Draft";
    public string? TaxRegime { get; set; }
    public string? NfeSerie { get; set; }
    public long NextNnf { get; set; } = 1;
    public string NfeEnvironment { get; set; } = "Homologation";
    public string DefaultCfopIntra { get; set; } = "5102";
    public string DefaultCfopInterstate { get; set; } = "6102";
    public bool ReadyToList { get; set; }
    public bool ReadyToSyncSales { get; set; }
    public bool ReadyToInvoice { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class AppUser
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string PasswordHash { get; set; } = "";
    public bool IsPlatformSuperUser { get; set; }
    public string Status { get; set; } = "Active";
    public int FailedLoginCount { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class UserCompany
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CompanyId { get; set; }
    public string Profile { get; set; } = "";
    public AppUser? User { get; set; }
    public Company? Company { get; set; }
}

public class CompanyCertificate
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Cnpj { get; set; } = "";
    public string ContainerPath { get; set; } = "";
    public string PasswordCipher { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
}

public class Marketplace
{
    public string Code { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string AuthProtocolCode { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class MarketplaceParameterDefinition
{
    public Guid Id { get; set; }
    public string MarketplaceCode { get; set; } = "";
    public string ParameterKey { get; set; } = "";
    public string Scope { get; set; } = "company";
    public bool IsSecret { get; set; }
    public bool FilledByOauth { get; set; }
    public string Label { get; set; } = "";
    public int SortOrder { get; set; }
}

public class CompanyMarketplaceConfig
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string MarketplaceCode { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public string LinkStatus { get; set; } = "PendingConnect";
}

public class CompanyMarketplaceParameter
{
    public Guid Id { get; set; }
    public Guid ConfigId { get; set; }
    public string ParameterKey { get; set; } = "";
    public string ParameterValue { get; set; } = "";
    public bool IsSecret { get; set; }
}

public class UsersDetail
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = "";
    public string? Document { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
}

public class UserDetailMarketplace
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public string MarketplaceCode { get; set; } = "";
    public string LinkStatus { get; set; } = "PendingConnect";
    public string ParametersJson { get; set; } = "{}";
}

public class UserCompanyMarketplace
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public string MarketplaceCode { get; set; } = "";
    public string Status { get; set; } = "PendingConnect";
}

public class Product
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Ean { get; set; }
    public string? Ncm { get; set; }
    public string? Cfop { get; set; }
    public decimal SalePrice { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class InventoryBalance
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Sku { get; set; } = "";
    public decimal OnHand { get; set; }
}

public class InventoryMovement
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Sku { get; set; } = "";
    public string Kind { get; set; } = "";
    public decimal Quantity { get; set; }
    public string? ChaveAcesso { get; set; }
    public int? NItem { get; set; }
    public Guid? SaleId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Listing
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid VendorUserId { get; set; }
    public string Sku { get; set; } = "";
    public string MarketplaceCode { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public string? RemoteId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Sale
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? VendorUserId { get; set; }
    public string MarketplaceCode { get; set; } = "";
    public string RemoteOrderId { get; set; } = "";
    public string Status { get; set; } = "PendingPayment";
    public decimal Total { get; set; }
    public string BuyerName { get; set; } = "";
    public string RecipientName { get; set; } = "";
    public string Street { get; set; } = "";
    public string Number { get; set; } = "";
    public string? Complement { get; set; }
    public string Neighborhood { get; set; } = "";
    public string City { get; set; } = "";
    public string Uf { get; set; } = "";
    public string Cep { get; set; } = "";
    public string? Tracking { get; set; }
    public string? StockShort { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<SaleItem> Items { get; set; } = [];
    public List<SaleMarketplaceAttribute> Attributes { get; set; } = [];
}

public class SaleItem
{
    public Guid Id { get; set; }
    public Guid SaleId { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Ncm { get; set; }
    public string? Cfop { get; set; }
}

public class SaleMarketplaceAttribute
{
    public Guid Id { get; set; }
    public Guid SaleId { get; set; }
    public string FieldName { get; set; } = "";
    public string FieldValue { get; set; } = "";
}

public class NfeDocument
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? SaleId { get; set; }
    public string Kind { get; set; } = "Inbound";
    public string ChaveAcesso { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public string? Xml { get; set; }
    public string? Protocol { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ShipmentLabel
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid SaleId { get; set; }
    public string Format { get; set; } = "Mm100x150";
    public byte[] Pdf { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class WorkItem
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Kind { get; set; } = "";
    public string PayloadJson { get; set; } = "{}";
    public string Status { get; set; } = "Pending";
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
}

public class IdempotencyRecord
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Key { get; set; } = "";
    public int StatusCode { get; set; }
    public string Body { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class WebhookEvent
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string MarketplaceCode { get; set; } = "";
    public string EventId { get; set; } = "";
    public string PayloadJson { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
