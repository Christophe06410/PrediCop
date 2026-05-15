namespace PrediCop.Core.Entities;

public class Tenant : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<User> Users { get; set; } = [];
    public ICollection<PatrolVehicle> Vehicles { get; set; } = [];
    public ICollection<Street> Streets { get; set; } = [];
}
