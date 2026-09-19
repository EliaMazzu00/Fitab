// Verifica di contratto del layer API contro il backend reale (cms.fitab.it).
// Non e' un test unitario: e' la sonda che dice se gli endpoint rispondono ancora
// come ci aspettiamo. Da rilanciare ogni volta che qualcosa smette di funzionare,
// prima di cercare il bug nell'app.
//
//   dotnet run --project src/Fitab.Probe

using System.Diagnostics;
using Fitab.Api;
using Fitab.Core.Abstractions;
using Fitab.Core.Models;
using Fitab.Data;
using Microsoft.Extensions.DependencyInjection;

var cartellaCache = Path.Combine(Path.GetTempPath(), "fitab-sonda-cache");
if (Directory.Exists(cartellaCache)) Directory.Delete(cartellaCache, true);

var services = new ServiceCollection();
services.AddFitabApi();
services.AddFitabData(cartellaCache);

var fornitore = services.BuildServiceProvider();
var api = fornitore.GetRequiredService<IFitabApi>();
var dati = fornitore.GetRequiredService<FitabData>();

var esiti = new List<(string Nome, bool Ok, string Dettaglio, long Ms)>();

// Quanti tornei live passare in rassegna nel controllo della classifica generale.
// Di norma bastano otto. Per passarli tutti, quando si tocca la logica dei turni e si
// vogliono vedere anche i casi rari (gironi multipli, campo dispari, campo che cambia
// numero fra un turno e l'altro):
//
//   dotnet run --project src/Fitab.Probe -- 60
var quantiCampioni = args.Length > 0 && int.TryParse(args[0], out var n) ? n : 8;

async Task Prova(string nome, Func<Task<string>> azione)
{
    var cronometro = Stopwatch.StartNew();
    try
    {
        var dettaglio = await azione();
        cronometro.Stop();
        esiti.Add((nome, true, dettaglio, cronometro.ElapsedMilliseconds));
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("  OK  ");
        Console.ResetColor();
        Console.WriteLine($"{nome,-22} {cronometro.ElapsedMilliseconds,5} ms   {dettaglio}");
    }
    catch (Exception ex)
    {
        cronometro.Stop();
        esiti.Add((nome, false, ex.Message, cronometro.ElapsedMilliseconds));
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write(" FAIL ");
        Console.ResetColor();
        Console.WriteLine($"{nome,-22} {cronometro.ElapsedMilliseconds,5} ms   {ex.GetType().Name}: {ex.Message}");
    }
}

Console.WriteLine();
Console.WriteLine("Sonda web service FITAB — https://cms.fitab.it/");
Console.WriteLine(new string('-', 78));

await Prova("News", async () =>
{
    var news = await api.GetNewsAsync();
    if (news.Count == 0) throw new InvalidOperationException("nessuna news");
    var ultima = news[0];
    return $"{news.Count} news, ultima: {ultima.Data:dd/MM/yyyy} \"{Taglia(ultima.Titolo, 40)}\"";
});

await Prova("Calendario", async () =>
{
    var tornei = await api.GetCalendarioAsync(DateOnly.FromDateTime(DateTime.Today));
    if (tornei.Count == 0) throw new InvalidOperationException("nessun torneo");
    var primo = tornei[0];
    return $"{tornei.Count} tornei, prossimo: {primo.DaData:dd/MM/yyyy} [{primo.Rilevanza.Descrizione()}]";
});

await Prova("Circoli", async () =>
{
    var circoli = await api.GetCircoliAsync();
    if (circoli.Count == 0) throw new InvalidOperationException("nessun circolo");
    var conCitta = circoli.Count(c => !string.IsNullOrWhiteSpace(c.Citta));
    return $"{circoli.Count} circoli attivi, {conCitta} con citta' valorizzata";
});

await Prova("Arbitri", async () =>
{
    var arbitri = await api.GetArbitriAsync();
    if (arbitri.Count == 0) throw new InvalidOperationException("nessun arbitro");
    return $"{arbitri.Count} arbitri";
});

