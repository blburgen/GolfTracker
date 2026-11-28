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

    // Cancel abandoned Microsoft sign-in requests.
    private CancellationTokenSource? _signInCancellation;

    public SignIn()
    {
        InitializeComponent();
    }

    private void CancelCurrentSignIn()
    {
        // Invalidate any pending sign-in results.
        _signInAttempt++;

        // Cancel the Microsoft authentication request.
        _signInCancellation?.Cancel();

        // Allow a new sign-in attempt.
        _isSigningIn = false;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Cancel sign-in when leaving this page.
        CancelCurrentSignIn();
    }

    // Sign in with the previously selected Microsoft account.
    private async void OnMicrosoftSignInClicked(
        object? sender, EventArgs e)
    {
        await StartMicrosoftSignInAsync(false);
    }

    // Start sign-in with a different Microsoft account.
    private async void OnSwitchMicrosoftAccountClicked(
        object? sender, EventArgs e)
    {
        await StartMicrosoftSignInAsync(true);
    }

    private async Task StartMicrosoftSignInAsync(
        bool useDifferentAccount)
    {
        // Prevent multiple sign-ins at the same time.
        if (_isSigningIn)
            return;

        _isSigningIn = true;
        int currentAttempt = ++_signInAttempt;

        var cancellation = new CancellationTokenSource();
        _signInCancellation = cancellation;

        try
        {
            var result = await _authService.SignInAsync(
                cancellation.Token,
                useDifferentAccount);

            // Ignore an outdated or canceled sign-in.
            if (currentAttempt != _signInAttempt ||
                cancellation.IsCancellationRequested)
                return;

            // Save only a successfully accepted Microsoft account.
            _authService.SaveAccount(result);

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
            if (currentAttempt != _signInAttempt ||
                cancellation.IsCancellationRequested)
                return;

            await Shell.Current.GoToAsync("//WelcomePage");
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected when leaving the page.
        }
        catch (MsalException ex)
        {
            if (currentAttempt == _signInAttempt &&
                !cancellation.IsCancellationRequested)
            {
                await DisplayAlertAsync(
                    "Microsoft Sign In Error",
                    ex.Message,
                    "OK");
            }
        }
        catch (Exception ex)
        {
            if (currentAttempt == _signInAttempt &&
                !cancellation.IsCancellationRequested)
            {
                await DisplayAlertAsync(
                    "Error",
                    ex.Message,
                    "OK");
            }
        }
        finally
        {
            // Only clean up the current attempt.
            if (ReferenceEquals(_signInCancellation, cancellation))
            {
                _signInCancellation = null;
                _isSigningIn = false;
            }

            cancellation.Dispose();
        }
    }

    private async void OnContinueClicked(
        object? sender, EventArgs e)
    {
        // Cancel any pending Microsoft sign-in.
        CancelCurrentSignIn();

        // Prevent session restoration from overriding Guest Mode.
        if (Shell.Current is AppShell appShell)
        {
            appShell.CancelPendingSessionRestore();
        }

        Preferences.Default.Set("GuestMode", true);

        await Shell.Current.GoToAsync("//WelcomePage");
    }
}

