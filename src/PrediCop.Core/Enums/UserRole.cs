namespace PrediCop.Core.Enums;

public enum UserRole
{
    Operator,
    Officer,
    Verbalisateur,  // ASVP — émet des PV uniquement, sans accès patrouille/missions
    Manager,
    Admin
}