await Prova("Classifica", async () =>
{
    var classifica = await api.GetClassificaAsync(tipoClassifica: 1);
    if (classifica.Count == 0) throw new InvalidOperationException("classifica vuota");
    var primo = classifica[0];
    return $"{classifica.Count} voci, 1° {Taglia(primo.CognomeNome, 24)} " +
           $"{primo.Punti} pt [{primo.Categoria.Descrizione()}]";
});

// --- Live: e' la parte che conta, la verifichiamo in profondita' -------------

var tornei = Array.Empty<Fitab.Core.Models.LiveTorneo>();

await Prova("Live tornei", async () =>
{
    tornei = [.. await api.GetLiveTorneiAsync()];
    if (tornei.Length == 0) throw new InvalidOperationException("nessun torneo live");
    var oggi = tornei.Count(t => t.EDiOggi);
    return $"{tornei.Length} tornei, {oggi} in data odierna";
});

var torneo = tornei.FirstOrDefault(t => t.EDiOggi) ?? tornei.FirstOrDefault();

if (torneo is not null)
{
    Console.WriteLine();
    Console.WriteLine($"  torneo campione: \"{Taglia(torneo.Descrizione, 50)}\"");
    Console.WriteLine($"                   {torneo.Circolo} — {torneo.Data:dd/MM/yyyy} — " +
                      $"{torneo.NumeroTavoli} tavoli, {torneo.TurniTotali} turni, stato \"{torneo.Stato}\"");
    Console.WriteLine();

    var turni = Array.Empty<Fitab.Core.Models.LiveTurno>();

    await Prova("Live turni", async () =>
    {
        turni = [.. await api.GetLiveTurniAsync(torneo.Codice)];
        if (turni.Length == 0) throw new InvalidOperationException("nessun turno");
        var etichette = string.Join(" ", turni.Take(6).Select(t => t.EtichettaBreve));
        return $"{turni.Length} turni: {etichette}";
    });

    if (turni.Length > 0)
    {
        var turno = turni[0];
        await Prova("Live classifica", async () =>
        {
            var righe = await api.GetLiveClassificaAsync(torneo.Codice, turno.Codice, turno.Girone);
            if (righe.Count == 0) throw new InvalidOperationException("classifica turno vuota");
            var prima = righe[0];
            return $"turno {turno.Codice}: {righe.Count} coppie, 1ª {Taglia(prima.DescrizioneCoppia, 30)} " +
                   $"VP {prima.VP} / MP {prima.MP}";
        });

        var sequenza = turni.InOrdineDiGioco().ToList();

        await Prova("Ordine turni", () =>
        {
            if (sequenza.Count == 0) throw new InvalidOperationException("nessun turno giocato");
            var etichette = string.Join(" < ", sequenza.Select(t => t.Codice));
            return Task.FromResult($"{etichette}");
        });

        if (sequenza.Count > 0)
        {
            var ultimo = sequenza[^1];
            var penultimo = sequenza.Count > 1 ? sequenza[^2] : null;

            await Prova("Classifica generale", async () =>
            {
                var righe = await api.GetLiveClassificaAsync(torneo.Codice, ultimo.Codice, ultimo.Girone);
                var prima = penultimo is null
                    ? null
                    : await api.GetLiveClassificaAsync(torneo.Codice, penultimo.Codice, penultimo.Girone);

                var generale = ClassificaGenerale.Calcola(torneo, ultimo, righe, prima);

                // Le due invarianti — somma MP nulla e somma VP pari a coppie x 10 x
                // turni — valgono sul campo intero. Sono verificabili solo a girone
                // unico e senza coppia fittizia: i gironi si formano per fascia di
                // classifica e si scambiano i punti fra loro, la coppia fittizia
                // regala punti che nessuno perde.
                var gironi = turni.Gironi();
                var campoIntero = gironi.Count == 1 && !generale.ConCoppiaFittizia;

                var sommaMp = generale.Righe.Sum(r => r.MP);
                if (sommaMp != 0 && campoIntero)
                    throw new InvalidOperationException($"somma MP {sommaMp}, attesa 0");

                var nota = "";

                if (campoIntero && generale.ConteggioTurniAttendibile)
                {
                    var atteso = generale.Righe.Count * 10 * generale.TurniGiocati;
                    var somma = generale.Righe.Sum(r => r.VP);
                    var scarto = Math.Abs(somma - atteso);
                    if (scarto > generale.Righe.Count)
                        throw new InvalidOperationException(
                            $"somma VP {somma}, attesa ~{atteso}: VP forse non piu' cumulativi");
                    nota = $", somma MP 0, scarto VP {scarto}";
                }
                else
                {
                    nota = generale.ConCoppiaFittizia
                        ? ", invarianti non verificabili (campo dispari)"
                        : $", invarianti non verificabili ({gironi.Count} gironi)";
                }

                var testa = generale.Righe[0];
                return $"{generale.Descrizione}: {generale.Righe.Count} coppie, " +
                       $"1ª {Taglia(testa.DescrizioneCoppia, 24)} {testa.VP} VP{nota}";
            });

            await Prova("Turno in corso", async () =>
            {
                var righe = await api.GetLiveClassificaAsync(torneo.Codice, ultimo.Codice, ultimo.Girone);
                var generale = ClassificaGenerale.Calcola(torneo, ultimo, righe);

                if (!generale.TurnoInCorso)
                    return $"torneo concluso ({generale.TurniGiocati}/{generale.TurniTotali} turni)";

                if (!generale.AbbinamentiNoti)
                    return $"{generale.NumeroTurnoInCorso}° turno in corso, " +
                           "abbinamenti non ricavabili (il prossimo turno e' Mitchell)";

                var tavolo = generale.TavoliInCorso[0];

                return $"{generale.NumeroTurnoInCorso}° turno, {generale.TavoliInCorso.Count} tavoli — " +
                       $"tav.1 {Taglia(tavolo.Prima.DescrizioneCoppia, 20)} " +
                       $"vs {Taglia(tavolo.Seconda.DescrizioneCoppia, 20)}";
            });
        }
    }
}

