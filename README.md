# MigraWin

[![Windows CI](https://github.com/renegadeFree/MigraWin/actions/workflows/ci.yml/badge.svg)](https://github.com/renegadeFree/MigraWin/actions/workflows/ci.yml)

MigraWin è un'applicazione desktop Windows per inventariare il software installato, scegliere componenti da conservare, creare un repository cifrato e ripristinare i contenuti su un altro PC.

## Cosa include questa versione

- inventario del registro a 32/64 bit e dei pacchetti Microsoft Store/MSIX;
- ricerca, filtri, selezione multipla e personalizzazione per applicazione;
- aggiunta manuale di eseguibili portabili;
- esclusione dei giochi e supporto esplicito agli applicativi riconosciuti;
- profili esportabili e importabili senza credenziali;
- libreria di installer locali con SHA-256 e rilevamento della firma;
- repository deduplicato con oggetti cifrati AES-256-GCM, manifest autenticati e chiave di recupero;
- cambio password senza riscrivere gli oggetti cifrati;
- backup incrementale o differenziale, verifica degli oggetti e conservazione condivisa;
- politica di conservazione configurabile ed esportazione/importazione di un archivio `.migrawin` autonomo;
- confronto visuale tra snapshot con rapporto Markdown;
- pianificazione giornaliera con credenziale protetta tramite DPAPI;
- ripristino selettivo per applicazione e componente;
- mappatura dei percorsi, tre politiche di conflitto e copia preventiva per il rollback;
- rapporti Markdown e JSON, cronologia persistente SQLite;
- interfaccia Fluent chiara/scura, navigazione da tastiera e distribuzione MSI x64 self-contained.

## Avvio

L'installer è generato in:

```text
artifacts\installer\MigraWin-1.4.2-x64.msi
```

L'installazione è per l'utente corrente e non richiede privilegi amministrativi. L'app viene installata in `%LOCALAPPDATA%\Programs\MigraWin`.

L'app non contiene account cloud né telemetria. L'analisi e i backup operano localmente.

## Uso essenziale

1. Apri **Inventario** e scegli **Analizza PC**.
2. Seleziona applicazioni e componenti; i giochi restano esclusi.
3. Apri **Backup**, scegli un profilo, la destinazione e una password di almeno 10 caratteri.
4. Al primo backup conserva la chiave di recupero fuori dal disco del repository.
5. Apri **Archivio** sul nuovo PC, seleziona il file `.migrawin` o la cartella e inserisci la password o la chiave di recupero.
6. Prepara il ripristino, controlla mappature e conflitti, quindi avvia.

Il manuale completo è in [docs/MANUALE.md](docs/MANUALE.md).

## Supporto dichiarato

Il catalogo incluso riconosce configurazioni specifiche per Visual Studio Code, Windows Terminal, Git, PowerShell, Notepad++, OBS Studio, Paint.NET, LibreOffice, FileZilla e Sublime Text. Le altre applicazioni restano rilevabili: file e cartelle personalizzate possono essere archiviati, senza dichiarare automaticamente una reinstallazione o migrazione verificata.

I videogiochi non vengono inclusi automaticamente. Un installer importato soddisfa il requisito offline solo per quel determinato applicativo e file.

## Sviluppo e build

Requisiti di build:

- Windows 11 x64;
- .NET 10 SDK;
- PowerShell.

Con il runtime locale in `.dotnet`:

```powershell
.\build.ps1
```

Oppure, con .NET 10 SDK nel `PATH`:

```powershell
dotnet tool restore
dotnet publish src\MigraWin\MigraWin.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o artifacts\publish
dotnet tool run wix extension add WixToolset.UI.wixext/6.0.2
dotnet tool run wix build installer\MigraWin.wxs -ext WixToolset.UI.wixext -arch x64 -o artifacts\installer\MigraWin-1.4.2-x64.msi
```

## Collaudi

```powershell
dotnet run --project tests\MigraWin.Tests\MigraWin.Tests.csproj
```

Il collaudo verifica cambio password, password errata, chiave di recupero, lock concorrente, deduplicazione, modalità differenziale, confronto, archivi autonomi, conservazione, esclusione dei segreti, profili, cronologia SQLite, DPAPI, creazione e rimozione dell'attività pianificata, esecuzione headless e rifiuto di oggetti o percorsi manomessi.

## Aggiornamenti

In **Impostazioni** il programma confronta la versione installata con l'ultima release pubblica GitHub. Un aggiornamento può essere scaricato solo dopo conferma; MigraWin verifica l'MSI tramite `SHA256SUMS.txt`, avvia l'installer e si chiude.

Gli installer aggiornati vengono conservati in `Downloads\MigraWinUpdates`.

## Firma dei rilasci

Impostando `MIGRAWIN_SIGN_PFX` e `MIGRAWIN_SIGN_PFX_PASSWORD`, oppure `MIGRAWIN_SIGN_CERT_THUMBPRINT`, `build.ps1` firma e verifica Authenticode sia `MigraWin.exe` sia il pacchetto MSI. La GitHub Action usa automaticamente i secret `MIGRAWIN_SIGN_PFX_BASE64` e `MIGRAWIN_SIGN_PFX_PASSWORD` quando configurati. Senza un certificato valido la build resta possibile, ma la release è dichiarata non firmata.

## Limiti noti

- Ripristino e acquisizione sono collaudati su Windows 11 x64; Windows ARM64 e Windows 10 non sono dichiarati supportati.
- Le credenziali non vengono estratte o trasferite automaticamente. Alcuni applicativi richiederanno una nuova autenticazione.
- Le copie integrali di `Program Files`, `WindowsApps` e del registro non sono considerate reinstallazioni portabili.
- Gli installer di terze parti non sono firmati da MigraWin e devono essere importati da una fonte attendibile.
- Il pacchetto MSI non è firmato con un certificato di code signing di distribuzione.
- Trasferimento diretto PC-PC e VSS avanzato non fanno parte di questa consegna.
- WSL, container Docker e macchine virtuali sono esplicitamente fuori dal perimetro del prodotto.
