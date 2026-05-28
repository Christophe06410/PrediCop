@echo off
echo ============================================================
echo  PrediCop — Tests automatiques
echo ============================================================
echo.

echo [1/2] Tests unitaires BackOffice (sans serveur requis)...
echo --------------------------------------------------------
dotnet test src/PrediCop.BackOffice.Tests/ --logger "console;verbosity=normal"
echo.

echo [2/2] Tests UI Playwright (necessite API + BackOffice en cours)
echo --------------------------------------------------------
echo    API      : https://localhost:7229
echo    BackOffice: https://localhost:7218
echo    Compte    : definir PREDICOP_TEST_EMAIL, PREDICOP_TEST_PASSWORD, PREDICOP_TEST_CITY
echo    (ou editer PlaywrightBaseTest.cs pour les valeurs par defaut)
echo.
set /p RUNUI="Lancer les tests UI ? (O/N) : "
if /i "%RUNUI%"=="O" (
    dotnet test src/PrediCop.BackOffice.UITests/ --logger "console;verbosity=normal"
)

echo.
echo ============================================================
echo  Termine.
echo ============================================================
pause
