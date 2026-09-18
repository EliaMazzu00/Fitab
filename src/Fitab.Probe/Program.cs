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
    }
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
