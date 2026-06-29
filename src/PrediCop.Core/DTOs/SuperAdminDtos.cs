namespace PrediCop.Core.DTOs;

public class TenantAdminDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string SubscriptionStatus { get; set; } = string.Empty;
    public string SubscriptionPlan { get; set; } = string.Empty;
    public int VehicleLimit { get; set; }
    public int UserLimit { get; set; }
    public int UserCount { get; set; }
    public int VehicleCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? TrialEndsAt { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public string? StripeCustomerId { get; set; }
}

public class CreateTenantRequest
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string SubscriptionPlan { get; set; } = "Essential";
    public string SubscriptionStatus { get; set; } = "Active";
    public int VehicleLimit { get; set; } = 5;
    public int UserLimit { get; set; } = 10;
    public string AdminEmail { get; set; } = string.Empty;
    public string AdminFirstName { get; set; } = string.Empty;
    public string AdminLastName { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
}

public class UpdateTenantRequest
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string SubscriptionPlan { get; set; } = string.Empty;
    public string SubscriptionStatus { get; set; } = string.Empty;
    public int VehicleLimit { get; set; }
    public int UserLimit { get; set; }
}
