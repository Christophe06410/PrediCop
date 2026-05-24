namespace PrediCop.Core.Enums;

public enum UserRole
{
    Operator     = 0,
    Officer      = 1,
    Verbalisateur = 2, // ASVP — émet des PV uniquement, sans accès patrouille/missions
    Manager      = 3,
    Admin        = 4,
    PatrolLeader = 5,  // Chef de patrouille — menu étendu, active la patrouille
    PatrolAgent  = 6   // Agent patrouilleur — connexion simple, géoloc individuelle
}
