# Fitab 2.0 — soluzione

App dimostrativa per FITAB-APS. .NET MAUI Blazor, componenti Blazor propri
(nessuna libreria UI), dati dai web service `cms.fitab.it`.

Le note di reverse engineering sul backend e sull'app esistente — che non sono
nostre e riguardano un servizio di terzi — sono tenute fuori dal repository.

Per installare la demo: [INSTALLAZIONE.md](INSTALLAZIONE.md).

## Progetti

```
Fitab.Core    modelli di dominio, IFitabApi, utilità testo
Fitab.Api     client HTTP su cms.fitab.it (anti-corruption layer)
Fitab.Data    cache-first su file + politiche di aggiornamento
Fitab.App     MAUI Blazor: componenti, pagine, servizi di piattaforma
Fitab.Probe   sonda di contratto contro il backend reale
```

La dipendenza va in una sola direzione: `App → Data → Api → Core`.
Nessun progetto oltre `Fitab.Api` conosce la forma degli endpoint: se il backend
cambia — o viene rifatto — si riscrive quel progetto e basta.

## Comandi

Sviluppo su Windows (iterazione rapida sulla UI):

```bash
dotnet run --project src/Fitab.App -f net10.0-windows10.0.19041.0
```

Deploy su un Android collegato:

```bash
dotnet build src/Fitab.App/Fitab.App.csproj -f net10.0-android -c Release -t:Run
```

APK da presentare:

```bash
dotnet publish src/Fitab.App/Fitab.App.csproj -f net10.0-android -c Release
```

Verifica che i web service rispondano ancora come ci aspettiamo:

```bash
dotnet run --project src/Fitab.Probe
```

## Note operative

- **Package id `it.mazzuchelli.fitab.demo`**, non `com.fitab.evin`: l'app
  pubblicata resta di Evin srl e le due devono poter convivere sullo stesso
  telefono.
- **Target Android e Windows.** iOS e MacCatalyst sono stati tolti dai
  `TargetFrameworks` per accorciare i tempi di build: si riattivano aggiungendoli
  in `Fitab.App.csproj`.
- **Percorsi lunghi.** Su Windows AAPT2 può fallire con `APT2066 failed parsing
  overlays` quando il percorso supera i 260 caratteri. Se succede: `rm -rf
  src/Fitab.App/obj` e ricompilare; la soluzione definitiva è spostare il
  progetto in un percorso corto e senza spazi (es. `C:\dev\Fitab`).
- **Notifiche push**: fuori scope. Dipendono dal progetto Firebase dell'app
  attuale, che non è nostro.

## Marchi e grafica

| Asset | Origine | Uso |
|---|---|---|
| `wwwroot/img/fitab-logo.png` | marchio ufficiale FITAB-APS, a colori | fondi chiari: accesso, contatti |
| `wwwroot/img/fitab-logo-bianco.png` | stessa versione in bianco | fondi colorati: testata, tessera, avvio |
| `wwwroot/img/aics-logo.png` | marchio AICS | sezione affiliazione nei contatti |
| `Resources/AppIcon/*.svg` | ridisegnati da noi | icona adattiva Android |
| `Resources/Splash/splash.svg` | ridisegnato da noi | schermata di avvio |
| `Components/Comuni/MotivoSemi.razor` | disegno originale | filigrana della testata |

I marchi sono estratti dall'APK ufficiale e **appartengono alla federazione e ad
AICS**, non a noi e nemmeno a Evin srl. Usarli in un APK dimostrativo presentato
alla federazione stessa e' legittimo; per una eventuale pubblicazione serve
l'autorizzazione scritta di FITAB. Le due versioni del logo non vanno mai
ricolorate, deformate o usate come texture: per la decorazione c'e' il motivo
con i semi delle carte, che e' roba nostra.

Icona e splash **non** riproducono l'artwork altrui: sono una stella a cinque
punte inscritta in un cerchio, ridisegnata in SVG, che richiama il simbolo del
marchio restando una forma geometrica generica.

### Palette

