namespace PrediCop.Core.Entities;

public class UniformProfile : TenantEntity
{
    public Guid AgentId { get; set; }
    public User Agent { get; set; } = null!;

    public string? JacketSize { get; set; }
    public string? PantSize { get; set; }
    public string? ShirtSize { get; set; }
    public string? ShoeSize { get; set; }
    public string? HatSize { get; set; }
    public string? Notes { get; set; }
}
