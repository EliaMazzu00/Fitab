using Fitab.Data.Cache;
using Microsoft.Extensions.DependencyInjection;

namespace Fitab.Data;

public static class ServiceCollectionExtensions
{
    /// <param name="cartellaCache">
    /// Cartella dove salvare i dati locali. Su MAUI si passa
    /// <c>FileSystem.AppDataDirectory</c>.
    /// </param>
    public static IServiceCollection AddFitabData(
        this IServiceCollection services, string cartellaCache)
    {
        services.AddSingleton<ICacheStore>(_ =>
            new FileCacheStore(Path.Combine(cartellaCache, "cache")));

        services.AddSingleton<FitabData>();
        return services;
    }
}
