using System.ComponentModel.DataAnnotations;

namespace PrediCop.BackOffice.Models;

public class CallDto
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CallerName { get; set; } = string.Empty;
    public string CallerPhone { get; set; } = string.Empty;
    public string IncidentCategory { get; set; } = string.Empty;
    public string IncidentDescription { get; set; } = string.Empty;
    public string IncidentAddress { get; set; } = string.Empty;
    public string? IncidentAddressComplement { get; set; }
    public double? IncidentLatitude { get; set; }
    public double? IncidentLongitude { get; set; }
    public string? ThirdParties { get; set; }
    public string? InternalNotes { get; set; }
    public string? Notes { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public List<MissionDto> Missions { get; set; } = [];
}

public class CreateCallDto
{
    [Required(ErrorMessage = "Le nom de l'appelant est obligatoire.")]
    [Display(Name = "Nom de l'appelant")]
    public string CallerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le téléphone est obligatoire.")]
    [Phone(ErrorMessage = "Numéro de téléphone invalide.")]
    [Display(Name = "Téléphone")]
    public string CallerPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "La catégorie est obligatoire.")]
    [Display(Name = "Catégorie d'incident")]
    public string IncidentCategory { get; set; } = string.Empty;

    [Required(ErrorMessage = "La description est obligatoire.")]
    [Display(Name = "Description de l'incident")]
    public string IncidentDescription { get; set; } = string.Empty;

    [Required(ErrorMessage = "L'adresse est obligatoire.")]
    [Display(Name = "Adresse de l'incident")]
    public string IncidentAddress { get; set; } = string.Empty;

    [Display(Name = "Complément d'adresse")]
    public string? IncidentAddressComplement { get; set; }

    [Display(Name = "Latitude")]
    public double? IncidentLatitude { get; set; }

    [Display(Name = "Longitude")]
    public double? IncidentLongitude { get; set; }

    [Display(Name = "Tierces personnes impliquées")]
    public string? ThirdParties { get; set; }

    [Display(Name = "Notes internes")]
    public string? InternalNotes { get; set; }
}
