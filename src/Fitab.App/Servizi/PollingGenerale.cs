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

    private string _chiaveUltimoTurno = "";
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
        _torneo = torneo;
        _girone = girone;
        _tessera = tessera;
        _chiaveUltimoTurno = "";
        _righePerTurno.Clear();
        _ultima = null;
        AzzeraErrori();
    }

    protected override async Task Scarica(CancellationToken ct)
    {
        if (_torneo is null) return;

        try
        {
            var turni = await dati.Api.GetLiveTurniAsync(_torneo.Codice, _tessera, ct);

            var sequenza = turni
                .Where(t => string.IsNullOrEmpty(_girone) || t.Girone == _girone)
                .InOrdineDiGioco()
                .ToList();

            if (sequenza.Count == 0)
            {
                // Torneo aperto ma senza risultati: non e' un errore, non c'e' ancora nulla.
                Riuscito();
                await Segnala(new EsitoGenerale(null, DateTimeOffset.Now, turni));
                return;
            }

            var ultimo = sequenza[^1];
            var penultimo = sequenza.Count > 1 ? sequenza[^2] : null;
            var turnoNuovo = _chiaveUltimoTurno.Length > 0 && _chiaveUltimoTurno != ultimo.Chiave;

            IReadOnlyList<LiveRiga>? righePrecedenti = null;
            if (penultimo is not null && !_righePerTurno.TryGetValue(penultimo.Chiave, out righePrecedenti))
            {
                righePrecedenti = await dati.Api.GetLiveClassificaAsync(
                    _torneo.Codice, penultimo.Codice, penultimo.Girone, _tessera, ct);
                _righePerTurno[penultimo.Chiave] = righePrecedenti;
            }

            var righe = await dati.Api.GetLiveClassificaAsync(
                _torneo.Codice, ultimo.Codice, ultimo.Girone, _tessera, ct);

            Riuscito();

            if (righe.Count == 0)
            {
                await Segnala(new EsitoGenerale(_ultima, DateTimeOffset.Now, turni));
                return;
            }

            _ultima = ClassificaGenerale.Calcola(_torneo, ultimo, righe, righePrecedenti);
            _chiaveUltimoTurno = ultimo.Chiave;

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

    private Task Segnala(EsitoGenerale esito) =>
        Aggiornato is null ? Task.CompletedTask : Aggiornato.Invoke(esito);
}
