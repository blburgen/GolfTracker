
using GolfTracker.Services;

namespace GolfTracker;

public partial class AppShell : Shell
{
    private readonly MicrosoftAuthService _authService = new();

    private int _authVersion = 0;
    private Task _sessionRestoreTask = Task.CompletedTask;

    public AppShell()
    {
        InitializeComponent();

        Preferences.Default.Set("GuestMode", true);
    }

    public void CancelPendingSessionRestore()
    {
        _authVersion++;
    }

    // Allow other pages to wait for session restoration.
    public async Task WaitForSessionRestoreAsync()
    {
        await _sessionRestoreTask;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        int currentVersion = ++_authVersion;

        _sessionRestoreTask = RestoreSessionAsync(currentVersion);
    }

    private async Task RestoreSessionAsync(int currentVersion)
    {
        try
        {
            bool isSignedIn = await _authService.IsSignedInAsync();

            if (currentVersion != _authVersion)
                return;

            Preferences.Default.Set("GuestMode", !isSignedIn);
        }
        catch (Exception)
        {
            if (currentVersion == _authVersion)
            {
                Preferences.Default.Set("GuestMode", true);
            }
        }
    }
}
