using Fitab.Data;

namespace Fitab.App.Servizi;

/// <summary>
/// Scarica in anticipo le classifiche, in background, mentre l'utente fa altro.
/// <para>
/// Ha senso per una ragione precisa: la graduatoria nazionale costa ~24 secondi
/// per pagina lato server, mentre quella filtrata per circolo sta sotto il
/// secondo. Aspettare di aprire la schermata per scaricarla
/// significa mezzo minuto di attesa; farlo all'avvio significa trovarla gia'
/// pronta. E la classifica non cambia di ora in ora: la cache dura 24 ore,
/// quindi questo lavoro si fa una volta al giorno.
/// </para>
/// <para>
/// Il server non e' nostro, quindi il precaricamento e' deliberatamente
/// prudente: una richiesta alla volta, mai in parallelo, con una pausa fra
/// una e l'altra, e salta tutto cio' che e' gia' in cache e ancora valido.
/// </para>
/// </summary>
public sealed class Precaricamento(FitabData dati, SessioneUtente utente)
{
    /// <summary>Pagine della nazionale da precaricare: 4 x 50 = i primi 200.</summary>
    private const int PagineNazionale = 4;

    private const int VociPerPagina = 50;

    /// <summary>Respiro fra una richiesta e l'altra, per non martellare il server.</summary>
    private static readonly TimeSpan Pausa = TimeSpan.FromSeconds(2);

    private CancellationTokenSource? _cts;
    private Task? _lavoro;

    public bool InCorso => _lavoro is { IsCompleted: false };

    /// <summary>Avvia il precaricamento. Chiamate ripetute non lo duplicano.</summary>
    public void Avvia()
    {
        if (InCorso) return;

        _cts = new CancellationTokenSource();
        _lavoro = Task.Run(() => Esegui(_cts.Token));
    }

    public void Ferma()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    /// <summary>
    /// Pausa fra una richiesta e l'altra, ma solo se siamo davvero andati in
    /// rete: su un dato gia' in cache non c'e' nessuno da non disturbare, e
    /// aspettare comunque significherebbe perdere due minuti a ogni riavvio
    /// solo per riattraversare cio' che e' gia' salvato.
    /// </summary>
    private static Task RespiraAsync(bool daCache, CancellationToken ct) =>
        daCache ? Task.CompletedTask : Task.Delay(Pausa, ct);

    private async Task Esegui(CancellationToken ct)
    {
        try
        {
            // 1. Prima il circolo del tesserato: e' la classifica che apre piu'
            //    spesso ed e' immediata da scaricare.
            if (utente.Attuale is { IdCircolo: > 0 } tesserato)
            {
                var mio = await dati.ClassificaAsync(
                    1, codiceCircolo: tesserato.IdCircolo, ct: ct);
                await RespiraAsync(mio.DaCache, ct);
            }

            // 2. Poi le prime pagine della nazionale, una alla volta.
            for (var pagina = 0; pagina < PagineNazionale && !ct.IsCancellationRequested; pagina++)
            {
                try
                {
                    var voci = await dati.ClassificaAsync(
                        1, daPosizione: pagina * VociPerPagina + 1, ct: ct);

                    // Fine della graduatoria: inutile insistere.
                    if (voci.Valore.Count < VociPerPagina) break;

                    await RespiraAsync(voci.DaCache, ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception)
                {
                    // Pagina saltata: le altre proseguono.
                }
            }

            // 3. Infine le classifiche di tutti i circoli. Sono ~77 richieste da
            //    meno di un secondo: distribuite con calma valgono la pena,
            //    perche' rendono istantanea qualsiasi scheda di circolo.
            var circoli = await dati.CircoliAsync(ct: ct);

            foreach (var circolo in circoli.Valore)
            {
                if (ct.IsCancellationRequested) return;

                // Ogni circolo per conto suo: se il server inciampa su uno —
                // capita con quelli senza classifica — non deve fermare gli
                // altri settanta. Prima un singolo errore azzerava tutto.
                try
                {
                    var voci = await dati.ClassificaAsync(
                        1, codiceCircolo: circolo.Codice, ct: ct);

                    await RespiraAsync(voci.DaCache, ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception)
                {
                    // Si riproverà al prossimo avvio.
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Uscita normale: l'app si sta chiudendo o e' andata in background.
        }
        catch (Exception)
        {
            // Il precaricamento e' un'ottimizzazione: se fallisce, i dati
            // verranno scaricati su richiesta come sempre.
        }
    }
}
