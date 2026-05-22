using PrediCop.Core.Enums;

namespace PrediCop.Core.Entities;

public class EquipmentCatalog : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public EquipmentCategory Category { get; set; }
    public string? Description { get; set; }
    public string Unit { get; set; } = "pièce";
    public int? DefaultLifespanMonths { get; set; }
    public string? ReferenceCode { get; set; }
    public bool IsActive { get; set; } = true;
}
