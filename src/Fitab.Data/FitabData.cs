using System.Collections.Concurrent;
using Fitab.Core.Abstractions;
using Fitab.Core.Models;
using Fitab.Data.Cache;

namespace Fitab.Data;

/// <summary>
/// Accesso ai dati per la UI: cache-first con aggiornamento in background.
/// <para>
/// L'app originale era online-only: ogni schermata verificava la rete e, se assente,
/// mostrava un errore. E' la causa principale delle recensioni negative. Qui il
/// comportamento e' rovesciato: se una copia locale esiste viene mostrata subito,
/// l'aggiornamento parte in parallelo e la UI si ridisegna quando arriva. Nessuna
/// schermata bianca, nessun blocco, e l'app resta consultabile senza rete.
/// </para>
/// </summary>
public sealed class FitabData(IFitabApi api, ICacheStore cache)
{
    private readonly ConcurrentDictionary<string, Task> _aggiornamentiInCorso = new();

    // Durate di validita'. La classifica nazionale e' lunga di proposito: costa
    // ~24 secondi a pagina lato server e cambia solo quando
    // la federazione elabora i tornei.
    public static class Durate
    {
        public static readonly TimeSpan News = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan Calendario = TimeSpan.FromMinutes(15);

        /// <summary>Calendario esteso all'indietro: il passato non cambia piu'.</summary>
        public static readonly TimeSpan CalendarioStorico = TimeSpan.FromHours(6);

        public static readonly TimeSpan Dettaglio = TimeSpan.FromHours(24);
        public static readonly TimeSpan Circoli = TimeSpan.FromHours(24);
        public static readonly TimeSpan Arbitri = TimeSpan.FromHours(24);
        public static readonly TimeSpan ClassificaNazionale = TimeSpan.FromHours(24);
        public static readonly TimeSpan ClassificaCircolo = TimeSpan.FromHours(1);
        public static readonly TimeSpan Live = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Restituisce il dato applicando la politica cache-first.
    /// <list type="bullet">
    /// <item>cache presente e fresca: ritorna subito, nessuna chiamata di rete;</item>
    /// <item>cache presente ma scaduta: ritorna subito la copia vecchia e aggiorna
    /// in background, richiamando <paramref name="suAggiornamento"/> a dati nuovi;</item>
    /// <item>cache assente: attende la rete;</item>
    /// <item>rete fallita con cache disponibile: ritorna la copia vecchia segnalando
    /// l'errore, senza propagare l'eccezione.</item>
    /// </list>
    /// </summary>
    public async Task<Cached<T>> CaricaAsync<T>(
        string chiave,
        TimeSpan durata,
        Func<CancellationToken, Task<T>> scarica,
        Action<Cached<T>>? suAggiornamento = null,
        bool forzaAggiornamento = false,
        CancellationToken ct = default)
    {
        var inCache = forzaAggiornamento ? null : await cache.LeggiAsync<T>(chiave, ct);

        if (inCache is not null && inCache.Eta < durata)
            return inCache;

        if (inCache is not null)
        {
            // Copia vecchia disponibile: la mostriamo subito e aggiorniamo dietro.
            AvviaAggiornamento(chiave, scarica, suAggiornamento);
            return inCache;
        }

        try
        {
            var valore = await scarica(ct);
            await cache.ScriviAsync(chiave, valore, ct);
            return new Cached<T>(valore, DateTimeOffset.Now, DaCache: false);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Nessuna copia locale e rete fallita: qui l'errore va mostrato davvero,
            // ma proviamo un'ultima volta a recuperare qualcosa dal disco.
            var ultimaSpiaggia = await cache.LeggiAsync<T>(chiave, ct);
            if (ultimaSpiaggia is not null)
                return ultimaSpiaggia with { ErroreAggiornamento = Descrivi(ex) };

            throw;
        }
    }

    private void AvviaAggiornamento<T>(
        string chiave,
        Func<CancellationToken, Task<T>> scarica,
        Action<Cached<T>>? suAggiornamento)
    {
        // Un solo aggiornamento per chiave alla volta: se l'utente rimbalza tra le
        // schermate non moltiplichiamo le chiamate allo stesso endpoint.
        if (_aggiornamentiInCorso.ContainsKey(chiave)) return;

        var task = Task.Run(async () =>
        {
            try
            {
                // Volutamente scollegato dal token del chiamante: se l'utente cambia
                // pagina l'aggiornamento prosegue e la cache resta buona per dopo.
                var valore = await scarica(CancellationToken.None);
                await cache.ScriviAsync(chiave, valore, CancellationToken.None);
                suAggiornamento?.Invoke(new Cached<T>(valore, DateTimeOffset.Now, DaCache: false));
            }
            catch (Exception ex)
            {
                var vecchio = await cache.LeggiAsync<T>(chiave, CancellationToken.None);
                if (vecchio is not null)
                    suAggiornamento?.Invoke(vecchio with { ErroreAggiornamento = Descrivi(ex) });
            }
            finally
            {
                _aggiornamentiInCorso.TryRemove(chiave, out _);
            }
        });

        _aggiornamentiInCorso[chiave] = task;
    }

