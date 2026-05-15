using PoliceMunicipale.Core.Entities;

namespace PoliceMunicipale.Core.Interfaces;

public interface IStreetRiskService
{
    Task<int> CalculateCurrentRiskScoreAsync(Guid streetId, CancellationToken ct = default);
    Task RecalculateAllStreetRisksAsync(Guid tenantId, CancellationToken ct = default);
    Task RecordPatrolAsync(Guid streetId, Guid vehicleId, CancellationToken ct = default);
    Task<IEnumerable<Street>> GetStreetsOrderedByPriorityAsync(Guid tenantId, int count = 10, CancellationToken ct = default);
}
