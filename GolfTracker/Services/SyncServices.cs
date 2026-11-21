using GolfTracker.Data;

namespace GolfTracker.Services;

public class SyncService : IDisposable
{
    private readonly GolfDatabase _database;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public SyncService(GolfDatabase database)
    {
        _database = database;
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    private async void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.Internet)
        {
            await TriggerSyncAsync();
        }
    }

    public async Task TriggerSyncAsync()
    {
        if (!await _syncLock.WaitAsync(0))
            return;

        try
        {
            // TODO: Call your sync method on _database (e.g., await _database.SyncScoresAsync();)
            await Task.Delay(1000);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Sync failed: {ex.Message}");
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public void Dispose()
    {
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
        _syncLock.Dispose();
    }
}
