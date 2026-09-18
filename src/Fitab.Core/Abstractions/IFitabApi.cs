using Fitab.Core.Models;

namespace Fitab.Core.Abstractions;

/// <summary>
/// Accesso ai web service FITAB (cms.fitab.it).
/// <para>
/// Questa interfaccia e' il confine dell'anti-corruption layer: il backend non e'
/// nostro (e' di Evin srl, ASP.NET/IIS, non documentato e senza garanzia di
/// stabilita'). Tutto il resto dell'app parla solo questa interfaccia, cosi' se
/// gli endpoint cambiano — o se un giorno il backend viene rifatto — si riscrive
/// una sola implementazione.
/// </para>
/// </summary>
public interface IFitabApi
{
    Task<IReadOnlyList<News>> GetNewsAsync(int anno = 0, CancellationToken ct = default);

    Task<News?> GetNewsAsync(ContenutoId id, CancellationToken ct = default);

    Task<IReadOnlyList<Torneo>> GetCalendarioAsync(
        DateOnly? daData = null, Rilevanza? rilevanza = null, CancellationToken ct = default);

    Task<Torneo?> GetTorneoAsync(ContenutoId id, CancellationToken ct = default);

    Task<IReadOnlyList<Circolo>> GetCircoliAsync(CancellationToken ct = default);

    Task<Circolo?> GetCircoloAsync(int codice, CancellationToken ct = default);

    Task<IReadOnlyList<Arbitro>> GetArbitriAsync(int anno = 0, CancellationToken ct = default);

    /// <param name="daPosizione">Posizione di partenza (1-based): il backend pagina da qui.</param>
    Task<IReadOnlyList<VoceClassifica>> GetClassificaAsync(
        int tipoClassifica,
        int daPosizione = 1,
        int? codiceCircolo = null,
        bool escludiFederaliECircolo = false,
        CancellationToken ct = default);

    Task<MiaPosizione?> GetMiaPosizioneAsync(
        string tessera, string codiceFiscale, CancellationToken ct = default);

    Task<IReadOnlyList<Punteggio>> GetPunteggiAsync(
        string tessera, string codiceFiscale, int anno, CancellationToken ct = default);

    // --- Live ---------------------------------------------------------------

    /// <param name="tessera">
    /// Se valorizzata, il backend marca i turni e le coppie del tesserato
    /// (<c>SonoQui</c> / <c>SonoIo</c>). Senza tessera l'elenco e' in sola consultazione.
    /// </param>
    Task<IReadOnlyList<LiveTorneo>> GetLiveTorneiAsync(
        string? tessera = null, CancellationToken ct = default);

    Task<IReadOnlyList<LiveTurno>> GetLiveTurniAsync(
        string idTorneo, string? tessera = null, CancellationToken ct = default);

    Task<IReadOnlyList<LiveRiga>> GetLiveClassificaAsync(
        string idTorneo, string turno, string girone,
        string? tessera = null, CancellationToken ct = default);

    // --- Tesserato ----------------------------------------------------------

    Task<EsitoLogin> LoginAsync(
        string tessera, string codiceFiscale, CancellationToken ct = default);

    Task<ValiditaTessera?> GetValiditaAsync(
        string tessera, string codiceFiscale, CancellationToken ct = default);

    // --- Allegati -----------------------------------------------------------

    /// <summary>URL assoluto del PDF allegato a una news.</summary>
    string UrlAllegatoNews(string nomeFile);

    /// <summary>URL assoluto della locandina PDF di un torneo.</summary>
    string UrlLocandinaTorneo(string nomeFile);
}
