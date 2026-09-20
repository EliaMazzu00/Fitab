namespace Fitab.App.Servizi;

/// <summary>
/// Ciclo di interrogazione periodica condiviso dalle schermate live.
/// <para>
/// Il server e' di terzi, quindi il comportamento e' volutamente prudente: si
/// interroga solo quello che l'utente sta guardando, ci si sospende quando l'app
/// va in background e si rallenta progressivamente dopo una serie di errori
/// invece di martellare un server che evidentemente non sta rispondendo.
/// </para>
/// </summary>
public abstract class PollingPrudente : IAsyncDisposable
{
    private CancellationTokenSource? _cts;
    private int _erroriConsecutivi;

    /// <summary>Cadenza di una serata in corso. I tavoli non cambiano piu' spesso.</summary>
    public static readonly TimeSpan CadenzaSerata = TimeSpan.FromSeconds(25);

    /// <summary>
    /// Cadenza per un torneo che non e' di oggi: i suoi turni sono chiusi e non si
    /// muovono piu'. Si continua a controllare, ma di rado — un arbitro puo' sempre
    /// spedire in ritardo un turno rimasto indietro — e senza spendere una chiamata
    /// ogni venticinque secondi su un server che non e' nostro.
    /// </summary>
    public static readonly TimeSpan CadenzaRiposo = TimeSpan.FromMinutes(5);

    /// <summary>Cadenza base.</summary>
    public TimeSpan Intervallo { get; set; } = CadenzaSerata;

    public bool Sospeso { get; private set; }

    public void Avvia()
    {
        Ferma();
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => Ciclo(_cts.Token));
    }

    public void Ferma()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    /// <summary>Sospende il polling quando l'app non e' in primo piano.</summary>
    public void Sospendi() => Sospeso = true;

    public void Riprendi() => Sospeso = false;

    /// <summary>Aggiornamento immediato richiesto dall'utente.</summary>
    public Task AggiornaOra(CancellationToken ct = default) => Scarica(ct);

    /// <summary>Un giro di interrogazione. Gestisce da se' i propri errori.</summary>
    protected abstract Task Scarica(CancellationToken ct);

    /// <summary>Da chiamare a giro riuscito: riporta la cadenza a quella base.</summary>
    protected void Riuscito() => _erroriConsecutivi = 0;

    /// <summary>Da chiamare a giro fallito: allunga l'attesa del giro successivo.</summary>
    protected void Fallito() => _erroriConsecutivi++;

    /// <summary>Azzera il conteggio degli errori, di norma quando si cambia cosa si segue.</summary>
    protected void AzzeraErrori() => _erroriConsecutivi = 0;

    private async Task Ciclo(CancellationToken ct)
    {
        // Primo giro subito, poi a cadenza.
        await Scarica(ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(IntervalloEffettivo(), ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (Sospeso) continue;
            await Scarica(ct);
        }
    }

    private TimeSpan IntervalloEffettivo() => _erroriConsecutivi switch
    {
        0 => Intervallo,
        1 => Intervallo * 2,
        2 => Intervallo * 4,
        _ => TimeSpan.FromMinutes(5)
    };

    protected static string Descrivi(Exception ex) => ex switch
    {
        TaskCanceledException or TimeoutException => "Il server non risponde.",
        HttpRequestException => "Connessione assente.",
        _ => "Aggiornamento non riuscito."
    };

    public ValueTask DisposeAsync()
    {
        Ferma();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
