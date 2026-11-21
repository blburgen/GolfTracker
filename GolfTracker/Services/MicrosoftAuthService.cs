
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

    private static readonly IPublicClientApplication _app =
        PublicClientApplicationBuilder
            .Create(ClientId)
            .WithAuthority("https://login.microsoftonline.com/common")
            .Build();

    // Initialize the Windows token cache only once.
    private static readonly Lazy<Task> _cacheInitialization =
        new(InitializeCacheAsync);

    public MicrosoftAuthService()
    {
    }

    private static async Task InitializeCacheAsync()
    {
#if WINDOWS
        var cacheDirectory = Path.Combine(
            FileSystem.AppDataDirectory,
            "MicrosoftAuth");

        Directory.CreateDirectory(cacheDirectory);

        var storageProperties =
            new StorageCreationPropertiesBuilder(
                "GolfTracker.msalcache.bin",
                cacheDirectory)
            .Build();

        var cacheHelper = await MsalCacheHelper.CreateAsync(
            storageProperties);

        cacheHelper.RegisterCache(_app.UserTokenCache);
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
                .AcquireTokenWithDeviceCode(Scopes, deviceCodeResult =>
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await Shell.Current.DisplayAlertAsync(
                            "Microsoft Sign In",
                            deviceCodeResult.Message,
                            "OK");
                    });

                    return Task.CompletedTask;
                })
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
