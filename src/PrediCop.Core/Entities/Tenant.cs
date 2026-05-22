using PrediCop.Core.Enums;

namespace PrediCop.Core.Entities;

public class Tenant : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // Subscription
    public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.None;
    public SubscriptionPlan SubscriptionPlan { get; set; } = SubscriptionPlan.None;
    public SubscriptionPeriod SubscriptionPeriod { get; set; } = SubscriptionPeriod.Monthly;
    public int VehicleLimit { get; set; } = 5;
    public int UserLimit { get; set; } = 5;
    public DateTime? TrialEndsAt { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }

    // Stripe
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public string? StripeCheckoutSessionId { get; set; }

    // Géofencing & RGPD
    public bool GeofencingEnabled { get; set; } = false;
    public string? DpoEmail { get; set; } // email DPO pour les demandes RGPD

    // ---- Modules optionnels (activables par tenant) ----
    public bool ModuleRhEnabled { get; set; } = false;
    public bool ModuleFourriereEnabled { get; set; } = false;
    public bool ModuleFleetEnabled { get; set; } = false;
    public bool ModuleLogisticsEnabled { get; set; } = false;
    public bool ModuleVerbalisationEnabled { get; set; } = false;

    // ---- Champs sensibles (RGPD Art. 9 et Art. 10) ----
    public bool AgentBloodTypeEnabled { get; set; } = false;      // données de santé — requiert consentement explicite
    public bool AgentEmergencyContactEnabled { get; set; } = true; // contacts d'urgence
    public bool GpsTrackingEnabled { get; set; } = true;           // géolocalisation temps réel
    public bool PhotoAttachmentsEnabled { get; set; } = true;      // photos sur PV et fourrière

    // ---- Rétention des données (jours, 0 = pas de purge automatique) ----
    public int GpsDataRetentionDays { get; set; } = 30;            // CNIL recommande 30 jours
    public int AuditLogRetentionDays { get; set; } = 365;

    public ICollection<User> Users { get; set; } = [];
    public ICollection<PatrolVehicle> Vehicles { get; set; } = [];
    public ICollection<Street> Streets { get; set; } = [];
}
