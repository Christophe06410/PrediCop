using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PoliceMunicipale.Core.Entities;
using PoliceMunicipale.Core.Interfaces;
using PoliceMunicipale.Infrastructure.Data;
using PoliceMunicipale.Infrastructure.Repositories;
using PoliceMunicipale.Infrastructure.Services;

namespace PoliceMunicipale.Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(config.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly("PoliceMunicipale.Infrastructure")));

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IGpsService, GpsService>();
        services.AddScoped<IStreetRiskService, StreetRiskService>();
        services.AddScoped<IMissionService, MissionService>();

        return services;
    }
}
