namespace PrediCop.Core.Enums;

public enum UserRole
{
    Operator,
    Officer,
    PatrolLeader,   // Chef de patrouille — menu étendu, active la patrouille
    PatrolAgent,    // Agent patrouilleur — connexion simple, géoloc individuelle
    Verbalisateur,  // ASVP — émet des PV uniquement, sans accès patrouille/missions
    Manager,
    Admin
}
