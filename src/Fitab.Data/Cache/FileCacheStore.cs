using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Fitab.Data.Cache;

/// <summary>Archivio su file JSON, uno per chiave.</summary>
public sealed class FileCacheStore : ICacheStore
{
    private readonly string _cartella;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _lucchetti = new();

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = false
    };

    public FileCacheStore(string cartella)
    {
        _cartella = cartella;
        Directory.CreateDirectory(_cartella);
    }

    private sealed record Voce<T>(T Valore, DateTimeOffset AggiornatoIl);

    public async Task<Cached<T>?> LeggiAsync<T>(string chiave, CancellationToken ct = default)
    {
        var percorso = Percorso(chiave);
        if (!File.Exists(percorso)) return null;

        var lucchetto = Lucchetto(chiave);
        await lucchetto.WaitAsync(ct);
        try
        {
            await using var stream = File.OpenRead(percorso);
            var voce = await JsonSerializer.DeserializeAsync<Voce<T>>(stream, Json, ct);

            return voce is null
                ? null
                : new Cached<T>(voce.Valore, voce.AggiornatoIl, DaCache: true);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Cache corrotta o scritta da una versione precedente del modello:
            // non e' un errore per l'utente, si riscarica e basta.
            TentaCancellazione(percorso);
            return null;
        }
        finally
        {
            lucchetto.Release();
        }
    }

    public async Task ScriviAsync<T>(string chiave, T valore, CancellationToken ct = default)
    {
        var percorso = Percorso(chiave);
        var lucchetto = Lucchetto(chiave);

        await lucchetto.WaitAsync(ct);
        try
        {
            // Scrittura atomica: file temporaneo e poi rename, cosi' un'interruzione
            // non lascia mai una cache mezza scritta.
            var temporaneo = percorso + ".tmp";
            await using (var stream = File.Create(temporaneo))
            {
                await JsonSerializer.SerializeAsync(
                    stream, new Voce<T>(valore, DateTimeOffset.Now), Json, ct);
            }

            File.Move(temporaneo, percorso, overwrite: true);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Non poter scrivere la cache non deve mai far fallire un'operazione.
        }
        finally
        {
            lucchetto.Release();
        }
    }

    private SemaphoreSlim Lucchetto(string chiave) =>
        _lucchetti.GetOrAdd(chiave, _ => new SemaphoreSlim(1, 1));

    private string Percorso(string chiave) =>
        Path.Combine(_cartella, NomeFile(chiave) + ".json");

    /// <summary>
    /// Le chiavi contengono ':' e altri caratteri non validi nei nomi file, quindi
    /// componiamo un nome leggibile piu' un hash breve che ne garantisce l'unicita'.
    /// </summary>
    private static string NomeFile(string chiave)
    {
        var leggibile = new string(chiave
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .Take(40)
            .ToArray());

        var hash = Convert.ToHexString(
            MD5.HashData(Encoding.UTF8.GetBytes(chiave)))[..8];

        return $"{leggibile}-{hash}".ToLowerInvariant();
    }

    private static void TentaCancellazione(string percorso)
    {
        try
        {
            if (File.Exists(percorso)) File.Delete(percorso);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
