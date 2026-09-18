namespace Fitab.Data.Cache;

/// <summary>
/// Faccia non generica di <see cref="Cached{T}"/>: permette ai componenti di UI
/// che mostrano solo lo stato di freschezza (e non il dato) di non essere generici.
/// </summary>
public interface IStatoDati
{
    DateTimeOffset AggiornatoIl { get; }
    string? ErroreAggiornamento { get; }
    string EtaDescrizione { get; }
    bool DaCache { get; }
}

/// <summary>
/// Un valore con la sua provenienza. L'app mostra sempre qualcosa — se c'e' una
/// copia in cache la usa subito — e la UI sa dire da quando quel dato e' fermo
/// e se l'ultimo tentativo di aggiornamento e' fallito.
/// </summary>
/// <param name="Valore">Il dato.</param>
/// <param name="AggiornatoIl">Quando e' stato scaricato dal server.</param>
/// <param name="DaCache">Vero se proviene dal disco e non dalla rete.</param>
/// <param name="ErroreAggiornamento">
/// Valorizzato quando il refresh e' fallito ma avevamo una copia da mostrare.
/// </param>
public sealed record Cached<T>(
    T Valore,
    DateTimeOffset AggiornatoIl,
    bool DaCache,
    string? ErroreAggiornamento = null) : IStatoDati
{
    public TimeSpan Eta => DateTimeOffset.Now - AggiornatoIl;

    public bool Aggiornato => !DaCache && ErroreAggiornamento is null;

    /// <summary>Etichetta pronta per la UI: "adesso", "2 min fa", "ieri".</summary>
    public string EtaDescrizione
    {
        get
        {
            var e = Eta;
            if (e < TimeSpan.FromSeconds(10)) return "adesso";
            if (e < TimeSpan.FromMinutes(1)) return $"{(int)e.TotalSeconds} sec fa";
            if (e < TimeSpan.FromHours(1)) return $"{(int)e.TotalMinutes} min fa";
            if (e < TimeSpan.FromHours(24)) return $"{(int)e.TotalHours} ore fa";
            return e < TimeSpan.FromHours(48) ? "ieri" : $"{(int)e.TotalDays} giorni fa";
        }
    }
}
