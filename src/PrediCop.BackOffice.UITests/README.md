# PrediCop BackOffice — Tests UI Playwright

Tests end-to-end pour le BackOffice PrediCop avec Playwright + xUnit.

## Prérequis

Avant de lancer les tests, s'assurer que les deux services tournent :

- **API** : `https://localhost:7229`
- **BackOffice** : `https://localhost:7218`

## Installation de Playwright (une seule fois)

Après avoir compilé le projet, installer les navigateurs Playwright :

```powershell
dotnet build src/PrediCop.BackOffice.UITests/
dotnet run --project src/PrediCop.BackOffice.UITests -- playwright install chromium
```

Ou directement via l'outil CLI Playwright :

```powershell
playwright install chromium
```

## Variables d'environnement (optionnelles)

| Variable | Valeur par défaut | Description |
|---|---|---|
| `PREDICOP_BASE_URL` | `https://localhost:7218` | URL du BackOffice |
| `PREDICOP_TEST_EMAIL` | `admin@predicop.local` | Email de l'utilisateur de test |
| `PREDICOP_TEST_PASSWORD` | `Admin1234!` | Mot de passe de l'utilisateur de test |
| `PREDICOP_TEST_CITY` | `test` | Slug de la ville (CitySlug) |

Exemple de configuration sous PowerShell :

```powershell
$env:PREDICOP_TEST_EMAIL = "monuser@maville.fr"
$env:PREDICOP_TEST_PASSWORD = "MonMotDePasse!"
$env:PREDICOP_TEST_CITY = "maville"
```

## Lancer les tests

```powershell
dotnet test src/PrediCop.BackOffice.UITests/
```

Avec filtre par catégorie :

```powershell
# Uniquement les tests d'authentification
dotnet test src/PrediCop.BackOffice.UITests/ --filter "FullyQualifiedName~AuthTests"

# Uniquement les tests Dashboard
dotnet test src/PrediCop.BackOffice.UITests/ --filter "FullyQualifiedName~DashboardTests"
```

## Mode visible (non headless)

Pour voir le navigateur s'exécuter, modifier `Headless = false` dans :

`src/PrediCop.BackOffice.UITests/Infrastructure/PlaywrightFixture.cs`

```csharp
Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
{
    Headless = false,   // <-- changer ici
    SlowMo = 100        // optionnel : ralentir pour mieux visualiser
});
```

## Structure des tests

```
Infrastructure/
  PlaywrightFixture.cs       # Fixture partagée (browser lifecycle)
  PlaywrightBaseTest.cs      # Classe de base abstraite (context, login helper)
  CollectionDefinition.cs    # Collection xUnit "Playwright"
Tests/
  AuthTests.cs               # Login, logout, protection des pages
  DashboardTests.cs          # KPI cards, titre, navbar
  CallsTests.cs              # Liste des appels, formulaire de réception
  MissionsTests.cs           # Liste des missions, carte Leaflet
  AdminModulesTests.cs       # Chargement des pages d'administration
  NavigationTests.cs         # Navbar, recherche, navigation entre pages
```
