using Fitab.Core.Models;
using Fitab.Data;

namespace Fitab.App.Servizi;

/// <param name="Classifica">Null finche' il torneo non ha pubblicato nemmeno un turno.</param>
/// <param name="Turni">
/// Elenco dei turni cosi' come sta sul server adesso: la pagina ci disegna i chip,
/// senza doverlo chiedere per conto suo.
/// </param>
/// <param name="TurnoAppenaChiuso">
/// Vero sul giro in cui compare un turno nuovo: e' il momento in cui la classifica
/// fa un salto e la UI puo' evidenziare chi e' salito e chi e' sceso.
/// </param>
public sealed record EsitoGenerale(
    ClassificaGenerale? Classifica,
    DateTimeOffset Momento,
    IReadOnlyList<LiveTurno>? Turni = null,
    bool TurnoAppenaChiuso = false,
    string? Errore = null);

/// <summary>
/// Tiene aggiornata la classifica generale di un torneo live.
/// <para>
/// A differenza di <see cref="PollingLive"/> non segue un turno fisso: a ogni giro
/// ricontrolla quale sia l'ultimo turno pubblicato e, quando l'arbitro ne chiude uno
/// nuovo, la classifica avanza da sola.
/// </para>
/// <para>
/// Sul costo: a regime sono due chiamate per giro, l'elenco turni e la classifica
/// dell'ultimo. Il turno precedente serve solo a ricostruire il risultato di tappa e
/// non cambia piu' una volta pubblicato, quindi lo si scarica una volta sola e lo si
/// tiene da parte.
/// </para>
/// <para>
/// Nota su cosa questo polling non puo' fare: il backend non espone mai un turno a
/// meta'. I turni compaiono gia' completi — verificato su un nazionale da 106 coppie
/// seguito per sei ore, con i turni 5, 6 e 7 apparsi interi — perche' il gestionale
/// dell'arbitro spedisce il turno quando lo chiude, non tavolo per tavolo. Una
/// classifica che si muove a ogni tavolo che finisce non e' ottenibile da qui: quello
/// che si puo' fare, e che <see cref="ClassificaGenerale"/> fa, e' mostrare gli
/// abbinamenti del turno in corso e gli abbinamenti che ne derivano.
/// </para>
/// <para>
/// Dopo ogni giro riuscito salva su disco una <see cref="IstantaneaLive"/>: al
/// rientro nella schermata la classifica c'e' subito, senza aspettare la rete.
/// </para>
/// </summary>
public sealed class PollingGenerale(FitabData dati) : PollingPrudente
{
    private LiveTorneo? _torneo;
    private string _girone = "";
    private string? _tessera;

    private LiveTurno? _ultimoTurno;
    private ClassificaGenerale? _ultima;

    // Righe dei turni gia' scaricati. Un turno chiuso non cambia piu', quindi quello
    // precedente si scarica una volta sola; l'ultimo si ricarica sempre, perche' un
    // arbitro puo' correggere un punteggio appena inserito.
    private readonly Dictionary<string, IReadOnlyList<LiveRiga>> _righePerTurno = [];

    public event Func<EsitoGenerale, Task>? Aggiornato;

    /// <summary>L'ultima classifica calcolata, per ridisegnare senza aspettare un giro.</summary>
    public ClassificaGenerale? Ultima => _ultima;

    /// <param name="girone">
    /// Vuoto per il girone unico. I tornei a piu' gironi sono competizioni separate:
    /// le classifiche non si uniscono, se ne segue uno per volta.
    /// </param>
    public void Configura(LiveTorneo torneo, string girone = "", string? tessera = null)
    {
        // Riconfigurare con gli stessi parametri capita a ogni rientro nella vista
        // generale dai chip dei turni. Buttare via le righe gia' scaricate
        // costringerebbe il giro successivo a rifare tutto da capo: due chiamate
        // in piu' ogni volta che si tocca "Generale".
        if (_torneo?.Codice == torneo.Codice && _girone == girone && _tessera == tessera)
        {
            AzzeraErrori();
            return;
        }

        _torneo = torneo;
        _girone = girone;
        _tessera = tessera;
        _ultimoTurno = null;
        _righePerTurno.Clear();
        _ultima = null;
        AzzeraErrori();
    }

