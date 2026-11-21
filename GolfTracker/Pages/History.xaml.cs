
namespace GolfTracker.Pages;

public partial class History : ContentPage
{
    public History()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Wait for Microsoft session restoration to finish.
        if (Shell.Current is AppShell appShell)
        {
            await appShell.WaitForSessionRestoreAsync();
        }

        // Check if the user is signed in.
        bool isGuest = Preferences.Default.Get("GuestMode", true);

        if (isGuest)
        {
            await DisplayAlertAsync(
                "Sign In Required",
                "Please sign in with Microsoft to view your game history.",
                "OK");

            await Shell.Current.GoToAsync("//SignIn");
        }
    }
}
