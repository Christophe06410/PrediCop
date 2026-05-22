using System.ComponentModel.DataAnnotations;

namespace PrediCop.BackOffice.Models;

public class VehicleDto
{
    public Guid Id { get; set; }
    public string CallSign { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double? LastLatitude { get; set; }
    public double? LastLongitude { get; set; }
    public DateTime? LastPositionUpdate { get; set; }
    public List<string> OfficerNames { get; set; } = [];
    public string? BeaconUuid { get; set; }
    public Guid? AssignedGeoZoneId { get; set; }
}

public class EditVehicleDto
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "L'indicatif radio est obligatoire.")]
    [Display(Name = "Indicatif radio (Call Sign)")]
    public string CallSign { get; set; } = string.Empty;

    [Required(ErrorMessage = "La plaque d'immatriculation est obligatoire.")]
    [Display(Name = "Immatriculation")]
    public string LicensePlate { get; set; } = string.Empty;

    [Display(Name = "Statut")]
    public string Status { get; set; } = "Offline";

    [Display(Name = "UUID Beacon BLE")]
    public string? BeaconUuid { get; set; }

    [Display(Name = "Zone de patrouille assignée")]
    public Guid? AssignedGeoZoneId { get; set; }
}
