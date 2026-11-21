using Microsoft.Extensions.Logging;
using System.Reflection;
using dotenv.net;
using GolfTracker.Data;
using GolfTracker.Pages;
using GolfTracker.Services;

namespace GolfTracker;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		LoadEnvironmentVariables();

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Register the SQLite database service as a singleton
		builder.Services.AddSingleton<GolfDatabase>();

		// Register SyncService right after GolfDatabase
		builder.Services.AddSingleton<SyncService>();

		// Register your pages here so GolfDatabase can be injected into them
		// Example:
		// builder.Services.AddSingleton<MainPage>();

		// Register pages that inject GolfDatabase
		builder.Services.AddTransient<Scorecard>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

	private static void LoadEnvironmentVariables()
	{
		var assembly = Assembly.GetExecutingAssembly();
		var resourceName = $"{assembly.GetName().Name}..env";

		using var stream = assembly.GetManifestResourceStream(resourceName);
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
