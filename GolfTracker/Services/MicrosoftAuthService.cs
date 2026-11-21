
using Microsoft.Identity.Client;

namespace GolfTracker.Services;

public class MicrosoftAuthService
{
    private const string ClientId = "6df57443-1a07-469f-a9f3-6674fe35b711";

    private static readonly string[] Scopes =
    {
        "User.Read",
        "Files.ReadWrite"
    };

    private readonly IPublicClientApplication _app;

    public MicrosoftAuthService()
    {
        _app = PublicClientApplicationBuilder
            .Create(ClientId)
            .WithAuthority("https://login.microsoftonline.com/common")
            .Build();
    }

    public async Task<AuthenticationResult> SignInAsync()
    {
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
}
