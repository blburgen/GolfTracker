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
    private readonly IPublicClientApplication _pca;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly string[] _scopes = { "Files.ReadWrite.AppFolder" };

    private string RemoteFileName => Path.GetFileName(_database.DbPath);

    public SyncService(GolfDatabase database, IPublicClientApplication pca)
    {
        _database = database;
        _pca = pca;
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

            // Close SQLite connection to flush WAL files and release the lock
            await _database.CloseConnectionAsync();

            try
            {
                bool localExists = File.Exists(_database.DbPath);
                DateTimeOffset localLastWrite = localExists
                    ? File.GetLastWriteTimeUtc(_database.DbPath)
                    : DateTimeOffset.MinValue;

                if (remoteFile == null && localExists)
                {
                    await UploadDatabaseAsync(graphClient, appFolder);
                }
                else if (remoteFile != null && !localExists && remoteFile.Id != null)
                {
                    await DownloadDatabaseAsync(graphClient, appFolder, remoteFile.Id);
                }
                else if (remoteFile != null && localExists && remoteFile.Id != null)
                {
                    var remoteLastWrite = remoteFile.LastModifiedDateTime ?? DateTimeOffset.MinValue;

                    if (localLastWrite > remoteLastWrite.AddSeconds(5))
                    {
                        await UploadDatabaseAsync(graphClient, appFolder);
                    }
                    else if (remoteLastWrite > localLastWrite.AddSeconds(5))
                    {
                        await DownloadDatabaseAsync(graphClient, appFolder, remoteFile.Id);
                    }
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
        try
        {
            var builder = _pca.AcquireTokenInteractive(_scopes);

#if ANDROID
            builder = builder.WithParentActivityOrWindow(Platform.CurrentActivity);
#endif

            var result = await builder.ExecuteAsync();
            return result != null;
        }
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
        var accounts = await _pca.GetAccountsAsync();
        var firstAccount = accounts.FirstOrDefault();
        if (firstAccount == null) return null;

        try
        {
            var authResult = await _pca.AcquireTokenSilent(_scopes, firstAccount).ExecuteAsync();
            var tokenProvider = new StaticTokenProvider(authResult.AccessToken);
            var authProvider = new BaseBearerTokenAuthenticationProvider(tokenProvider);
            return new GraphServiceClient(authProvider);
        }
        catch (MsalUiRequiredException)
        {
            return null;
        }
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
