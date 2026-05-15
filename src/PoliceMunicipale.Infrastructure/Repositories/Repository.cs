using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PoliceMunicipale.Core.Entities;
using PoliceMunicipale.Core.Interfaces;
using PoliceMunicipale.Infrastructure.Data;

namespace PoliceMunicipale.Infrastructure.Repositories;

public class Repository<T>(AppDbContext context) : IRepository<T> where T : BaseEntity
{
    private readonly DbSet<T> _set = context.Set<T>();

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _set.FindAsync([id], ct);

    public async Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default)
        => await _set.ToListAsync(ct);

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await _set.Where(predicate).ToListAsync(ct);

    public async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        await _set.AddAsync(entity, ct);
        return entity;
    }

    public Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        _set.Update(entity);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetByIdAsync(id, ct);
        if (entity is not null)
        {
            entity.IsDeleted = true;
            entity.UpdatedAt = DateTime.UtcNow;
            _set.Update(entity);
        }
    }

    public IQueryable<T> Query() => _set.AsQueryable();
}