    private static string Descrivi(Exception ex) => ex switch
    {
        TaskCanceledException or TimeoutException => "Il server non ha risposto in tempo.",
        HttpRequestException => "Connessione non disponibile.",
        _ => "Aggiornamento non riuscito."
    };

    // --- News ---------------------------------------------------------------

    public Task<Cached<IReadOnlyList<News>>> NewsAsync(
        Action<Cached<IReadOnlyList<News>>>? suAggiornamento = null,
        bool forza = false, CancellationToken ct = default) =>
        CaricaAsync("news", Durate.News,
            c => api.GetNewsAsync(0, c), suAggiornamento, forza, ct);

    public Task<Cached<News?>> NewsAsync(
        ContenutoId id, Action<Cached<News?>>? suAggiornamento = null,
        bool forza = false, CancellationToken ct = default) =>
        CaricaAsync($"news:{id}", Durate.Dettaglio,
            c => api.GetNewsAsync(id, c), suAggiornamento, forza, ct);

    // --- Tornei -------------------------------------------------------------

    public Task<Cached<IReadOnlyList<Torneo>>> CalendarioAsync(
        DateOnly? daData = null,
        Action<Cached<IReadOnlyList<Torneo>>>? suAggiornamento = null,
        bool forza = false, CancellationToken ct = default) =>
        CaricaAsync($"calendario:{daData:yyyyMMdd}", Durate.Calendario,
            c => api.GetCalendarioAsync(daData, null, c), suAggiornamento, forza, ct);

    /// <summary>
    /// Calendario a partire da una data nel passato, tornei gia' giocati compresi.
    /// <para>
    /// Senza <c>DaData</c> il backend parte da oggi: per vedere lo storico di un
    /// circolo bisogna chiederglielo esplicitamente. Due anni e mezzo di calendario
    /// sono ~760 tornei per ~350 KB e arrivano in mezzo secondo — il filtro per
    /// circolo lo fa la UI, perche' l'endpoint non lo prevede.
    /// </para>
    /// </summary>
    public Task<Cached<IReadOnlyList<Torneo>>> CalendarioStoricoAsync(
        DateOnly daData,
        Action<Cached<IReadOnlyList<Torneo>>>? suAggiornamento = null,
        bool forza = false, CancellationToken ct = default) =>
        CaricaAsync($"calendario:storico:{daData:yyyyMMdd}", Durate.CalendarioStorico,
            c => api.GetCalendarioAsync(daData, null, c), suAggiornamento, forza, ct);

    public Task<Cached<Torneo?>> TorneoAsync(
        ContenutoId id, Action<Cached<Torneo?>>? suAggiornamento = null,
        bool forza = false, CancellationToken ct = default) =>
        CaricaAsync($"torneo:{id}", Durate.Dettaglio,
            c => api.GetTorneoAsync(id, c), suAggiornamento, forza, ct);

    // --- Circoli e arbitri --------------------------------------------------

    public Task<Cached<IReadOnlyList<Circolo>>> CircoliAsync(
        Action<Cached<IReadOnlyList<Circolo>>>? suAggiornamento = null,
        bool forza = false, CancellationToken ct = default) =>
        CaricaAsync("circoli", Durate.Circoli,
            c => api.GetCircoliAsync(c), suAggiornamento, forza, ct);

    public Task<Cached<Circolo?>> CircoloAsync(
        int codice, Action<Cached<Circolo?>>? suAggiornamento = null,
        bool forza = false, CancellationToken ct = default) =>
        CaricaAsync($"circolo:{codice}", Durate.Circoli,
            c => api.GetCircoloAsync(codice, c), suAggiornamento, forza, ct);

    public Task<Cached<IReadOnlyList<Arbitro>>> ArbitriAsync(
        Action<Cached<IReadOnlyList<Arbitro>>>? suAggiornamento = null,
        bool forza = false, CancellationToken ct = default) =>
        CaricaAsync("arbitri", Durate.Arbitri,
            c => api.GetArbitriAsync(0, c), suAggiornamento, forza, ct);

    // --- Classifiche --------------------------------------------------------

    /// <param name="codiceCircolo">
    /// Filtrando per circolo il backend risponde in meno di un secondo; senza filtro
    /// impiega ~24 secondi, da cui le due durate di cache diverse.
    /// </param>
    public Task<Cached<IReadOnlyList<VoceClassifica>>> ClassificaAsync(
        int tipoClassifica,
        int daPosizione = 1,
        int? codiceCircolo = null,
        Action<Cached<IReadOnlyList<VoceClassifica>>>? suAggiornamento = null,
        bool forza = false, CancellationToken ct = default) =>
        CaricaAsync(
            $"classifica:{tipoClassifica}:{codiceCircolo?.ToString() ?? "all"}:{daPosizione}",
            codiceCircolo is null ? Durate.ClassificaNazionale : Durate.ClassificaCircolo,
            c => api.GetClassificaAsync(tipoClassifica, daPosizione, codiceCircolo, false, c),
            suAggiornamento, forza, ct);

