
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace GolfTracker.Services;

public class MicrosoftAuthService
{
    private const string ClientId = "6df57443-1a07-469f-a9f3-6674fe35b711";

    private static readonly string[] Scopes =
    {
        "User.Read",
        "Files.ReadWrite",
        "Files.ReadWrite.AppFolder"
    };

    private static IPublicClientApplication? _app;

    private static IPublicClientApplication GetApp()
    {
        if (_app == null)
        {
            var builder = PublicClientApplicationBuilder
                .Create(ClientId)
                .WithAuthority(AzureCloudInstance.AzurePublic, AadAuthorityAudience.AzureAdAndPersonalMicrosoftAccount);

#if ANDROID || IOS
        builder = builder.WithRedirectUri($"msal{ClientId}://auth");
#else
            builder = builder.WithRedirectUri("http://localhost");
#endif

            _app = builder.Build();
        }
        return _app;
    }

    public static async Task<IPublicClientApplication> GetSharedClientAsync()
    {
        await _cacheInitialization.Value;
        return GetApp();
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

    public static async Task<AuthenticationResult?> SignInAsync()
    {
        await InitializeCacheAsync();

        var app = GetApp();
        var accounts = await app.GetAccountsAsync();
        var firstAccount = accounts.FirstOrDefault();

        try
        {
            return await app
                .AcquireTokenSilent(Scopes, firstAccount)
                .ExecuteAsync();
        }
        catch (MsalUiRequiredException)
        {
            // ---> PART 2 OF STEP 1 GOES HERE <---
            var interactiveRequest = app.AcquireTokenInteractive(Scopes);

#if ANDROID
        interactiveRequest = interactiveRequest.WithParentActivityOrWindow(Platform.CurrentActivity);
#elif WINDOWS
            interactiveRequest = interactiveRequest.WithUseEmbeddedWebView(false);
#endif

            return await interactiveRequest.ExecuteAsync();
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
