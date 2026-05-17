using System.ComponentModel.DataAnnotations;

namespace PrediCop.BackOffice.Models;

public class UserDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public string Email { get; set; } = string.Empty;
    public string BadgeNumber { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class EditUserDto
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Le prénom est obligatoire.")]
    [Display(Name = "Prénom")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le nom est obligatoire.")]
    [Display(Name = "Nom")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "L'email est obligatoire.")]
    [EmailAddress(ErrorMessage = "Adresse email invalide.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le numéro de matricule est obligatoire.")]
    [Display(Name = "Numéro de matricule")]
    public string BadgeNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le rôle est obligatoire.")]
    [Display(Name = "Rôle")]
    public string Role { get; set; } = "Officer";

    [Display(Name = "Compte actif")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Mot de passe")]
    [MinLength(8, ErrorMessage = "Le mot de passe doit faire au moins 8 caractères.")]
    public string? Password { get; set; }

    [Display(Name = "Confirmer le mot de passe")]
    [Compare(nameof(Password), ErrorMessage = "Les mots de passe ne correspondent pas.")]
    public string? ConfirmPassword { get; set; }
}
