using Fitab.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Fitab.Api;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra il client dei web service FITAB.
    /// </summary>
    public static IServiceCollection AddFitabApi(
        this IServiceCollection services, Action<FitabApiOptions>? configura = null)
    {
        var options = new FitabApiOptions();
        configura?.Invoke(options);
        services.AddSingleton(options);

        services.AddHttpClient<IFitabApi, FitabApiClient>(http =>
        {
            http.BaseAddress = options.BaseUrl;

            // Il timeout effettivo lo gestisce il client per singola chiamata
            // (la classifica generale ha bisogno di piu' tempo delle altre):
            // qui teniamo un tetto massimo di sicurezza.
            http.Timeout = options.TimeoutClassifica + TimeSpan.FromSeconds(10);
            http.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        return services;
    }
}
