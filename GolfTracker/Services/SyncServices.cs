using GolfTracker.Data;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;

namespace GolfTracker.Services;

public class SyncService : IDisposable
{
    private readonly GolfDatabase _database;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly string[] _scopes = { "Files.ReadWrite.AppFolder" };

    private string RemoteFileName => Path.GetFileName(_database.DbPath);

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

    public async Task<bool> TriggerSyncAsync()
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            return false;

        if (!await _syncLock.WaitAsync(0))
            return false;

        try
        {
            var graphClient = await GetAuthenticatedGraphClientAsync();
            if (graphClient == null) return false;

            // 1. Get the user's primary Drive first to retrieve its DriveId
            var drive = await graphClient.Me.Drive.GetAsync();
            if (drive?.Id == null) return false;

            // 2. Access Special["approot"] through Drives[drive.Id]
            var appFolder = await graphClient.Drives[drive.Id].Special["approot"].GetAsync();
            if (appFolder?.Id == null) return false;

            if (appFolder?.Id == null || appFolder.ParentReference?.DriveId == null)
                return false;

            DriveItem? remoteFile = null;
            try
            {
                remoteFile = await graphClient.Drives[appFolder.ParentReference.DriveId]
                    .Items[appFolder.Id]
                    .ItemWithPath(RemoteFileName)
                    .GetAsync();
            }
            catch (ODataError ex) when (ex.ResponseStatusCode == 404)
            {
                // File doesn't exist on OneDrive yet
            }

            // Check how many rounds exist locally before closing SQLite
            int roundCount = (await _database.GetRoundsAsync()).Count;

            // Close SQLite connection to flush WAL files and release the lock
            await _database.CloseConnectionAsync();

            try
            {
                bool hasLocalFile = File.Exists(_database.DbPath);
                bool hasCloudFile = remoteFile?.Id != null;

                if (!hasCloudFile && hasLocalFile && roundCount > 0)
                {
                    await UploadDatabaseAsync(graphClient, appFolder);
                }
                else if (hasCloudFile && (!hasLocalFile || roundCount == 0))
                {
                    await DownloadDatabaseAsync(graphClient, appFolder, remoteFile!.Id!);
                }
                else if (hasCloudFile && hasLocalFile && roundCount > 0)
                {
                    var localTime = File.GetLastWriteTimeUtc(_database.DbPath);
                    var cloudTime = remoteFile!.LastModifiedDateTime ?? DateTimeOffset.MinValue;

                    if (localTime > cloudTime.AddSeconds(5))
                        await UploadDatabaseAsync(graphClient, appFolder);
                    else if (cloudTime > localTime.AddSeconds(5))
                        await DownloadDatabaseAsync(graphClient, appFolder, remoteFile.Id!);
                }

            }
            finally
            {
                // Always reopen SQLite connection before releasing _syncLock
                await _database.Init();
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Sync failed: {ex.Message}");
            return false;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<bool> SignInAsync()
    {
        try { return (await MicrosoftAuthService.SignInAsync()) != null; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OneDrive sign-in failed: {ex.Message}");
            return false;
        }
    }

    private async Task UploadDatabaseAsync(GraphServiceClient client, DriveItem appFolder)
    {
        using var fileStream = File.OpenRead(_database.DbPath);
        await client.Drives[appFolder.ParentReference!.DriveId]
            .Items[appFolder.Id]
            .ItemWithPath(RemoteFileName)
            .Content
            .PutAsync(fileStream);
    }

    private async Task DownloadDatabaseAsync(GraphServiceClient client, DriveItem appFolder, string itemId)
    {
        using var stream = await client.Drives[appFolder.ParentReference!.DriveId]
            .Items[itemId]
            .Content
            .GetAsync();

        if (stream != null)
        {
            using var fileStream = File.Create(_database.DbPath);
            await stream.CopyToAsync(fileStream);
        }
    }

    private async Task<GraphServiceClient?> GetAuthenticatedGraphClientAsync()
    {
        var pca = await MicrosoftAuthService.GetSharedClientAsync();
        var firstAccount = (await pca.GetAccountsAsync()).FirstOrDefault();
        if (firstAccount == null) return null;

        try
        {
            var authResult = await pca.AcquireTokenSilent(_scopes, firstAccount).ExecuteAsync();
            return new GraphServiceClient(new BaseBearerTokenAuthenticationProvider(new StaticTokenProvider(authResult.AccessToken)));
        }
        catch (MsalUiRequiredException) { return null; }
    }

    public void Dispose()
    {
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
        _syncLock.Dispose();
    }
}

public class StaticTokenProvider : IAccessTokenProvider
{
    private readonly string _token;
    public StaticTokenProvider(string token) => _token = token;
    public AllowedHostsValidator AllowedHostsValidator => new();
    public Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
        => Task.FromResult(_token);
}
