namespace Fitab.Api;

public sealed class FitabApiOptions
{
    /// <summary>
    /// Base dei web service. L'app originale usava <c>http://</c> per via di un problema
    /// del 2021 con la regola di URL rewrite su ComandiClass; verificato che oggi
    /// <c>https://</c> risponde correttamente, quindi partiamo cifrati.
    /// </summary>
    public Uri BaseUrl { get; set; } = new("https://cms.fitab.it/");

    /// <summary>Cartella dei PDF allegati alle news.</summary>
    public Uri UrlAllegatiNews { get; set; } = new("https://cms.fitab.it/documentiNews/");

    /// <summary>Cartella delle locandine PDF dei tornei.</summary>
    public Uri UrlAllegatiCalendario { get; set; } = new("https://www.fitab.it/documentiCalendario/");

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>Timeout piu' generoso per la classifica generale, che e' pesante.</summary>
    public TimeSpan TimeoutClassifica { get; set; } = TimeSpan.FromSeconds(60);
}
