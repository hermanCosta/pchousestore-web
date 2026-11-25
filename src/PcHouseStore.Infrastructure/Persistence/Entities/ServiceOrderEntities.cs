namespace PcHouseStore.Infrastructure.Persistence.Entities;

public sealed class ServiceOrderRecord
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long OriginShopId { get; set; }
    public long ShopId { get; set; }
    public long DisplayNumber { get; set; }
    public long CustomerId { get; set; }
    public long DeviceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? ResponsibleEmployeeId { get; set; }
    public long CreatedByEmployeeId { get; set; }
    public long? RefurbishedUnitWarrantyId { get; set; }
    public string Status { get; set; } = "CREATED";
    public string? TechnicalOutcome { get; set; }
    public string CurrencyCode { get; set; } = "EUR";
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal Total { get; set; }
    public decimal BalanceDue { get; set; }
    public DateTime? PickedAt { get; set; }
    public int? BadSector { get; set; }
}
