
using Microsoft.Maui.Storage;

namespace GolfTracker.Pages;



public partial class SignIn : ContentPage
{
    string clientID = Environment.GetEnvironmentVariable("CLIENT_ID") ?? throw new InvalidOperationException("CLIENT_ID is missing from .env");
    public SignIn()
    {
        InitializeComponent();
    }

    // Microsoft / OneDrive sign-in
    private async void OnMicrosoftSignInClicked(
        object sender, EventArgs e)
    {
        await DisplayAlertAsync(
            "Microsoft Sign In",
            "Microsoft authentication is not configured yet.",
            "OK");
    }

    // Continue using GolfTracker without signing in
    private async void OnContinueClicked(
        object sender, EventArgs e)
    {
        // Save guest mode preference
        Preferences.Default.Set("GuestMode", true);

        // Navigate to the Welcome page
        await Shell.Current.GoToAsync("//WelcomePage");
    }
}
