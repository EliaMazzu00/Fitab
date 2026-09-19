namespace Fitab.Core.Models;

/// <summary>
/// Un tavolo del turno in corso: le due coppie che si stanno affrontando adesso.
/// </summary>
/// <param name="Numero">Numero del tavolo come lo espone il backend.</param>
public sealed record TavoloLive(int Numero, RigaGenerale Prima, RigaGenerale Seconda)
{
    /// <summary>Vero se a questo tavolo gioca la coppia del tesserato loggato.</summary>
    public bool SonoQui => Prima.SonoIo || Seconda.SonoIo;

    /// <summary>Distacco in VP fra le due coppie all'inizio del turno.</summary>
    public int Distacco => Math.Abs(Prima.VP - Seconda.VP);
}

/// <summary>
/// Una coppia nella classifica generale, col risultato dell'ultimo turno concluso
/// e il tavolo a cui e' seduta in quello in corso.
/// </summary>
public sealed record RigaGenerale
{
    public required LiveRiga Dati { get; init; }

    /// <summary>Posizione generale all'ultimo turno concluso.</summary>
    public int Posizione => Dati.Posizione;

    /// <summary>Victory point totali dall'inizio del torneo.</summary>
    public int VP => Dati.VP;

    /// <summary>Differenza punti totale dall'inizio del torneo. La somma su tutte le coppie fa zero.</summary>
    public int MP => Dati.MP;

    public string DescrizioneCoppia => Dati.DescrizioneCoppia;
    public string ChiaveCoppia => Dati.ChiaveCoppia;
    public bool SonoIo => Dati.SonoIo;

    // --- Risultato dell'ultimo turno concluso -------------------------------

    /// <summary>VP conquistati nell'ultimo turno concluso, su 20 in palio al tavolo.</summary>
    public int VPUltimoTurno { get; init; }

    /// <summary>Differenza punti dell'ultimo turno concluso.</summary>
    public int MPUltimoTurno { get; init; }

    /// <summary>
    /// Posizioni guadagnate nell'ultimo turno concluso: positivo = risalita.
    /// </summary>
    public int DeltaPosizione { get; init; }

    /// <summary>
    /// Falso quando il turno precedente non e' stato pubblicato dal gestionale
    /// dell'arbitro e quindi il risultato di tappa non e' ricostruibile.
    /// </summary>
    public bool RisultatoUltimoTurnoNoto { get; init; }

    // --- Proiezione sul turno in corso --------------------------------------

    /// <summary>Tavolo del turno in corso, zero se gli abbinamenti non sono noti.</summary>
    public int TavoloInCorso { get; init; }

    /// <summary>Coppia avversaria nel turno in corso, vuoto se gli abbinamenti non sono noti.</summary>
    public string Avversario { get; init; } = "";

    /// <summary>Posizione dell'avversario nel turno in corso, zero se non nota.</summary>
    public int PosizioneAvversario { get; init; }

    /// <summary>
    /// Vero se nel turno in corso la coppia e' abbinata a quella fittizia: sta di fatto
    /// riposando, e la previsione di posizione per lei non ha il solito significato.
    /// </summary>
    public bool ARiposo { get; init; }

    /// <summary>Vero per la coppia segnaposto dei campi dispari, da non mostrare in elenco.</summary>
    public bool EFittizia => Dati.EFittizia;

    public bool InRisalita => DeltaPosizione > 0;
    public bool InDiscesa => DeltaPosizione < 0;
}

