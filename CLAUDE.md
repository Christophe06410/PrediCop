# PrediCop — SaaS Application

Application SaaS pour la gestion des Polices Municipales.

## Architecture

```
PrediCop.sln
├── src/PrediCop.Core/          # Entités domaine, interfaces, DTOs, enums
├── src/PrediCop.Infrastructure/ # EF Core + SQL Server, repositories, services métier
├── src/PrediCop.Api/           # ASP.NET Core Web API REST + SignalR (.NET 10)
├── src/PrediCop.BackOffice/    # Razor Pages + Bootstrap 5 (.NET 10)
└── src/PrediCop.Mobile/        # .NET MAUI Android + iOS
```

## Dépendances entre projets

- **Infrastructure** → Core
- **Api** → Core, Infrastructure
- **BackOffice** → Core, Infrastructure
- **Mobile** → appelle Api via HttpClient (pas de référence directe)

## Modules fonctionnels

1. **Réception d'appels** (BackOffice) — opérateur saisit les détails d'un appel entrant
2. **Dispatch missions** (Api+BackOffice+Mobile) — propose les patrouilles les plus proches
3. **Gestion patrouilles GPS** (Mobile) — suivi temps réel, statut disponible/occupé
4. **Indice de risque des rues** (Api) — score croissant dans le temps, reset après patrouille
5. **Carte temps réel** (BackOffice + Mobile) — Leaflet.js avec positions véhicules et couleurs rues
6. **Dashboard manager** (BackOffice) — KPIs, stats par véhicule, missions refusées

## Entités principales (Core/Entities/)

- `Tenant` — multi-SaaS (un tenant = une PM)
- `User` — opérateurs, PM, managers, admins
- `PatrolVehicle` — voiture de patrouille avec GPS
- `VehicleOfficer` — quelle(s) personne(s) dans quelle voiture (plusieurs apps/voiture)
- `Call` — appel entrant avec adresse, description, tierces personnes
- `Mission` — intervention créée depuis un appel
- `MissionAssignment` — proposition à un véhicule (avec historique refus/acceptations)
- `Street` — rue avec score de risque
- `PatrolRecord` — passage d'une patrouille dans une rue
- `StreetRiskEvent` — événement ponctuel qui augmente le risque d'une rue

## Technologies clés

- **.NET 10**, EF Core 10, SQL Server
- **SignalR** pour les notifications temps réel (missions, GPS, risques)
- **JWT** authentification (API + Mobile), Cookie auth (BackOffice)
- **Leaflet.js** pour les cartes
- **Bootstrap 5.3** pour le back-office
- **MAUI** pour iOS et Android

## Conventions

- Namespace root: `PrediCop.*`
- Toujours `async/await` avec `CancellationToken`
- Multi-tenant: filtre global EF sur `!IsDeleted` pour `TenantEntity`
- Services métier injectés via interfaces (`IMissionService`, `IGpsService`, `IStreetRiskService`)
- BackOffice appelle l'API via `HttpClient` (pas accès direct DB)

## Connexions

- SQL Server: `Server=localhost;Database=PrediCop;Trusted_Connection=True;`
- API: `https://localhost:7001`
- BackOffice: `https://localhost:7002`
- JWT secret: dans `JwtSettings:SecretKey` (appsettings.json)
