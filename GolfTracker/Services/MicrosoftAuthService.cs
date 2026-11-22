
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace GolfTracker.Services;

public class MicrosoftAuthService
{
    private const string ClientId = "6df57443-1a07-469f-a9f3-6674fe35b711";

    private static readonly string[] Scopes =
    {
        "User.Read",
        "Files.ReadWrite"
    };

    private static IPublicClientApplication? _app;

    private static IPublicClientApplication GetApp()
    {
        if (_app == null)
        {
            _app = PublicClientApplicationBuilder
                .Create(ClientId)
                .WithAuthority(AzureCloudInstance.AzurePublic, AadAuthorityAudience.AzureAdAndPersonalMicrosoftAccount)
                .WithRedirectUri("http://localhost")
                .Build();
        }
        return _app;
    }

    // Initialize the Windows token cache only once.
    private static readonly Lazy<Task> _cacheInitialization =
        new(InitializeCacheAsync);

    public MicrosoftAuthService()
    {
    }

    private static async Task InitializeCacheAsync()
    {
#if WINDOWS
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var cacheDirectory = Path.Combine(baseDir, "GolfTracker", "MicrosoftAuth");
        Directory.CreateDirectory(cacheDirectory);

        var storageProperties =
            new StorageCreationPropertiesBuilder(
                "GolfTracker.msalcache.bin",
                cacheDirectory)
            .Build();

        var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
        cacheHelper.RegisterCache(GetApp().UserTokenCache);
#endif
    }

    public async Task<AuthenticationResult> SignInAsync()
    {
        // Ensure the persistent cache is ready first.
        await _cacheInitialization.Value;

        var accounts = await _app.GetAccountsAsync();

        try
        {
            return await _app
                .AcquireTokenSilent(Scopes, accounts.FirstOrDefault())
                .ExecuteAsync();
        }
        catch (MsalUiRequiredException)
        {
            return await _app
                .AcquireTokenInteractive(Scopes)
#if WINDOWS
                .WithUseEmbeddedWebView(false) // Uses system browser on Windows
#endif
            .ExecuteAsync();
        }
    }

    // Check whether an existing Microsoft session can be restored.
    public async Task<bool> IsSignedInAsync()
    {
        // Load the saved cache before checking accounts.
        await _cacheInitialization.Value;

        var accounts = await _app.GetAccountsAsync();

        if (!accounts.Any())
        {
            return false;
        }

        try
        {
            await _app
                .AcquireTokenSilent(Scopes, accounts.First())
                .ExecuteAsync();

            return true;
        }
        catch (MsalUiRequiredException)
        {
            return false;
        }
    }
}
