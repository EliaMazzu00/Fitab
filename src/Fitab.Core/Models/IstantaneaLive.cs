namespace Fitab.Core.Models;

/// <summary>
/// Fotografia di un torneo live, salvata su disco per ridisegnare la schermata
/// senza rete.
/// <para>
/// Serve a togliere l'attesa all'ingresso. Prima la pagina non mostrava nulla
/// finche' non rispondeva il server: elenco tornei, elenco turni, classifica
/// dell'ultimo turno e di quello prima, tutte in fila — circa un secondo e mezzo
/// su rete fissa, oltre due su rete mobile, con la schermata vuota nel frattempo.
/// Ed era cosi' <b>a ogni ingresso</b>, anche rientrando dopo dieci secondi,
/// perche' il polling andava dritto all'API saltando la cache.
/// </para>
/// <para>
/// Qui si conserva tutto quello che serve a ricostruire la schermata: il torneo,
/// i turni e le righe degli ultimi due. Al rientro la classifica compare
/// istantaneamente con l'etichetta di quando e' stata presa, e il primo giro di
/// polling la rimpiazza con quella fresca.
/// </para>
/// </summary>
/// <param name="Torneo">Serve per il conteggio dei turni e la composizione Mitchell/Danesi.</param>
/// <param name="Turni">Elenco completo, cosi' i chip ci sono subito.</param>
/// <param name="Ultimo">Ultimo turno concluso al momento della fotografia.</param>
/// <param name="Righe">Classifica su quel turno: e' gia' la generale.</param>
/// <param name="Precedenti">
/// Righe del turno prima, se erano disponibili: servono solo ai risultati di tappa.
/// </param>
public sealed record IstantaneaLive(
    LiveTorneo Torneo,
    IReadOnlyList<LiveTurno> Turni,
    LiveTurno Ultimo,
    IReadOnlyList<LiveRiga> Righe,
    IReadOnlyList<LiveRiga>? Precedenti);
