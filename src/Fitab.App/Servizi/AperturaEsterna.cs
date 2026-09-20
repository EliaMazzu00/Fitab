namespace Fitab.App.Servizi;

/// <summary>
/// Azioni che escono dall'app: PDF, telefono, email, mappe, sito.
/// <para>
/// I PDF (locandine dei tornei, allegati delle news) si aprono nel visualizzatore
/// di sistema come faceva l'app originale: sono documenti impaginati per la
/// stampa, forzarli dentro una WebView a 375px li rende illeggibili.
/// </para>
/// </summary>
public sealed class AperturaEsterna
{
    public Task ApriUrlAsync(string url) =>
        string.IsNullOrWhiteSpace(url) ? Task.CompletedTask : Prova(() => Launcher.Default.OpenAsync(url));

    public Task ChiamaAsync(string numero)
    {
        var pulito = new string(numero.Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (pulito.Length == 0) return Task.CompletedTask;

        return Prova(() =>
        {
            PhoneDialer.Default.Open(pulito);
            return Task.FromResult(true);
        });
    }

    public Task ScriviEmailAsync(string indirizzo, string? oggetto = null)
    {
        if (string.IsNullOrWhiteSpace(indirizzo)) return Task.CompletedTask;

        var url = $"mailto:{indirizzo}";
        if (!string.IsNullOrWhiteSpace(oggetto))
            url += $"?subject={Uri.EscapeDataString(oggetto)}";

        return ApriUrlAsync(url);
    }

    /// <summary>
    /// Ricerca su mappa per testo libero: gli indirizzi dei circoli arrivano dal
    /// CMS in forma libera, e un placemark costruito a pezzi non li ritrova.
    /// <para>
    /// Lo schema dell'URL cambia per piattaforma: <c>geo:</c> esiste solo su
    /// Android, su iOS va usato maps.apple.com e su Windows lo schema bingmaps.
    /// </para>
    /// </summary>
    public Task CercaSuMappaAsync(string testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return Task.CompletedTask;

        var query = Uri.EscapeDataString(testo);

        var url = DeviceInfo.Current.Platform switch
        {
            var p when p == DevicePlatform.iOS || p == DevicePlatform.macOS
                => $"https://maps.apple.com/?q={query}",
            var p when p == DevicePlatform.WinUI
                => $"bingmaps:?q={query}",
            _ => $"geo:0,0?q={query}"
        };

        return ApriUrlAsync(url);
    }

    private static async Task Prova(Func<Task> azione)
    {
        try
        {
            await azione();
        }
        catch (Exception)
        {
            // Nessuna app in grado di gestire l'azione (o permesso negato):
            // non e' un errore che valga la pena mostrare all'utente.
        }
    }

    private static Task Prova(Func<Task<bool>> azione) => Prova(() => (Task)azione());
}
