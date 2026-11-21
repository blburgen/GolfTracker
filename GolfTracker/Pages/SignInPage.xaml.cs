
using Microsoft.Maui.Storage;
using GolfTracker.Services;
using Microsoft.Identity.Client;

namespace GolfTracker.Pages;

public partial class SignIn : ContentPage
{
    private readonly MicrosoftAuthService _authService = new();

    public SignIn()
    {
        InitializeComponent();
    }

    private async void OnMicrosoftSignInClicked(
        object? sender, EventArgs e)
    {
        try
        {
            var result = await _authService.SignInAsync();

            Preferences.Default.Set("GuestMode", false);

            await DisplayAlertAsync(
                "Signed In",
                $"Welcome, {result.Account.Username}!",
                "OK");

            await Shell.Current.GoToAsync("//WelcomePage");
        }
        catch (MsalException ex)
        {
            await DisplayAlertAsync(
                "Microsoft Sign In Error",
                ex.Message,
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(
                "Error",
                ex.Message,
                "OK");
        }
    }

    private async void OnContinueClicked(
        object? sender, EventArgs e)
    {
        Preferences.Default.Set("GuestMode", true);

        await Shell.Current.GoToAsync("//WelcomePage");
    }
}
