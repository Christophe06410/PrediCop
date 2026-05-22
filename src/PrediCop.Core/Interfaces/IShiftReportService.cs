using PrediCop.Core.DTOs;

namespace PrediCop.Core.Interfaces;

public interface IShiftReportService
{
    Task<ShiftReportResponse> GenerateAsync(CreateShiftReportRequest request, Guid tenantId, CancellationToken ct);
    Task<ShiftReportResponse?> GetAsync(Guid id, Guid tenantId, CancellationToken ct);
    Task<(List<ShiftReportResponse> Items, int Total)> GetListAsync(Guid tenantId, Guid? vehicleId, DateTime? dateFrom, DateTime? dateTo, int page, int pageSize, CancellationToken ct);
    Task SignAsync(Guid id, Guid tenantId, CancellationToken ct);
}
