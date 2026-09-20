using Fitab.Core.Abstractions;
using Fitab.Core.Models;

namespace Fitab.App.Servizi;

/// <summary>Una riga di classifica live con il confronto rispetto al giro precedente.</summary>
/// <param name="Dati">La riga come arriva dal server.</param>
/// <param name="DeltaPosizione">
/// Posizioni guadagnate dall'ultimo aggiornamento: positivo = risalita.
/// Zero anche quando la coppia e' appena comparsa.
/// </param>
/// <param name="Nuova">La coppia non era presente nel giro precedente.</param>
/// <param name="Cambiata">
/// Posizione, VP o MP sono cambiati dall'ultimo giro: la riga viene evidenziata
/// per qualche secondo.
/// </param>
public sealed record RigaLive(LiveRiga Dati, int DeltaPosizione, bool Nuova, bool Cambiata = false)
{
    public bool InRisalita => DeltaPosizione > 0;
}

public sealed record EsitoLive(
    IReadOnlyList<RigaLive> Righe,
    DateTimeOffset Momento,
    string? Errore = null);

/// <summary>
/// Interroga periodicamente la classifica di un turno live.
/// <para>
/// E' il cuore della differenza rispetto all'app attuale, dove per vedere i
/// risultati di un tavolo appena caricato bisogna uscire dalla pagina e
/// rientrarci. Qui l'aggiornamento arriva da solo e viene applicato in modo
/// incrementale: chi ha cambiato posizione viene evidenziato, il resto della
/// pagina non si muove.
/// </para>
/// <para>
/// Segue un turno preciso, quello che l'utente ha aperto. Per la classifica
/// generale, che deve invece seguire l'ultimo turno concluso man mano che il
/// torneo avanza, c'e' <see cref="PollingGenerale"/>.
/// </para>
/// </summary>
public sealed class PollingLive(IFitabApi api) : PollingPrudente
{
    private Dictionary<string, LiveRiga> _precedenti = [];
    private bool _primoGiro = true;

    private string _idTorneo = "";
    private string _turno = "";
    private string _girone = "";
    private string? _tessera;

    public event Func<EsitoLive, Task>? Aggiornato;

    /// <summary>
    /// Imposta il turno da seguire. Azzera il confronto: i delta hanno senso
    /// solo all'interno dello stesso turno.
    /// </summary>
    public void Configura(string idTorneo, string turno, string girone, string? tessera)
    {
        _idTorneo = idTorneo;
        _turno = turno;
        _girone = girone;
        _tessera = tessera;
        _precedenti = [];
        _primoGiro = true;
        AzzeraErrori();
    }

    protected override async Task Scarica(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_idTorneo) || string.IsNullOrEmpty(_turno)) return;

        try
        {
            var righe = await api.GetLiveClassificaAsync(_idTorneo, _turno, _girone, _tessera, ct);
            Riuscito();

            var risultato = Confronta(righe);
            _precedenti = righe.GroupBy(r => r.ChiaveCoppia).ToDictionary(g => g.Key, g => g.First());
            _primoGiro = false;

            if (Aggiornato is not null)
                await Aggiornato.Invoke(new EsitoLive(risultato, DateTimeOffset.Now));
        }
        catch (OperationCanceledException)
        {
            // Uscita normale: la pagina e' stata chiusa.
        }
        catch (Exception ex)
        {
            Fallito();

            if (Aggiornato is not null)
            {
                var invariate = _precedenti.Values
                    .OrderBy(r => r.Posizione)
                    .Select(r => new RigaLive(r, 0, false))
                    .ToList();

                await Aggiornato.Invoke(
                    new EsitoLive(invariate, DateTimeOffset.Now, Descrivi(ex)));
            }
        }
    }

    private List<RigaLive> Confronta(IReadOnlyList<LiveRiga> righe)
    {
        var risultato = new List<RigaLive>(righe.Count);

        foreach (var riga in righe.OrderBy(r => r.Posizione))
        {
            if (_precedenti.TryGetValue(riga.ChiaveCoppia, out var prima))
            {
                var cambiata = prima.Posizione != riga.Posizione
                               || prima.VP != riga.VP
                               || prima.MP != riga.MP;

                risultato.Add(new RigaLive(
                    riga, prima.Posizione - riga.Posizione, Nuova: false, Cambiata: cambiata));
            }
            else
            {
                // Al primo giro non segnaliamo nulla come "nuovo": sarebbe tutto nuovo.
                risultato.Add(new RigaLive(riga, 0, Nuova: !_primoGiro));
            }
        }

        return risultato;
    }
}
