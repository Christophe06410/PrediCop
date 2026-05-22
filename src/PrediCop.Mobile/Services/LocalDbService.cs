using SQLite;
using PrediCop.Mobile.Models;

namespace PrediCop.Mobile.Services;

public class LocalDbService
{
    private const string DbFileName = "predicop.db";

    private SQLiteAsyncConnection? _db;

    public async Task InitAsync()
    {
        if (_db is not null)
            return;

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, DbFileName);
        _db = new SQLiteAsyncConnection(dbPath);

        await _db.CreateTableAsync<CachedMission>();
        await _db.CreateTableAsync<PendingTrackingEntry>();
    }

    private SQLiteAsyncConnection Db =>
        _db ?? throw new InvalidOperationException("LocalDbService not initialized. Call InitAsync() first.");

    // ── CachedMission ────────────────────────────────────────────────────────

    public Task<CachedMission?> GetCachedMissionAsync(Guid missionId)
        => Db.Table<CachedMission>().Where(m => m.Id == missionId).FirstOrDefaultAsync();

    public Task UpsertCachedMissionAsync(CachedMission mission)
        => Db.InsertOrReplaceAsync(mission);

    public Task ClearCachedMissionsAsync()
        => Db.DeleteAllAsync<CachedMission>();

    // ── PendingTrackingEntry ─────────────────────────────────────────────────

    public Task AddPendingEntryAsync(PendingTrackingEntry entry)
        => Db.InsertAsync(entry);

    public Task<List<PendingTrackingEntry>> GetUnsyncedEntriesAsync()
        => Db.Table<PendingTrackingEntry>().Where(e => !e.IsSynced).ToListAsync();

    public async Task MarkEntrySyncedAsync(int localId)
    {
        var entry = await Db.Table<PendingTrackingEntry>()
                             .Where(e => e.LocalId == localId)
                             .FirstOrDefaultAsync();
        if (entry is null) return;
        entry.IsSynced = true;
        await Db.UpdateAsync(entry);
    }
}
