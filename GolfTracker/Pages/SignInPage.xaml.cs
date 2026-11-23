
using Microsoft.Maui.Storage;
using GolfTracker.Services;
using Microsoft.Identity.Client;

namespace GolfTracker.Pages;

// Handles user authentication via Microsoft Entra 
// (MSAL) or offline Guest Mode before navigating to 
// the main app.
public partial class SignIn : ContentPage
{

    // Track the current sign-in attempt.
    private int _signInAttempt = 0;

    // Guards against double-taps on the Microsoft 
    // Sign-In button while an auth prompt is active.
    private bool _isSigningIn = false;

    public SignIn()
    {
        InitializeComponent();
    }

    // Invalidates any in-flight sign-in 
    // attempts when the user navigates away from the page.
    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Ignore results from sign-ins abandoned on this page.
        _signInAttempt++;
    }

    // Initiates interactive Microsoft authentication, saves 
    // session state, and navigates to the welcome screen.
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

            // To this (static type call):
            var result = await MicrosoftAuthService.SignInAsync();

            // Ignore an outdated or abandoned sign-in.
            if (currentAttempt != _signInAttempt)
                return;

            // Prevent an older session check from overriding sign-in.
            if (Shell.Current is AppShell appShell)
            {
                appShell.CancelPendingSessionRestore();
            }

            // Persist authenticated mode so future app 
            // launches attempt silent session restoration
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
        // Handle MSAL-specific errors (e.g., user canceled login, 
        // network failure, or invalid tenant configuration)
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
        // Catch-all for unexpected errors, surfacing 
        // inner exception details when available
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

    // Bypasses Microsoft authentication and 
    // enters the app in offline Guest Mode.
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

        // Persist guest preference to skip automatic 
        // sign-in prompts on subsequent launches
        Preferences.Default.Set("GuestMode", true);

        await Shell.Current.GoToAsync("//WelcomePage");
    }
}
