
using Microsoft.Maui.Storage;
using GolfTracker.Services;
using Microsoft.Identity.Client;

namespace GolfTracker.Pages;



public partial class SignIn : ContentPage
{
    private readonly MicrosoftAuthService _authService = new();

    // Track the current sign-in attempt.
    private int _signInAttempt = 0;
    private bool _isSigningIn = false;

    public SignIn()
    {
        InitializeComponent();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Ignore results from sign-ins abandoned on this page.
        _signInAttempt++;
    }

    private async void OnMicrosoftSignInClicked(
        object? sender, EventArgs e)
    {
        // Prevent multiple sign-ins at the same time.
        if (_isSigningIn)
            return;

        _isSigningIn = true;
        int currentAttempt = ++_signInAttempt;

        try
        {
            var result = await _authService.SignInAsync();

            // Ignore an outdated or abandoned sign-in.
            if (currentAttempt != _signInAttempt)
                return;

            // Prevent an older session check from overriding sign-in.
            if (Shell.Current is AppShell appShell)
            {
                appShell.CancelPendingSessionRestore();
            }

            Preferences.Default.Set("GuestMode", false);

            await DisplayAlertAsync(
                "Signed In",
                $"Welcome, {result.Account.Username}!",
                "OK");

            // Only navigate if this is still the active attempt.
            if (currentAttempt != _signInAttempt)
                return;

            await Shell.Current.GoToAsync("//WelcomePage");
        }
        catch (MsalException ex)
        {
            if (currentAttempt == _signInAttempt)
            {
                await DisplayAlertAsync(
                    "Microsoft Sign In Error",
                    ex.Message,
                    "OK");
            }
        }
        catch (Exception ex)
        {
            if (currentAttempt == _signInAttempt)
            {
                var message = ex.InnerException != null
                    ? $"{ex.Message}\n\nDetails: {ex.InnerException.Message}"
                    : ex.Message;

                await DisplayAlertAsync("Error", message, "OK");
            }
        }
        finally
        {
            _isSigningIn = false;
        }
    }

    private async void OnContinueClicked(
        object? sender, EventArgs e)
    {
        // Invalidate any pending Microsoft sign-in.
        _signInAttempt++;

        // Prevent session restoration from overriding Guest Mode.
        if (Shell.Current is AppShell appShell)
        {
            appShell.CancelPendingSessionRestore();
        }

        Preferences.Default.Set("GuestMode", true);

        await Shell.Current.GoToAsync("//WelcomePage");
    }
}