/// <summary>
/// Classifica generale di un torneo live: la somma di tutti i turni, non la tappa.
/// <para>
/// E' quello che manca alla 1.x, dove si vedono solo i tavoli turno per turno e per
/// farsi un'idea della classifica bisogna aprire l'ultimo turno e interpretarlo.
/// In realta' il dato c'e' gia': il backend, nel comando <c>LIVCLA</c>, restituisce
/// VP e MP <b>cumulativi sull'intero torneo</b> e una <c>Posizione</c> che e' gia'
/// quella generale. Verificato su un torneo nazionale da 106 coppie: la somma dei VP
/// di ogni turno vale esattamente <c>coppie x 10 x turni giocati</c> e la somma degli
/// MP e' zero, perche' l'MP e' una differenza punti a somma nulla.
/// </para>
/// <para>
/// La somma nulla degli MP vale sul campo intero, non dentro il singolo girone: quando
/// un torneo si spezza in gironi lo fa per fascia di classifica, e i punti che il
/// girone di testa ha guadagnato li ha persi quello di coda. Su un torneo reale i due
/// gironi chiudevano a +16785 e -16785. La classifica di ogni girone resta corretta —
/// i gironi sono competizioni separate e non vanno unite — ma l'invariante si controlla
/// sommando tutti i gironi.
/// </para>
/// <para>
/// Quindi la classifica generale e' una sola chiamata, quella sull'ultimo turno
/// pubblicato. Il lavoro vero di questa classe e' il resto: capire quale sia
/// l'ultimo turno, ricostruire il risultato di tappa e ricavare gli abbinamenti
/// del turno in corso.
/// </para>
/// </summary>
public sealed record ClassificaGenerale
{
    public required IReadOnlyList<RigaGenerale> Righe { get; init; }

    /// <summary>Ultimo turno concluso e pubblicato: e' la fotografia che stiamo mostrando.</summary>
    public required LiveTurno UltimoTurno { get; init; }

    public string Girone { get; init; } = "";

    /// <summary>Turni effettivamente giocati fino a qui, compresi quelli non pubblicati.</summary>
    public int TurniGiocati { get; init; }

    public int TurniTotali { get; init; }

    /// <summary>
    /// Stima dei turni giocati ricavata dai soli VP: ogni tavolo ne assegna venti, quindi
    /// la somma vale <c>coppie x 10 x turni</c>. Serve da controprova, non da conteggio:
    /// quando il torneo e' diviso in gironi la stima sbanda, perche' i gironi si formano
    /// per fascia di classifica e i VP non sono distribuiti uniformemente fra loro.
    /// </summary>
    public double TurniGiocatiStimati { get; init; }

    /// <summary>
    /// Falso quando il numero di coppie e' cambiato fra l'ultimo turno e quello prima:
    /// il campo si e' ricomposto e la progressione dei turni non e' piu' una semplice
    /// somma.
    /// <para>
    /// Visto su un torneo reale del 15/09: il turno "02-M" portava 36 coppie contro le
    /// 18 del "01-M", e le prime 18 avevano VP e MP identici nei due turni. Non era il
    /// secondo turno, era lo stesso turno su un campo piu' grande. In casi cosi'
    /// <see cref="TurniGiocati"/> sovrastima e la UI fa bene a non scrivere
    /// "dopo N turni su M"; la classifica in se' resta valida.
    /// </para>
    /// </summary>
    public bool ConteggioTurniAttendibile { get; init; } = true;

    /// <summary>Vero se il torneo non e' finito e quindi c'e' un turno in corso.</summary>
    public bool TurnoInCorso => TurniGiocati > 0 && TurniGiocati < TurniTotali;

    /// <summary>Numero del turno che si sta giocando adesso.</summary>
    public int NumeroTurnoInCorso => TurnoInCorso ? TurniGiocati + 1 : 0;

    /// <summary>
    /// Tavoli del turno in corso. Vuoto quando gli abbinamenti non sono ricavabili,
    /// cioe' quando il prossimo turno e' ancora un Mitchell.
    /// </summary>
    public IReadOnlyList<TavoloLive> TavoliInCorso { get; init; } = [];