// La classifica generale si regge su due deduzioni fragili — l'ordine dei turni e il
// conteggio dei turni giocati — e i casi che le mettono in crisi (gironi multipli,
// campo che cambia numero fra un turno e l'altro) non compaiono sul torneo campione.
// Qui si passano in rassegna alcuni tornei veri e si controlla che regga tutto.

if (tornei.Length > 0)
{
    await Prova("Generale (a campione)", async () =>
    {
        var esaminati = 0;
        var conGironi = 0;
        var nonAttendibili = 0;
        var conAbbinamenti = 0;
        var conFittizia = 0;

        // Campionatura distribuita su tutto l'elenco, estremi compresi, e non i primi
        // otto: l'elenco e' ordinato per data e i tornei a piu' gironi — quelli che
        // mettono davvero alla prova il conteggio dei turni — stanno in fondo.
        var campioni = Math.Clamp(quantiCampioni, 2, tornei.Length);
        var campione = Enumerable.Range(0, campioni)
            .Select(i => tornei[i * (tornei.Length - 1) / (campioni - 1)])
            .DistinctBy(t => t.Codice)
            .ToList();

        foreach (var t in campione)
        {
            var turniT = await api.GetLiveTurniAsync(t.Codice);
            var gironi = turniT.Gironi();

            foreach (var girone in gironi)
            {
                var sequenza = turniT.Where(x => x.Girone == girone).InOrdineDiGioco().ToList();
                if (sequenza.Count == 0) continue;

                var ultimoT = sequenza[^1];
                var righeT = await api.GetLiveClassificaAsync(t.Codice, ultimoT.Codice, girone);
                if (righeT.Count == 0) continue;

                var precedentiT = sequenza.Count > 1
                    ? await api.GetLiveClassificaAsync(t.Codice, sequenza[^2].Codice, girone)
                    : null;

                var g = ClassificaGenerale.Calcola(t, ultimoT, righeT, precedentiT);

                if (g.Righe.Sum(r => r.MP) != 0 && gironi.Count == 1 && !g.ConCoppiaFittizia)
                    throw new InvalidOperationException(
                        $"\"{Taglia(t.Descrizione, 24)}\" girone {girone}: somma MP non nulla");

                if (g.Righe.Count != righeT.Count)
                    throw new InvalidOperationException("righe perse nel calcolo");

                // Ogni tavolo del turno in corso deve avere esattamente due coppie.
                foreach (var tavolo in g.TavoliInCorso)
                    if (tavolo.Prima.ChiaveCoppia == tavolo.Seconda.ChiaveCoppia)
                        throw new InvalidOperationException($"tavolo {tavolo.Numero} con una coppia sola");

                esaminati++;
                if (gironi.Count > 1) conGironi++;
                if (!g.ConteggioTurniAttendibile) nonAttendibili++;
                if (g.AbbinamentiNoti) conAbbinamenti++;
                if (g.ConCoppiaFittizia) conFittizia++;
            }
        }

        if (esaminati == 0) throw new InvalidOperationException("nessuna classifica esaminata");

        return $"{esaminati} classifiche ok — {conGironi} a piu' gironi, " +
               $"{conAbbinamenti} con abbinamenti, {conFittizia} a campo dispari, " +
               $"{nonAttendibili} con conteggio turni non attendibile";
    });
}