    /// <summary>
    /// Riparte dalla fotografia salvata su disco invece che da zero.
    /// <para>
    /// Le righe che contiene sono quelle degli ultimi due turni al momento in cui
    /// e' stata presa, e un turno chiuso non cambia piu'. Senza questo, il primo
    /// giro dopo ogni ingresso riscaricava una classifica che avevamo gia' letto
    /// dal disco un istante prima.
    /// </para>
    /// </summary>
    public void Riparti(IstantaneaLive istantanea)
    {
        if (_torneo is null || _torneo.Codice != istantanea.Torneo.Codice) return;

        // Con piu' gironi la fotografia parla di uno solo: se non e' quello che
        // stiamo seguendo, le sue righe sono di un'altra competizione.
        if (!string.IsNullOrEmpty(_girone) && istantanea.Ultimo.Girone != _girone) return;

        _righePerTurno[istantanea.Ultimo.Chiave] = istantanea.Righe;

        // La fotografia non dice a quale turno appartengano le righe precedenti:
        // si ricava dalla sequenza dei turni salvata insieme a lei.
        if (istantanea.Precedenti is { Count: > 0 } precedenti)
        {
            var sequenza = Sequenza(istantanea.Turni);
            var posizione = sequenza.FindIndex(t => t.Chiave == istantanea.Ultimo.Chiave);
            if (posizione > 0) _righePerTurno[sequenza[posizione - 1].Chiave] = precedenti;
        }

        _ultimoTurno = istantanea.Ultimo;
    }