    /// <summary>
    /// Vero se conosciamo chi gioca contro chi nel turno in corso.
    /// <para>
    /// Non e' un'informazione in piu' chiesta al server: il campo <c>NumTavolo</c>
    /// delle righe dell'ultimo turno non e' il tavolo dove quel turno si e' giocato,
    /// e' gia' il tavolo del turno <b>successivo</b>, assegnato dall'accoppiamento
    /// danese (1ª contro 2ª al tavolo 1, 3ª contro 4ª al tavolo 2, e cosi' via).
    /// Verificato incrociando le variazioni di MP, che al tavolo sono opposte:
    /// 53 tavoli su 53 su un torneo nazionale, 7 su 7 su uno di circolo.
    /// </para>
    /// <para>
    /// Vale solo se il turno che sta per essere giocato e' un Danese. Se e' ancora un
    /// Mitchell l'abbinamento lo decide il movimento, non la classifica, e il campo
    /// <c>NumTavolo</c> diventa un residuo senza significato — zero corrispondenze su
    /// sette. E' anche il motivo per cui la 1.x nasconde il badge del tavolo sui
    /// turni Mitchell.
    /// </para>
    /// </summary>
    public bool AbbinamentiNoti => TavoliInCorso.Count > 0;

    /// <summary>
    /// Vero se il campo e' dispari e il gestionale ha aggiunto la coppia segnaposto.
    /// E' anche l'unico caso in cui la somma degli MP non fa zero.
    /// </summary>
    public bool ConCoppiaFittizia => Righe.Any(r => r.EFittizia);

    /// <summary>Righe da mostrare: la classifica senza la coppia segnaposto.</summary>
    public IEnumerable<RigaGenerale> RigheReali => Righe.Where(r => !r.EFittizia);

    /// <summary>La riga del tesserato loggato, se sta giocando questo torneo.</summary>
    public RigaGenerale? Mia => Righe.FirstOrDefault(r => r.SonoIo);

    /// <summary>Etichetta pronta per la UI, es. "dopo 7 turni su 11".</summary>
    public string Descrizione => !ConteggioTurniAttendibile
        ? $"aggiornata al {UltimoTurno.Descrizione}"
        : TurniTotali > 0
            ? $"dopo {TurniGiocati} turni su {TurniTotali}"
            : $"dopo {TurniGiocati} turni";

    // ------------------------------------------------------------------------

    /// <summary>
    /// Costruisce la classifica generale a partire dalle righe dell'ultimo turno
    /// pubblicato.
    /// </summary>
    /// <param name="torneo">Serve per il numero di turni e per la composizione Mitchell/Danesi.</param>
    /// <param name="ultimoTurno">Ultimo turno con risultati, scelto con <c>InOrdineDiGioco</c>.</param>
    /// <param name="righe">Righe di <c>LIVCLA</c> su quel turno: sono gia' la classifica generale.</param>
    /// <param name="righePrecedenti">
    /// Righe del turno pubblicato prima, se disponibili. Servono solo a ricostruire il
    /// risultato di tappa e i delta di posizione; senza, la classifica resta valida.
    /// </param>
    public static ClassificaGenerale Calcola(
        LiveTorneo torneo,
        LiveTurno ultimoTurno,
        IReadOnlyList<LiveRiga> righe,
        IReadOnlyList<LiveRiga>? righePrecedenti = null)
    {
        var ordinate = righe.OrderBy(r => r.Posizione).ToList();
        var giocati = TurniGiocatiA(torneo, ultimoTurno);
        var turnoInCorso = giocati > 0 && giocati < torneo.TurniTotali;

        // Se il campo cambia di numero fra un turno e l'altro non stiamo guardando una
        // progressione: il confronto di tappa e il conteggio dei turni perdono senso.
        var campoStabile = righePrecedenti is null || righePrecedenti.Count == ordinate.Count;

        var prima = campoStabile
            ? righePrecedenti?
                .GroupBy(r => r.ChiaveCoppia)
                .ToDictionary(g => g.Key, g => g.First())
            : null;

        var stima = ordinate.Count > 0 ? ordinate.Sum(r => r.VP) / (ordinate.Count * 10.0) : 0;

        // Gli abbinamenti del turno in corso li conosciamo solo se quel turno e' Danese.
        var abbinamenti = turnoInCorso && ProssimoTurnoEDanese(torneo, giocati)
            ? Abbina(ordinate)
            : null;


        var righeGenerali = ordinate.Select(r =>
        {
            var precedente = prima is not null && prima.TryGetValue(r.ChiaveCoppia, out var p) ? p : null;
            var avversario = abbinamenti is not null && abbinamenti.TryGetValue(r.ChiaveCoppia, out var a) ? a : null;

            return new RigaGenerale
            {
                Dati = r,
                VPUltimoTurno = precedente is null ? 0 : r.VP - precedente.VP,
                MPUltimoTurno = precedente is null ? 0 : r.MP - precedente.MP,
                DeltaPosizione = precedente is null ? 0 : precedente.Posizione - r.Posizione,
                RisultatoUltimoTurnoNoto = precedente is not null,
                TavoloInCorso = avversario is null ? 0 : r.NumTavolo,
                Avversario = avversario?.DescrizioneCoppia ?? "",
                PosizioneAvversario = avversario?.Posizione ?? 0,
                ARiposo = avversario?.EFittizia ?? false
            };
        }).ToList();

        IReadOnlyList<TavoloLive> tavoli = abbinamenti is null ? [] : Tavoli(righeGenerali);

        return new ClassificaGenerale
        {
            Righe = righeGenerali,
            UltimoTurno = ultimoTurno,
            Girone = ultimoTurno.Girone,
            TurniGiocati = giocati,
            TurniTotali = torneo.TurniTotali,
            TurniGiocatiStimati = stima,
            // Senza un turno precedente da confrontare ci si affida alla stima sui VP,
            // che e' attendibile finche' il girone e' unico.
            ConteggioTurniAttendibile = campoStabile
                && (righePrecedenti is not null || Math.Abs(stima - giocati) <= 0.6),
            TavoliInCorso = tavoli
        };
    }

