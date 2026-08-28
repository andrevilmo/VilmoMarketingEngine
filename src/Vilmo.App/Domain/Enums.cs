namespace Vilmo.Domain;

public static class UserProfiles
{
    public const string CompanyAdmin = "CompanyAdmin";
    public const string Operator = "Operator";
    public const string Viewer = "Viewer";
    public const string Vendor = "Vendor";
}

public static class CompanyStatuses
{
    public const string Draft = "Draft";
    public const string Active = "Active";
    public const string Disabled = "Disabled";
}

public static class UserStatuses
{
    public const string Active = "Active";
    public const string Disabled = "Disabled";
    public const string Invited = "Invited";
}

public static class SaleStatuses
{
    public const string PendingPayment = "PendingPayment";
    public const string Paid = "Paid";
    public const string Invoicing = "Invoicing";
    public const string PreparingForDispatch = "PreparingForDispatch";
    public const string LabelPrinted = "LabelPrinted";
    public const string Shipped = "Shipped";
    public const string Delivered = "Delivered";
    public const string InvoiceRejected = "InvoiceRejected";
    public const string Cancelled = "Cancelled";
    public const string Returning = "Returning";
    public const string Returned = "Returned";

    public static readonly IReadOnlyDictionary<string, string> Pt = new Dictionary<string, string>
    {
        [PendingPayment] = "Aguardando pagamento",
        [Paid] = "Pago",
        [Invoicing] = "Emitindo NF-e",
        [PreparingForDispatch] = "Preparando para envio",
        [LabelPrinted] = "Etiqueta impressa",
        [Shipped] = "Enviado",
        [Delivered] = "Entregue",
        [InvoiceRejected] = "NF-e rejeitada",
        [Cancelled] = "Cancelado",
        [Returning] = "Em devolução",
        [Returned] = "Devolvido"
    };
}

public static class InventoryMovementKinds
{
    public const string NfeInbound = "NfeInbound";
    public const string SalePaid = "SalePaid";
    public const string SalePaidReversal = "SalePaidReversal";
}

public static class LinkStatuses
{
    public const string PendingConnect = "PendingConnect";
    public const string Linked = "Linked";
}

public static class WorkKinds
{
    public const string NfeIngest = "nfe.ingest.requested";
    public const string NfeEmit = "nfe.emit.requested";
    public const string SaleImport = "sale.import";
    public const string StockPublish = "stock.publish.requested";
    public const string UploadInvoice = "marketplace.upload_invoice";
    public const string PublishListing = "listing.publish.requested";
    public const string ListingRefresh = "listing.refresh.requested";
    public const string ListingCancel = "listing.cancel.requested";
    public const string ListingImport = "listing.import.requested";
}

public static class ListingStatuses
{
    public const string Draft = "Draft";
    public const string Queued = "Queued";
    public const string Published = "Published";
    public const string Cancelled = "Cancelled";
    public const string Error = "Error";

    public static readonly IReadOnlyDictionary<string, string> Pt = new Dictionary<string, string>
    {
        [Draft] = "Rascunho",
        [Queued] = "Na fila",
        [Published] = "Publicado",
        ["PublishedDemo"] = "Publicado",
        [Cancelled] = "Cancelado",
        [Error] = "Erro"
    };
}

public static class AdvertisementKinds
{
    public const string Product = "Product";
    public const string Kit = "Kit";
}

public static class RemoteAdMatchStatuses
{
    public const string AlreadyLinked = "AlreadyLinked";
    public const string Suggested = "Suggested";
    public const string Unmatched = "Unmatched";
    public const string Linked = "Linked";
    public const string Ignored = "Ignored";

    public static readonly IReadOnlyDictionary<string, string> Pt = new Dictionary<string, string>
    {
        [AlreadyLinked] = "Já vinculado",
        [Suggested] = "SKU sugerido",
        [Unmatched] = "Pendente de vínculo",
        [Linked] = "Vinculado",
        [Ignored] = "Ignorado"
    };
}

public static class NfeDocumentKinds
{
    public const string Inbound = "Inbound";
    public const string Outbound = "Outbound";
}

public static class LoginLevels
{
    public const string Admin = "Admin";
    public const string Company = "Company";
    public const string Vendor = "Vendor";
}