    /// <summary>
    /// Scarica in background le pagine successive della classifica nazionale.
    /// <para>
    /// Serve perche' ogni pagina costa ~24 secondi lato server: caricarle su
    /// richiesta renderebbe lo scorrimento inutilizzabile. Precaricandole
    /// mentre l'utente legge la prima, le successive si aprono all'istante.
    /// Le richieste sono sequenziali di proposito: non vogliamo aprire quattro
    /// query pesanti in parallelo su un server che non e' nostro.
    /// </para>
    /// </summary>
    public void PrecaricaClassifica(int tipoClassifica, int paginePreviste = 4)
    {
        var chiave = $"precarica:{tipoClassifica}";
        if (_aggiornamentiInCorso.ContainsKey(chiave)) return;

        var task = Task.Run(async () =>
        {
            try
            {
                for (var pagina = 1; pagina < paginePreviste; pagina++)
                {
                    var daPosizione = pagina * 50 + 1;
                    var chiavePagina = $"classifica:{tipoClassifica}:all:{daPosizione}";

                    var esistente = await cache.LeggiAsync<IReadOnlyList<VoceClassifica>>(
                        chiavePagina, CancellationToken.None);

                    if (esistente is not null && esistente.Eta < Durate.ClassificaNazionale)
                        continue;

                    var voci = await api.GetClassificaAsync(
                        tipoClassifica, daPosizione, null, false, CancellationToken.None);

                    await cache.ScriviAsync(chiavePagina, voci, CancellationToken.None);

                    // Fine della graduatoria: inutile insistere.
                    if (voci.Count == 0) break;
                }
            }
            catch (Exception)
            {
                // Il precaricamento e' un'ottimizzazione: se fallisce, le pagine
                // verranno scaricate su richiesta come al solito.
            }
            finally
            {
                _aggiornamentiInCorso.TryRemove(chiave, out _);
            }
        });

        _aggiornamentiInCorso[chiave] = task;
    }

    public Task<Cached<IReadOnlyList<Punteggio>>> PunteggiAsync(
        string tessera, string codiceFiscale, int anno,
        Action<Cached<IReadOnlyList<Punteggio>>>? suAggiornamento = null,
        bool forza = false, CancellationToken ct = default) =>
        CaricaAsync($"punteggi:{tessera}:{anno}", TimeSpan.FromHours(6),
            c => api.GetPunteggiAsync(tessera, codiceFiscale, anno, c), suAggiornamento, forza, ct);

    public Task<Cached<MiaPosizione?>> MiaPosizioneAsync(
        string tessera, string codiceFiscale,
        Action<Cached<MiaPosizione?>>? suAggiornamento = null,
        bool forza = false, CancellationToken ct = default) =>
        CaricaAsync($"posizione:{tessera}", TimeSpan.FromHours(6),
            c => api.GetMiaPosizioneAsync(tessera, codiceFiscale, c), suAggiornamento, forza, ct);

    // --- Live ---------------------------------------------------------------
    // Cache volutamente brevissima: serve solo a mostrare qualcosa all'istante
    // quando si rientra nella schermata. L'aggiornamento vero lo fa il polling.

    public Task<Cached<IReadOnlyList<LiveTorneo>>> LiveTorneiAsync(
        string? tessera = null,
        Action<Cached<IReadOnlyList<LiveTorneo>>>? suAggiornamento = null,
        bool forza = false, CancellationToken ct = default) =>
        CaricaAsync($"live:tornei:{tessera ?? "-"}", Durate.Live,
            c => api.GetLiveTorneiAsync(tessera, c), suAggiornamento, forza, ct);

    /// <summary>
    /// Fotografia dell'ultimo stato conosciuto di un torneo live, letta <b>solo dal
    /// disco</b>: non tocca la rete e torna null se non c'e' nulla. Serve al primo
    /// disegno della schermata, che cosi' non resta vuota ad aspettare il server.
    /// </summary>
    public Task<Cached<IstantaneaLive>?> IstantaneaLiveAsync(
        string idTorneo, string? tessera = null, CancellationToken ct = default) =>
        cache.LeggiAsync<IstantaneaLive>(ChiaveIstantanea(idTorneo, tessera), ct);

    /// <summary>Aggiorna la fotografia dopo un giro di polling riuscito.</summary>
    public Task SalvaIstantaneaLiveAsync(
        string idTorneo, string? tessera, IstantaneaLive istantanea, CancellationToken ct = default) =>
        cache.ScriviAsync(ChiaveIstantanea(idTorneo, tessera), istantanea, ct);

    private static string ChiaveIstantanea(string idTorneo, string? tessera) =>
        $"live:istantanea:{idTorneo}:{tessera ?? "-"}";

    /// <summary>Accesso diretto all'API, senza cache: usato dal polling della schermata live.</summary>
    public IFitabApi Api => api;
}
