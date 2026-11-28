namespace GolfTracker.Pages;

public partial class History : ContentPage
{
    // Track whether the current History page visit is still active.
    private int _appearanceVersion = 0;

    public History()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        int currentVersion = ++_appearanceVersion;

        // Wait for Microsoft session restoration to finish.
        if (Shell.Current is AppShell appShell)
        {
            await appShell.WaitForSessionRestoreAsync();
        }

        // Stop if the user has left the History page.
        if (currentVersion != _appearanceVersion)
            return;

        // Check if the user is signed in.
        bool isGuest = Preferences.Default.Get("GuestMode", true);

        if (isGuest)
        {
            await DisplayAlertAsync(
                "Sign In Required",
                "Please sign in with Microsoft to view your game history.",
                "OK");

            // Don't redirect if the user left while the alert was open.
            if (currentVersion != _appearanceVersion)
                return;

            await Shell.Current.GoToAsync("//SignIn");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Invalidate any pending History navigation.
        _appearanceVersion++;
    }
}
