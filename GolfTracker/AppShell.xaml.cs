namespace GolfTracker;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		Preferences.Default.Set("GuestMode", true);
	}
}
