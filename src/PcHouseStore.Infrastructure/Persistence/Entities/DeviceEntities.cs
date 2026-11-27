namespace PcHouseStore.Infrastructure.Persistence.Entities;

public sealed class DeviceRecord
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