// --- Login: senza credenziali verifichiamo solo il rifiuto -------------------

await Prova("Login (rifiuto)", async () =>
{
    var esito = await api.LoginAsync("XX000ZZ", "AAAAAA00A00A000A");
    return esito.Riuscito
        ? throw new InvalidOperationException("credenziali fasulle accettate!")
        : $"rifiutato correttamente: \"{Taglia(esito.Messaggio, 40)}\"";
});

// --- Layer di cache -------------------------------------------------------

Console.WriteLine();
Console.WriteLine("Cache locale (cache-first con revalidate)");
Console.WriteLine(new string('-', 78));

await Prova("Cache: 1ª lettura", async () =>
{
    var c = await dati.NewsAsync();
    if (c.Valore.Count == 0) throw new InvalidOperationException("nessuna news");
    return c.DaCache
        ? throw new InvalidOperationException("non doveva venire dalla cache")
        : $"{c.Valore.Count} news dalla rete";
});

await Prova("Cache: 2ª lettura", async () =>
{
    var c = await dati.NewsAsync();
    return c.DaCache
        ? $"{c.Valore.Count} news dal disco, aggiornate {c.EtaDescrizione}"
        : throw new InvalidOperationException("doveva venire dalla cache");
});

await Prova("Cache: circoli", async () =>
{
    var primo = System.Diagnostics.Stopwatch.StartNew();
    var a = await dati.CircoliAsync();
    primo.Stop();

    var secondo = System.Diagnostics.Stopwatch.StartNew();
    var b = await dati.CircoliAsync();
    secondo.Stop();

    if (!b.DaCache) throw new InvalidOperationException("seconda lettura non dalla cache");
    return $"{a.Valore.Count} circoli: rete {primo.ElapsedMilliseconds} ms, cache {secondo.ElapsedMilliseconds} ms";
});

await Prova("Cache: su disco", () =>
{
    var file = Directory.GetFiles(Path.Combine(cartellaCache, "cache"), "*.json");
    if (file.Length == 0) throw new InvalidOperationException("nessun file scritto");
    var kb = file.Sum(f => new FileInfo(f).Length) / 1024;
    return Task.FromResult($"{file.Length} file, {kb} KB");
});

await Prova("Classifica circolo", async () =>
{
    var c = await dati.ClassificaAsync(1, codiceCircolo: 14);
    if (c.Valore.Count == 0) throw new InvalidOperationException("vuota");
    return $"{c.Valore.Count} voci, 1° {Taglia(c.Valore[0].CognomeNome, 22)} {c.Valore[0].Punti} pt";
});

Console.WriteLine(new string('-', 78));
var ok = esiti.Count(e => e.Ok);
var tempoMedio = esiti.Count > 0 ? (int)esiti.Average(e => e.Ms) : 0;
Console.WriteLine($"{ok}/{esiti.Count} endpoint funzionanti — tempo medio {tempoMedio} ms");
Console.WriteLine();

return esiti.All(e => e.Ok) ? 0 : 1;

static string Taglia(string s, int max) =>
    s.Length <= max ? s : string.Concat(s.AsSpan(0, max - 1), "…");