    protected override async Task Scarica(CancellationToken ct)
    {
        if (_torneo is null) return;

        // Il turno che risulta ultimo dal giro precedente. La sua classifica parte
        // insieme all'elenco turni, senza aspettare di sapere se e' ancora lui:
        // nove giri su dieci lo e', e cosi' un aggiornamento costa un viaggio
        // invece di due. Quando invece l'arbitro ne ha appena chiuso uno nuovo,
        // quella che arriva e' la classifica del turno precedente, che serve
        // comunque ai risultati di tappa. In nessuno dei due casi e' sprecata.
        var ipotesi = _ultimoTurno;

        try
        {
            var attesaTurni = dati.Api.GetLiveTurniAsync(_torneo.Codice, _tessera, ct);

            var attesaIpotesi = ipotesi is null
                ? null
                : Osservata(dati.Api.GetLiveClassificaAsync(
                    _torneo.Codice, ipotesi.Codice, ipotesi.Girone, _tessera, ct));

            var turni = await attesaTurni;

            var sequenza = Sequenza(turni);

            if (sequenza.Count == 0)
            {
                // Torneo aperto ma senza risultati: non e' un errore, non c'e' ancora nulla.
                Riuscito();
                await Segnala(new EsitoGenerale(null, DateTimeOffset.Now, turni));
                return;
            }

            var ultimo = sequenza[^1];
            var penultimo = sequenza.Count > 1 ? sequenza[^2] : null;
            var turnoNuovo = ipotesi is not null && ipotesi.Chiave != ultimo.Chiave;

            IReadOnlyList<LiveRiga>? righeIpotesi = null;
            if (attesaIpotesi is not null)
            {
                try
                {
                    righeIpotesi = await attesaIpotesi;
                    _righePerTurno[ipotesi!.Chiave] = righeIpotesi;
                }
                catch (Exception) when (!ct.IsCancellationRequested)
                {
                    // Era una richiesta lanciata a scatola chiusa: se e' andata
                    // male non e' un giro fallito, si rifa' qui sotto dove serve.
                }
            }

            // L'ultimo turno si richiede sempre, anche se ce l'abbiamo gia': un
            // arbitro puo' correggere un punteggio appena inserito. L'unica copia
            // che vale e' quella appena arrivata in questo giro — non quella della
            // fotografia su disco, che e' vecchia di quanto lo e' la fotografia.
            var attesaUltimo = ipotesi?.Chiave == ultimo.Chiave && righeIpotesi is not null
                ? Task.FromResult(righeIpotesi)
                : Osservata(dati.Api.GetLiveClassificaAsync(
                    _torneo.Codice, ultimo.Codice, ultimo.Girone, _tessera, ct));

            // Un turno chiuso non cambia piu': il precedente si scarica una volta
            // sola. Quando serve parte insieme all'ultimo invece che dopo: a freddo
            // e' l'unico punto in cui servono davvero due classifiche, e sono due
            // richieste leggere.
            var attesaPenultimo = penultimo is null || _righePerTurno.ContainsKey(penultimo.Chiave)
                ? null
                : Osservata(dati.Api.GetLiveClassificaAsync(
                    _torneo.Codice, penultimo.Codice, penultimo.Girone, _tessera, ct));

            var righe = await attesaUltimo;

            if (attesaPenultimo is not null)
                _righePerTurno[penultimo!.Chiave] = await attesaPenultimo;

            var righePrecedenti = penultimo is not null
                && _righePerTurno.TryGetValue(penultimo.Chiave, out var prima) ? prima : null;

            Riuscito();

            if (righe.Count == 0)
            {
                await Segnala(new EsitoGenerale(_ultima, DateTimeOffset.Now, turni));
                return;
            }

            _ultima = ClassificaGenerale.Calcola(_torneo, ultimo, righe, righePrecedenti);
            _ultimoTurno = ultimo;

            // Bastano gli ultimi due turni: il resto e' storia che non riguardiamo.
            _righePerTurno[ultimo.Chiave] = righe;
            foreach (var vecchia in _righePerTurno.Keys
                         .Where(k => k != ultimo.Chiave && k != penultimo?.Chiave).ToList())
                _righePerTurno.Remove(vecchia);

            // La fotografia per il prossimo ingresso. Se il salvataggio fallisce
            // (disco pieno, permessi) non e' un motivo per rovinare il giro: si
            // perde solo la partenza istantanea la volta dopo.
            try
            {
                await dati.SalvaIstantaneaLiveAsync(_torneo.Codice, _tessera,
                    new IstantaneaLive(_torneo, turni, ultimo, righe, righePrecedenti), ct);
            }
            catch (Exception)
            {
                // ignorata di proposito
            }

            await Segnala(new EsitoGenerale(_ultima, DateTimeOffset.Now, turni, turnoNuovo));
        }
        catch (OperationCanceledException)
        {
            // Uscita normale: la pagina e' stata chiusa.
        }
        catch (Exception ex)
        {
            Fallito();
            await Segnala(new EsitoGenerale(_ultima, DateTimeOffset.Now, Errore: Descrivi(ex)));
        }
    }

    /// <summary>Turni del girone seguito, nell'ordine in cui sono stati giocati.</summary>
    private List<LiveTurno> Sequenza(IEnumerable<LiveTurno> turni) =>
        turni.Where(t => string.IsNullOrEmpty(_girone) || t.Girone == _girone)
             .InOrdineDiGioco()
             .ToList();

    /// <summary>
    /// Marca la richiesta come osservata. Le classifiche partono prima di sapere
    /// se serviranno: se il giro si interrompe su un'altra di esse, l'errore di
    /// quella rimasta indietro non deve restare orfano.
    /// </summary>
    private static Task<T> Osservata<T>(Task<T> richiesta)
    {
        _ = richiesta.ContinueWith(static t => _ = t.Exception,
                                   TaskContinuationOptions.OnlyOnFaulted);
        return richiesta;
    }

    private Task Segnala(EsitoGenerale esito) =>
        Aggiornato is null ? Task.CompletedTask : Aggiornato.Invoke(esito);
}
