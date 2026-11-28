using GolfTracker.Services;

namespace GolfTracker;

public partial class AppShell : Shell
{
    private readonly MicrosoftAuthService _authService = new();

    private int _authVersion = 0;
    private Task _sessionRestoreTask = Task.CompletedTask;

    // Notify waiting pages when restoration finishes or is canceled.
    private TaskCompletionSource? _sessionRestoreCompletion;

    // Track whether session restoration has already started.
    private bool _sessionRestoreStarted = false;

    public AppShell()
    {
        InitializeComponent();

        // Only set Guest Mode on the first launch.
        if (!Preferences.Default.ContainsKey("GuestMode"))
        {
            Preferences.Default.Set("GuestMode", true);
        }
    }

    public void CancelPendingSessionRestore()
    {
        // Invalidate any previous session restoration.
        _authVersion++;

        // Release pages already waiting for restoration.
        _sessionRestoreCompletion?.TrySetResult();
        _sessionRestoreCompletion = null;

        // New callers no longer need to wait.
        _sessionRestoreTask = Task.CompletedTask;
    }

    // Allow other pages to wait for session restoration.
    public async Task WaitForSessionRestoreAsync()
    {
        await _sessionRestoreTask;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Only start automatic restoration once per app launch.
        if (_sessionRestoreStarted)
            return;

        _sessionRestoreStarted = true;

        // Respect an explicit Guest Mode choice.
        if (Preferences.Default.Get("GuestMode", true))
        {
            _sessionRestoreTask = Task.CompletedTask;
            return;
        }

        int currentVersion = ++_authVersion;

        // Create a completion signal for waiting pages.
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        _sessionRestoreCompletion = completion;
        _sessionRestoreTask = completion.Task;

        // Start restoration without blocking the UI.
        _ = RestoreSessionAsync(currentVersion, completion);
    }

    private async Task RestoreSessionAsync(
        int currentVersion,
        TaskCompletionSource completion)
    {
        try
        {
            bool isSignedIn = await _authService.IsSignedInAsync();

            // Ignore outdated restoration results.
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
        finally
        {
            // Release waiting pages when restoration finishes.
            completion.TrySetResult();

            // Only clear the current restoration signal.
            if (ReferenceEquals(_sessionRestoreCompletion, completion))
            {
                _sessionRestoreCompletion = null;
            }
        }
    }
}

