
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace GolfTracker.Services;


// Manages Microsoft Entra / Personal Microsoft Account authentication and 
// cross-platform MSAL token caching
public class MicrosoftAuthService
{
    private const string ClientId = "6df57443-1a07-469f-a9f3-6674fe35b711";

    // Permissions required to read user profile info and 
    // sync the database file to OneDrive.
    private static readonly string[] Scopes =
    {
        "User.Read",
        "Files.ReadWrite",
        "Files.ReadWrite.AppFolder"
    };

    private static IPublicClientApplication? _app;

    // Lazily builds and returns the singleton MSAL public client 
    // configured with platform-specific redirect URIs.
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

    // Ensures persistent token caching is initialized before 
    // returning the shared MSAL client.
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

    // Configures encrypted persistent MSAL token storage on Windows 
    // (iOS and Android handle caching natively).
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

    // Attempts silent authentication using a cached account, 
    // falling back to an interactive browser prompt if required.
    public static async Task<AuthenticationResult?> SignInAsync()
    {
        await _cacheInitialization.Value;

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
            // Silent token acquisition failed or no cached account exists; 
            // prompt user to sign in interactively
            var interactiveRequest = app.AcquireTokenInteractive(Scopes);

            // Attach the current Android Activity so the auth prompt renders over the app, 
            // or use the system browser on Windows
#if ANDROID
        interactiveRequest = interactiveRequest.WithParentActivityOrWindow(Platform.CurrentActivity);
#elif WINDOWS
            interactiveRequest = interactiveRequest.WithUseEmbeddedWebView(false);
#endif

            return await interactiveRequest.ExecuteAsync();
        }
    }

    // Check whether an existing Microsoft session can be restored.
    // Checks whether a cached Microsoft account exists and can 
    // silently refresh its access token.
    public async Task<bool> IsSignedInAsync()
    {
        // Load the saved cache before checking accounts.
        await _cacheInitialization.Value;

        var app = GetApp(); var accounts = await app.GetAccountsAsync();

        if (!accounts.Any())
        {
            return false;
        }

        try
        {
            await app
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
