using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace GolfTracker.Services;

public class MicrosoftAuthService
{
    private const string ClientId = "6df57443-1a07-469f-a9f3-6674fe35b711";

    // Remember which Microsoft account was selected.
    private const string AccountIdKey = "MicrosoftAccountId";

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

    // Find the previously selected Microsoft account.
    private static async Task<IAccount?> GetSavedAccountAsync()
    {
        string savedAccountId = Preferences.Default.Get(
            AccountIdKey, string.Empty);

        if (string.IsNullOrEmpty(savedAccountId))
            return null;

        var accounts = await _app.GetAccountsAsync();

        return accounts.FirstOrDefault(account =>
            account.HomeAccountId.Identifier == savedAccountId);
    }

    // Save an account only after the Sign In page accepts it.
    public void SaveAccount(AuthenticationResult result)
    {
        Preferences.Default.Set(
            AccountIdKey,
            result.Account.HomeAccountId.Identifier);
    }

    public async Task<AuthenticationResult> SignInAsync(
        CancellationToken cancellationToken = default,
        bool useDifferentAccount = false)
    {
        // Ensure the persistent cache is ready first.
        await _cacheInitialization.Value;
        cancellationToken.ThrowIfCancellationRequested();

        // Find the previously selected account.
        var savedAccount = await GetSavedAccountAsync();
        cancellationToken.ThrowIfCancellationRequested();

        // Skip silent sign-in when switching Microsoft accounts.
        if (!useDifferentAccount && savedAccount != null)
        {
            try
            {
                var silentResult = await _app
                    .AcquireTokenSilent(Scopes, savedAccount)
                    .ExecuteAsync(cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                // The Sign In page will decide whether to save this account.
                return silentResult;
            }
            catch (MsalUiRequiredException)
            {
                // Continue to device-code sign-in.
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        var result = await _app
            .AcquireTokenWithDeviceCode(Scopes, async deviceCodeResult =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    await Shell.Current.DisplayAlertAsync(
                        "Microsoft Sign In",
                        deviceCodeResult.Message,
                        "OK");
                });
            })
            .ExecuteAsync(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        // Return the account without saving it yet.
        return result;
    }

    // Check whether the saved Microsoft session can be restored.
    public async Task<bool> IsSignedInAsync()
    {
        await _cacheInitialization.Value;

        var savedAccount = await GetSavedAccountAsync();

        // Never automatically select a different account.
        if (savedAccount == null)
            return false;

        try
        {
            await _app
                .AcquireTokenSilent(Scopes, savedAccount)
                .ExecuteAsync();

            return true;
        }
        catch (MsalUiRequiredException)
        {
            return false;
        }
    }

    // Clear the selected account if needed.
    public void ClearSelectedAccount()
    {
        Preferences.Default.Remove(AccountIdKey);
    }
}


