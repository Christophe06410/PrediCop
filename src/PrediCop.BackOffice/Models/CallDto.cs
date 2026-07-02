using System.ComponentModel.DataAnnotations;

namespace PrediCop.BackOffice.Models;

public class CallDto
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = "Routine";
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
    public int? CallDurationSeconds { get; set; }
    public List<MissionDto> Missions { get; set; } = [];
    public List<ReportDto> Reports { get; set; } = [];
}

public class ReportDto
{
    public Guid Id { get; set; }
    public Guid CallId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public int Recipients { get; set; }
    public string[] RecipientLabels { get; set; } = [];
    public bool IsDraft { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class EditCallDto
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

    [Display(Name = "Tierces personnes impliquées")]
    public string? ThirdParties { get; set; }

    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    [Display(Name = "Notes internes")]
    public string? InternalNotes { get; set; }
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

    [Display(Name = "Priorité")]
    public string Priority { get; set; } = "Routine";

    /// <summary>Durée de l'appel en secondes, mesurée par le timer du navigateur (Décrocher → Raccrocher).</summary>
    public int? CallDurationSeconds { get; set; }
}
