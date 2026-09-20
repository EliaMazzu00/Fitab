namespace Fitab.Data.Cache;

/// <summary>
/// Archivio locale dei dati scaricati.
/// <para>
/// L'implementazione attuale scrive file JSON (<see cref="FileCacheStore"/>): i volumi
/// in gioco sono piccoli — 77 circoli, 309 arbitri, ~100 tornei, le prime pagine di
/// classifica — e un archivio a file non aggiunge dipendenze native, il che aiuta
/// trimming, AOT e tempo di avvio su Android, che e' il rischio che stiamo tenendo
/// sotto controllo. Se i volumi cresceranno (ricerca full-text sull'intera
/// graduatoria federale) si sostituisce l'implementazione con SQLite senza toccare
/// il resto dell'app.
/// </para>
/// </summary>
public interface ICacheStore
{
    Task<Cached<T>?> LeggiAsync<T>(string chiave, CancellationToken ct = default);

    Task ScriviAsync<T>(string chiave, T valore, CancellationToken ct = default);
}
