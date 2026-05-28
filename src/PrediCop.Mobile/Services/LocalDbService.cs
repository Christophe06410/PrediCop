using SQLite;
using PrediCop.Mobile.Models;

namespace PrediCop.Mobile.Services;

public class LocalDbService
{
    private const string DbFileName = "predicop.db";

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _isInitialized;
    private SQLiteAsyncConnection? _db;

    public async Task InitAsync()
    {
        if (_isInitialized)
            return;

        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized)
                return;

            SQLitePCL.Batteries_V2.Init();

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, DbFileName);
            _db = new SQLiteAsyncConnection(dbPath);

            await _db.CreateTableAsync<CachedMission>();
            await _db.CreateTableAsync<PendingTrackingEntry>();

            _isInitialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private SQLiteAsyncConnection Db =>
        _db ?? throw new InvalidOperationException("LocalDbService not initialized. Call InitAsync() first.");

    // ── CachedMission ────────────────────────────────────────────────────────

    public async Task<CachedMission?> GetCachedMissionAsync(Guid missionId)
    {
        await InitAsync();
        return await Db.Table<CachedMission>().Where(m => m.Id == missionId).FirstOrDefaultAsync();
    }

    public async Task UpsertCachedMissionAsync(CachedMission mission)
    {
        await InitAsync();
        await Db.InsertOrReplaceAsync(mission);
    }

    public async Task ClearCachedMissionsAsync()
    {
        await InitAsync();
        await Db.DeleteAllAsync<CachedMission>();
    }

    // ── PendingTrackingEntry ─────────────────────────────────────────────────

    public async Task AddPendingEntryAsync(PendingTrackingEntry entry)
    {
        await InitAsync();
        await Db.InsertAsync(entry);
    }

    public async Task<List<PendingTrackingEntry>> GetUnsyncedEntriesAsync()
    {
        await InitAsync();
        return await Db.Table<PendingTrackingEntry>().Where(e => !e.IsSynced).ToListAsync();
    }

    public async Task MarkEntrySyncedAsync(int localId)
    {
        await InitAsync();

        var entry = await Db.Table<PendingTrackingEntry>()
                             .Where(e => e.LocalId == localId)
                             .FirstOrDefaultAsync();
        if (entry is null) return;
        entry.IsSynced = true;
        await Db.UpdateAsync(entry);
    }
}
