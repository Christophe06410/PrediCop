using Microsoft.EntityFrameworkCore;
using PrediCop.Core.Entities;
using PrediCop.Core.Enums;

namespace PrediCop.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(AppDbContext context)
    {
        // --- Tenant + Admin (first run only) ---
        if (!await context.Tenants.AnyAsync())
        {
            var tenant = new Tenant { Name = "PrediCop", Slug = "predicop", IsActive = true };
            context.Tenants.Add(tenant);
            await context.SaveChangesAsync();

            context.Users.Add(new User
            {
                TenantId = tenant.Id,
                FirstName = "Admin",
                LastName = "PrediCop",
                Email = "admin@predicop.fr",
                BadgeNumber = "ADMIN-001",
                PasswordHash = global::BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                Role = UserRole.Admin,
                IsActive = true
            });
            await context.SaveChangesAsync();
        }

        var tenantId = (await context.Tenants.FirstAsync()).Id;

        // --- Officer + vehicle (idempotent) ---
        if (!await context.Users.AnyAsync(u => u.TenantId == tenantId && u.Role == UserRole.Officer))
        {
            var officer = new User
            {
                TenantId = tenantId,
                FirstName = "Jean",
                LastName = "Dupont",
                Email = "officier@predicop.fr",
                BadgeNumber = "OFR-001",
                PasswordHash = global::BCrypt.Net.BCrypt.HashPassword("Officer123!"),
                Role = UserRole.Officer,
                IsActive = true
            };
            context.Users.Add(officer);
            await context.SaveChangesAsync();

            var vehicle = new PatrolVehicle
            {
                TenantId = tenantId,
                CallSign = "VP-01",
                LicensePlate = "AB-123-CD",
                Status = VehicleStatus.Available
            };
            context.PatrolVehicles.Add(vehicle);
            await context.SaveChangesAsync();

            context.VehicleOfficers.Add(new VehicleOfficer
            {
                VehicleId = vehicle.Id,
                UserId = officer.Id,
                IsActive = true
            });
            await context.SaveChangesAsync();
        }

        // --- Streets for Toulouse (idempotent) ---
        if (!await context.Streets.AnyAsync(s => s.TenantId == tenantId))
        {
            var streets = new List<Street>
            {
                new() { TenantId = tenantId, Name = "Place du Capitole",        District = "Centre",       City = "Toulouse", BaseRiskScore = 80, CurrentRiskScore = 85, StartLatitude = 43.6047, StartLongitude = 1.4430, EndLatitude = 43.6060, EndLongitude = 1.4445 },
                new() { TenantId = tenantId, Name = "Rue de la République",     District = "Centre",       City = "Toulouse", BaseRiskScore = 60, CurrentRiskScore = 72, StartLatitude = 43.6030, StartLongitude = 1.4440, EndLatitude = 43.6060, EndLongitude = 1.4445 },
                new() { TenantId = tenantId, Name = "Allées Jean Jaurès",       District = "Centre",       City = "Toulouse", BaseRiskScore = 55, CurrentRiskScore = 60, StartLatitude = 43.6010, StartLongitude = 1.4490, EndLatitude = 43.6050, EndLongitude = 1.4520 },
                new() { TenantId = tenantId, Name = "Boulevard de Strasbourg",  District = "Saint-Aubin",  City = "Toulouse", BaseRiskScore = 30, CurrentRiskScore = 35, StartLatitude = 43.6080, StartLongitude = 1.4410, EndLatitude = 43.6120, EndLongitude = 1.4430 },
                new() { TenantId = tenantId, Name = "Rue Saint-Rome",           District = "Centre",       City = "Toulouse", BaseRiskScore = 45, CurrentRiskScore = 48, StartLatitude = 43.6020, StartLongitude = 1.4410, EndLatitude = 43.6050, EndLongitude = 1.4430 },
                new() { TenantId = tenantId, Name = "Boulevard Carnot",         District = "Arnaud-Bernard", City = "Toulouse", BaseRiskScore = 50, CurrentRiskScore = 55, StartLatitude = 43.6090, StartLongitude = 1.4460, EndLatitude = 43.6130, EndLongitude = 1.4490 },
                new() { TenantId = tenantId, Name = "Rue d'Alsace-Lorraine",   District = "Centre",       City = "Toulouse", BaseRiskScore = 65, CurrentRiskScore = 70, StartLatitude = 43.6000, StartLongitude = 1.4410, EndLatitude = 43.6040, EndLongitude = 1.4440 },
                new() { TenantId = tenantId, Name = "Avenue de la Gloire",     District = "Rangueil",     City = "Toulouse", BaseRiskScore = 15, CurrentRiskScore = 15, StartLatitude = 43.5760, StartLongitude = 1.4680, EndLatitude = 43.5800, EndLongitude = 1.4710 },
                new() { TenantId = tenantId, Name = "Rue Bayard",              District = "Centre",       City = "Toulouse", BaseRiskScore = 40, CurrentRiskScore = 42, StartLatitude = 43.6050, StartLongitude = 1.4470, EndLatitude = 43.6080, EndLongitude = 1.4500 },
                new() { TenantId = tenantId, Name = "Avenue Honoré Serres",    District = "Compans-Caffarelli", City = "Toulouse", BaseRiskScore = 20, CurrentRiskScore = 22, StartLatitude = 43.6120, StartLongitude = 1.4320, EndLatitude = 43.6150, EndLongitude = 1.4380 },
            };
            context.Streets.AddRange(streets);
            await context.SaveChangesAsync();
        }
    }
}