    /// <summary>
    /// Quanti turni sono stati giocati quando il turno indicato e' l'ultimo pubblicato.
    /// <para>
    /// Non si puo' contare l'elenco dei turni, che ha buchi. Si ricava dalla struttura
    /// del torneo: i Mitchell si giocano per primi, quindi un "01-D" in un torneo
    /// 2 Mitchell + 2 Danesi e' il terzo turno. Quando <c>SoloDanesi</c> e' attivo la
    /// numerazione e' unica e il numero del turno e' gia' il conteggio.
    /// </para>
    /// </summary>
    public static int TurniGiocatiA(LiveTorneo torneo, LiveTurno turno) =>
        turno.Tipo == TipoTurno.Mitchell || torneo.SoloDanesi
            ? turno.Numero
            : torneo.TurniMitchell + turno.Numero;

    private static bool ProssimoTurnoEDanese(LiveTorneo torneo, int giocati) =>
        torneo.SoloDanesi || giocati >= torneo.TurniMitchell;

    /// <summary>
    /// Accoppia le righe per numero di tavolo. Ogni tavolo ospita esattamente due
    /// coppie: i gruppi di dimensione diversa sono un dato sporco e vengono scartati
    /// invece di produrre un avversario sbagliato.
    /// </summary>
    private static Dictionary<string, LiveRiga>? Abbina(List<LiveRiga> righe)
    {
        var abbinamenti = new Dictionary<string, LiveRiga>();

        foreach (var tavolo in righe.Where(r => r.NumTavolo > 0).GroupBy(r => r.NumTavolo))
        {
            var coppie = tavolo.ToList();
            if (coppie.Count != 2) continue;

            abbinamenti[coppie[0].ChiaveCoppia] = coppie[1];
            abbinamenti[coppie[1].ChiaveCoppia] = coppie[0];
        }

        return abbinamenti.Count > 0 ? abbinamenti : null;
    }

    private static List<TavoloLive> Tavoli(List<RigaGenerale> righe) =>
        righe.Where(r => r.TavoloInCorso > 0)
             .GroupBy(r => r.TavoloInCorso)
             .Where(g => g.Count() == 2)
             .OrderBy(g => g.Key)
             .Select(g =>
             {
                 var coppie = g.OrderBy(r => r.Posizione).ToList();
                 return new TavoloLive(g.Key, coppie[0], coppie[1]);
             })
             .ToList();
}