Il colore principale e' il **blu del marchio, `#0055A3`**, campionato dal logo
ufficiale FITAB-APS. Il verde dell'app attuale non era un colore istituzionale:
era una scelta dello sviluppatore precedente.

| Token | Valore | Ruolo |
|---|---|---|
| `--primario` | `#0055A3` | interfaccia: barre, azioni, evidenziazioni |
| `--tessera-da` / `--tessera-a` | `#00305E` → `#0055A3` | tessera socio, piu' profonda per staccare dalle schede |
| `--positivo` | `#1B9A3D` | guadagno: posizioni risalite, punti in crescita |
| `--negativo` | `#C62828` | perdita |

Il verde non e' stato buttato: e' diventato il colore semantico del guadagno,
dove nessun altro colore funzionerebbe. Le frecce di variazione nelle classifiche
live restano verdi e rosse indipendentemente dal marchio.

Nel tema scuro i token del primario si schiariscono (`#5AA2E0`) e il testo che ci
sta sopra diventa scuro, per non perdere contrasto. Fanno eccezione **testata
della home e schermata di avvio**, che restano al blu istituzionale in entrambi i
temi: portano il marchio, che esiste solo in bianco e a colori, e su azzurro
chiaro il bianco non reggerebbe. La schermata di avvio Blazor usa esattamente il
colore dello splash nativo dichiarato nel csproj, altrimenti si vedrebbe un lampo
nel passaggio fra i due.

Nessuna regola usa piu' i colori grezzi: cambiare tinta significa toccare
`--blu*` in `tokens.css` e nient'altro.

## Comportamenti dell'app

### Accesso persistente

Tessera e codice fiscale restano nell'archivio cifrato del sistema (Keystore su
Android, Portachiavi su iOS) e vengono ripristinati all'avvio, prima che il
router disegni qualsiasi schermata. I web service FITAB non rilasciano un token:
ogni chiamata personale richiede di nuovo le credenziali, quindi conservarle non
e' una comodita' ma un requisito.

Attenzione: **disinstallare l'app cancella l'archivio**. Aggiornare con
`adb install -r` lo preserva, disinstallare e reinstallare no. Se durante i test
"l'accesso non viene ricordato", la prima cosa da verificare e' se in mezzo c'e'
stata una disinstallazione.

### Tema chiaro/scuro

Tre scelte in *Profilo → Aspetto*: Automatico (segue il telefono), Chiaro, Scuro.
La preferenza sta nelle `Preferences` e scrive l'attributo `data-tema` sulla
radice del documento; i token in `tokens.css` fanno il resto.

Il marchio non si ricolora: esiste in due versioni ufficiali e il componente
`LogoFitab` mostra quella giusta per il tema. Sul tema scuro la versione a
colori, che ha il wordmark nero, sarebbe illeggibile.

### Tornei nella scheda del circolo

La scheda di un circolo mostra i suoi tornei, passati e futuri, con tre filtri a
pastiglia (*In programma*, *Passati*, *Tutti*, ciascuno col proprio conteggio) e,
quando ce n'è più di una, un secondo filtro per rilevanza. I futuri sono ordinati
dal primo che arriva, i passati dal più recente: in *Tutti* si legge come una
linea del tempo che parte da oggi e va all'indietro. Se ne vedono otto, poi c'è
*Mostra tutti*.

Un torneo aperto da qui riporta **al circolo** e non al calendario generale
(`tornei/{anno}/{prog}?daCircolo={codice}`). Se il circolo ha un torneo live in
data odierna, compare in cima alla scheda e porta ai risultati in diretta.

Il calendario storico è una sola chiamata, in cache per 6 ore e condivisa da
tutti i circoli: aprire una scheda dopo l'altra non fa altro traffico.

Anche la pagina **Tornei** guarda all'indietro: il menù del periodo ha *Ultimi
30 giorni*, *Tutti i tornei passati* e *Tutto il calendario* accanto alle voci
sul futuro. Lo storico però pesa dieci volte il calendario dei prossimi tornei,
quindi si scarica **solo alla prima scelta che guarda indietro** — chi apre la
pagina per vedere cosa si gioca sabato non paga niente. Le schede si disegnano
sessanta per volta: i tornei passati sono centinaia.

