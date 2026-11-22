using Microsoft.Extensions.DependencyInjection;
using GolfTracker.Services;

namespace GolfTracker;

public partial class App : Application
{
	private readonly SyncService _syncService;

	public App(SyncService syncService)
	{
		InitializeComponent();
		_syncService = syncService;

		// Run an initial background sync check on startup
		_ = _syncService.TriggerSyncAsync();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
	protected override void OnResume()
	{
		base.OnResume();
		_ = _syncService.TriggerSyncAsync();
	}
}
