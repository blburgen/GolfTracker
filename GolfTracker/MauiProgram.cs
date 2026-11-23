using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using System.Reflection;
using GolfTracker.Data;
using GolfTracker.Pages;
using GolfTracker.Services;

namespace GolfTracker;

// Configures and builds the cross-platform .NET MAUI 
// application, including fonts, authentication, and 
// dependency injection services.
public static class MauiProgram
{
	// Initializes the MAUI app builder, loads 
	// environment variables, and registers core 
	// services and pages.
	public static MauiApp CreateMauiApp()
	{
		// Load secrets (such as CLIENT_ID) 
		// from the embedded .env file before configuring 
		// services
		LoadEnvironmentVariables();

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// 1. Register MSAL using the Client ID loaded from your .env file
		var clientId = Environment.GetEnvironmentVariable("CLIENT_ID")
					   ?? throw new InvalidOperationException("CLIENT_ID is missing from the .env file.");

		// Configure custom MSAL redirect URI and multi-tenant 
		// Microsoft Entra authority
		var pcaBuilder = PublicClientApplicationBuilder.Create(clientId)
			.WithRedirectUri($"msal{clientId}://auth")
			.WithAuthority("https://login.microsoftonline.com/common");

		// Configure shared Keychain access group 
		// required for MSAL token caching on iOS
#if IOS
		pcaBuilder = pcaBuilder.WithIosKeychainSecurityGroup("com.microsoft.adalcache");
#endif

		builder.Services.AddSingleton<IPublicClientApplication>(pcaBuilder.Build());

		// 2. Register the SQLite database service as a singleton
		builder.Services.AddSingleton<GolfDatabase>();

		// 3. Register SyncService right after GolfDatabase & MSAL
		builder.Services.AddSingleton<SyncService>();

		// Register pages as transient so each navigation gets 
		// a fresh UI instance with injected singleton services
		builder.Services.AddTransient<Scorecard>();
		builder.Services.AddTransient<History>();

		// Enable debug console logging during development builds
#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

	// Reads key-value pairs from the embedded .env 
	// resource and populates process environment variables.
	private static void LoadEnvironmentVariables()
	{
		var assembly = Assembly.GetExecutingAssembly();
		// Embedded resource names use double dots when the 
		// filename begins with a dot (.env)
		var resourceName = $"{assembly.GetName().Name}..env";

		using var stream = assembly.GetManifestResourceStream(resourceName);

		// Exit silently if no .env file was embedded in the build
		if (stream == null) return;

		using var reader = new StreamReader(stream);
		while (reader.ReadLine() is { } line)
		{
			var trimmed = line.Trim();

			// Skip empty lines and comments
			if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
				continue;

			var parts = trimmed.Split('=', 2);
			if (parts.Length == 2)
			{
				var key = parts.First().Trim();
				// Remove optional surrounding quotes from values
				var value = parts.Last().Trim().Trim('"', '\'');
				Environment.SetEnvironmentVariable(key, value);
			}
		}
	}
}