### I risultati di un torneo

Sono due archivi diversi, e nessuno dei due copre tutto:

| Dove | Cosa c'è | Per quanto |
|---|---|---|
| `LinkRisultati` | classifica finale in PDF, stessa cartella della locandina | per sempre, ma ce l'hanno **24 tornei su 763** |
| classifiche live | risultati turno per turno | **circa una settimana**, poi il torneo sparisce dall'elenco |

I due archivi non hanno una chiave in comune: il calendario ha `anno_progressivo`,
il live un GUID. Si agganciano per **circolo, data e rilevanza** — la rilevanza
serve a non scambiare il torneo federale col torneo sociale che lo stesso circolo
ha giocato quel giorno.

La scheda del torneo mostra quello che c'è (classifica finale, diretta, locandina)
e, quando non c'è niente, lo dice invece di lasciar cercare. Nelle liste — pagina
Tornei e scheda del circolo — i tornei con qualcosa da vedere portano la pastiglia
*Risultati*.

### Precaricamento delle classifiche

All'avvio parte `Precaricamento`, che scarica in background, **una richiesta alla
volta e con due secondi di pausa**:

1. la classifica del circolo del tesserato, se autenticato;
2. le prime 4 pagine della nazionale (i primi 200);
3. le classifiche di tutti i circoli affiliati (~77, meno di un secondo l'una).

Salta tutto cio' che e' gia' in cache e ancora valido, quindi dal secondo avvio
della giornata non fa traffico. Occupa circa 600 KB.

Perche' non l'intera nazionale: sono oltre 6.000 tesserati, cioe' ~120 pagine da
24 secondi — quasi un'ora di richieste. Le pagine oltre le prime restano su
richiesta, e per la domanda che l'utente si pone davvero ("a che punto sono?")
c'e' l'endpoint dedicato, immediato.

## Cose da sapere sul backend

Sono tutte gestite dentro `Fitab.Api`, ma è utile conoscerle:

- i parametri viaggiano negli **header HTTP**, non in query string;
- tutte le chiamate sono **GET**;
- la risposta ha un unico nodo array con nome variabile per endpoint: lo
  individuiamo per forma, non per nome;
- i booleani arrivano in tre formati diversi (`"S"`/`"N"`, `1`/`0`, `-1`/`0`);
- la **classifica nazionale costa ~24 secondi a pagina**, ma filtrata per
  circolo risponde in meno di un secondo: da qui la cache a 24 ore e il
  precaricamento in background delle pagine successive;
- il **calendario senza `DaData` parte da oggi**: per i tornei già giocati va
  chiesta esplicitamente una data indietro nel tempo. Due anni e mezzo sono
  ~760 tornei per ~350 KB e arrivano in mezzo secondo, quindi il filtro per
  circolo lo fa la UI (l'endpoint non lo prevede);
- l'**elenco live tiene solo l'ultima settimana** (verificato: 51 tornei, dal
  giorno prima a otto giorni prima) e non accetta parametri di data — oltre
  quella finestra i risultati turno per turno non esistono più;
- **metà dei circoli ha spam SEO iniettato nel CMS** — vedi sotto.

### Lo spam dentro i campi dei circoli

In `DoveGioca` e `QuandoGioca` di 50 circoli su 77 ci sono blocchi
`<div style="display:none">` con link a siti di terzi e testi osceni, iniettati
nel CMS anni fa (sono visibili nel JSON dei web service, non li abbiamo
introdotti noi). Sul sito e nell'app attuale non si vedono perché il browser
rispetta il `display:none`; la nostra pulizia del markup, invece, toglieva i tag
e portava quel testo in chiaro dentro la scheda del circolo.

`Testo.SenzaHtml` elimina ora il contenuto nascosto **insieme al suo
contenitore**, prima di ogni altra pulizia — il contenuto invisibile non è
contenuto — e la sonda ha un controllo dedicato (`Testi dei circoli`) che
fallisce se dovesse riaffiorare. Vale la pena segnalarlo alla federazione: i
dati sul loro server restano sporchi.

## Compilare per iOS

Il progetto nasce Android + Windows. Per iOS serve riattivare il target e, come
requisito non negoziabile, **una macchina macOS**: la catena di Apple (Xcode,
`codesign`, `actool`) non esiste su Windows.

### 1. Riattivare il target

In `Fitab.App.csproj`:

```xml
<TargetFrameworks>net10.0-android;net10.0-ios</TargetFrameworks>
```

### 2. Procurarsi un Mac (tre strade)

| Strada | Cosa serve | Quando ha senso |
|---|---|---|
| **Mac in locale** | un Mac con Xcode + workload `ios` | la piu' semplice se ce l'hai |
| **Pair to Mac** | Visual Studio su Windows + un Mac in rete con Xcode e `Remote Login` attivo | si continua a sviluppare su Windows, la build gira sul Mac via SSH |
| **macOS a noleggio** | MacStadium, MacinCloud, Scaleway Mac mini, oppure i runner macOS di GitHub Actions / Azure Pipelines | build occasionali e CI, senza comprare hardware |

**Hot Restart** merita una nota: permette di installare una build **di debug** su un
iPhone fisico direttamente da Windows, senza Mac. Basta per far girare la demo su
un iPhone, ma **non produce un `.ipa`** e non serve per distribuire.

### 3. Account Apple

- **Apple Developer Program**, 99 $/anno, intestato alla federazione se l'app sara'
  sua. Serve per installare su dispositivi e per TestFlight.
- Con un Apple ID gratuito si installa su un proprio dispositivo, ma la firma
  **scade dopo 7 giorni** e va rifatta: inutilizzabile per una demo lasciata in mano
  a qualcuno.

### 4. Comandi

Simulatore:

```bash
dotnet build src/Fitab.App/Fitab.App.csproj -f net10.0-ios -t:Run -p:_DeviceName=:v2:udid=<UDID-SIMULATORE>
```

Archivio firmato per la distribuzione (sul Mac):

```bash
dotnet publish src/Fitab.App/Fitab.App.csproj -f net10.0-ios -c Release -p:ArchiveOnBuild=true -p:CodesignKey="Apple Distribution: Nome (TEAMID)" -p:CodesignProvision="Nome del provisioning profile"
```

Il `.ipa` esce in `bin/Release/net10.0-ios/ios-arm64/publish/`.

### 5. Distribuire la demo su iOS

Su iOS non esiste l'equivalente dell'APK da passare via chat: o **TestFlight**
(tester interni subito, esterni dopo una revisione Apple) o **ad hoc**, con gli
UDID dei dispositivi registrati uno per uno nel provisioning profile. Per una
presentazione a poche persone, ad hoc e' piu' rapido.

### 6. Cosa cambia nel nostro codice

Poco, ma non zero:

- **Mappe**: `geo:` e' uno schema solo Android. `AperturaEsterna.CercaSuMappaAsync`
  sceglie gia' lo schema per piattaforma (`maps.apple.com` su iOS, `bingmaps:` su
  Windows). Era un bug latente, sistemato.
- **ATS (App Transport Security)**: iOS blocca il traffico in chiaro. Noi parliamo
  con `https://cms.fitab.it`, quindi siamo a posto — se fossimo rimasti su `http://`
  come l'app attuale, servirebbe un'eccezione in `Info.plist`, che Apple contesta
  in revisione.
- **SecureStorage** usa il Portachiavi: funziona senza configurazione, ma se un
  domani servisse condividerlo con un'estensione va aggiunto un `Entitlements.plist`.
- **Icona**: iOS non ammette trasparenza. MAUI compone il primo piano sul colore di
  sfondo dichiarato in `MauiIcon`, quindi il nostro simbolo funziona anche li'.
- **Aree sicure**: il CSS usa gia' `env(safe-area-inset-*)`, che e' proprio quello
  che serve per il notch e la barra gestuale.
