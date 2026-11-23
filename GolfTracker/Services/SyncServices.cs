using GolfTracker.Data;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;

namespace GolfTracker.Services;

// Synchronizes the local SQLite database file with the 
// user's OneDrive AppFolder via Microsoft Graph.
public class SyncService : IDisposable
{
    private readonly GolfDatabase _database;

    // Prevents overlapping sync operations from running concurrently.
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    // Restricts OneDrive access strictly to this app's dedicated folder.
    private readonly string[] _scopes = { "Files.ReadWrite.AppFolder" };

    private string RemoteFileName => Path.GetFileName(_database.DbPath);

    // Initializes the sync service and subscribes to device connectivity 
    // changes for automatic background syncing.
    public SyncService(GolfDatabase database)
    {
        _database = database;
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    // Automatically triggers a database sync 
    // whenever internet access is restored.
    private async void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.Internet)
        {
            await TriggerSyncAsync();
        }
    }

    // Compares the local SQLite database against OneDrive and 
    // uploads or downloads the newer version.
    public async Task<bool> TriggerSyncAsync()
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            return false;

        // Skip immediately if another sync is already in progress
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

                // Case 1: First-time upload when local data exists 
                // but no backup is in OneDrive yet
                if (!hasCloudFile && hasLocalFile && roundCount > 0)
                {
                    await UploadDatabaseAsync(graphClient, appFolder);
                }
                // Case 2: Fresh install or empty local database—restore 
                // from existing OneDrive backup
                else if (hasCloudFile && (!hasLocalFile || roundCount == 0))
                {
                    await DownloadDatabaseAsync(graphClient, appFolder, remoteFile!.Id!);
                }
                // Case 3: Both exist—resolve by timestamp using a 5-second buffer 
                // to avoid clock-skew loops
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

    // Prompts the user to sign in interactively with 
    // Microsoft to enable OneDrive sync.
    public async Task<bool> SignInAsync()
    {
        try { return (await MicrosoftAuthService.SignInAsync()) != null; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OneDrive sign-in failed: {ex.Message}");
            return false;
        }
    }

    // Streams the local SQLite database file to the 
    // user's OneDrive AppFolder.
    private async Task UploadDatabaseAsync(GraphServiceClient client, DriveItem appFolder)
    {
        using var fileStream = File.OpenRead(_database.DbPath);
        await client.Drives[appFolder.ParentReference!.DriveId]
            .Items[appFolder.Id]
            .ItemWithPath(RemoteFileName)
            .Content
            .PutAsync(fileStream);
    }

    // Downloads the remote database file from OneDrive and 
    // overwrites the local SQLite file.
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

    // Silently acquires an MSAL access token for the cached account and 
    // builds an authenticated Graph client.
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

    // Unsubscribes from connectivity events and releases the sync semaphore.
    public void Dispose()
    {
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
        _syncLock.Dispose();
    }
}

// Provides a pre-acquired bearer access token to 
// Kiota's Microsoft Graph authentication pipeline.
public class StaticTokenProvider : IAccessTokenProvider
{
    private readonly string _token;
    public StaticTokenProvider(string token) => _token = token;
    public AllowedHostsValidator AllowedHostsValidator => new();
    public Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
        => Task.FromResult(_token);
}
