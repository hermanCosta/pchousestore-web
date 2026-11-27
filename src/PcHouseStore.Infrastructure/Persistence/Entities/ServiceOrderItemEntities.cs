namespace PcHouseStore.Infrastructure.Persistence.Entities;

public sealed class ServiceOrderItemRecord
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long ServiceOrderId { get; set; }
    public long CatalogItemId { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public string NameSnapshot { get; set; } = string.Empty;
    public string? DescriptionSnapshot { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxableAmount { get; set; }
    public string? TaxCodeSnapshot { get; set; }
    public decimal? TaxRateSnapshot { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public DateTime? RemovedAt { get; set; }
}
