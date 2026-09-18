using Fitab.Api;
using Fitab.App.Servizi;
using Fitab.Data;
using Microsoft.Extensions.Logging;

namespace Fitab.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();

		// Web service FITAB (cms.fitab.it). Vedi API-FITAB.md per la mappa
		// degli endpoint e i tempi di risposta misurati.
		builder.Services.AddFitabApi();

		// Cache locale: l'app deve restare consultabile senza rete.
		builder.Services.AddFitabData(FileSystem.AppDataDirectory);

		// Servizi di piattaforma e sessione del tesserato.
		builder.Services.AddSingleton<IArchivioSicuro, ArchivioSicuro>();
		builder.Services.AddSingleton<AperturaEsterna>();
		builder.Services.AddSingleton<SessioneUtente>();
		builder.Services.AddSingleton<ServizioTema>();
		builder.Services.AddSingleton<Precaricamento>();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
