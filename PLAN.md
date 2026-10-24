# Piano di sviluppo MigraWin

Aggiornato: 24 ottobre 2026

## Obiettivo

Realizzare un'applicazione Windows installabile per inventariare il software, selezionare componenti, creare backup cifrati verificati e ripristinare selettivamente l'ambiente applicativo su un altro PC.

## Stato verificato

- [x] Specifica analizzata e portata alla versione 1.2.
- [x] Repository Git pubblico `renegadeFree/MigraWin` creato e pubblicato.
- [x] Progetto WPF .NET 10 e progetto di collaudo creato.
- [x] Inventario registro a 32/64 bit e applicazioni Store/MSIX.
- [x] Ricerca, filtri, selezione multipla e dettaglio per componente.
- [x] Profili locali, esportazione e importazione senza segreti.
- [x] Catalogo di adattatori dichiarati per dieci applicativi diffusi.
- [x] Esclusione euristica dei giochi.
- [x] Libreria installer con SHA-256 e rilevamento della firma.
- [x] Oggetti cifrati AES-256-GCM, HMAC per deduplicazione e manifest autenticati.
- [x] Password PBKDF2 e chiave di recupero indipendente.
- [x] Backup incrementale o differenziale, protezione da scritture concorrenti e verifica.
- [x] Conservazione configurabile ed esportazione di un archivio autonomo.
- [x] Cambio password con riprotezione della chiave master.
- [x] Ripristino per applicazione/componente, mappature, conflitti e rollback.
- [x] Rapporti Markdown/JSON e cronologia SQLite.
- [x] Interfaccia Fluent con temi chiaro/scuro, tastiera e ridimensionamento.
- [x] Distribuzione self-contained x64.
- [x] MSI per utente corrente con menu Start e uninstaller.
- [x] Collaudo programmatico di crittografia, deduplicazione, profili, cronologia, ripristino e manomissione.
- [x] Installazione MSI e avvio dell'eseguibile installato verificati.
- [x] Pianificazione protetta con DPAPI e rimozione all'uninstall.
- [x] Esclusione predefinita dei file che possono contenere segreti.
- [x] Esclusioni persistenti per sottocartelle all'interno dei dati applicativi.
- [x] Esportazione e importazione `.migrawin` in un singolo file con estrazione controllata.
- [x] Controllo aggiornamenti, download MSI e verifica SHA-256 da GitHub Releases.
- [x] GitHub Action per build e pubblicazione delle future release con tag `v*`.
- [x] Pipeline di firma Authenticode opzionale per exe e MSI.
- [x] Adattatore Git verificato end-to-end con backup cifrato e ripristino in cartella isolata.
- [x] Adattatore Notepad++ verificato end-to-end con backup cifrato e ripristino isolato.
- [x] Layout verificado a 820×540 con navigazione compatta e scorrimento accessibile.
- [x] Percorso UI Git verificato interattivamente: selezione, backup, conferma, preparazione e ripristino.
- [x] README riorganizzato con obiettivi espliciti, flusso d'uso, perimetro e limiti dichiarati.
- [x] Schermate reali dell'app aggiunte per panoramica, inventario, backup e aggiornamenti.
- [x] Icone locali per le applicazioni, con fallback a monogramma.
- [x] Galleria separata delle immagini promozionali dal sito ufficiale, senza sostituire l'icona.
- [x] Collegamento a sito ufficiale o scheda Microsoft Store quando disponibile.
- [x] Nuova icona originale vettoriale, esportata in più dimensioni per Windows.

## Collaudi eseguiti

```text
OK: cifratura, cambio password, recupero, lock, deduplicazione, incrementale, differenziale, confronto, esportazione .migrawin, conservazione, segreti, esclusioni, profili, SQLite, DPAPI, pianificazione, conflitti, rollback, ripristino e manomissione verificati.
```

Ulteriori verifiche effettuate:

- scansione reale del PC e inventario applicazioni Windows;
- esecuzione delle nove aree dell'interfaccia e cattura del rendering;
- `dotnet list package --vulnerable --include-transitive`: nessun pacchetto vulnerabile;
- build Debug e Release senza errori;
- MSI installato con codice 0;
- avvio del binario self-contained installato dalla release 1.5.0, finestra responsiva.
- accessibilità UI: nessun controllo interattivo visibile privo di nome;
- icone applicazione estratte dal PC e galleria promozionale ufficiale verificate visivamente in Visual Studio Code;
- nuova icona v1.5.0 verificata nell'API, nell'eseguibile e nella finestra installata;
- repository pubblico: `https://github.com/renegadeFree/MigraWin`;
- release pubblica: `https://github.com/renegadeFree/MigraWin/releases/tag/v1.5.0`;
- GitHub Action della release `v1.5.0` completata con esito `success`;
- Windows CI sul commit `0c6f371` completata con esito `success`: build, test, installazione MSI, self-check, menu Start e disinstallazione;
- MSI pubblico v1.5.0 scaricato e verificato: 59.990.016 byte, SHA-256 `7829A41B7A7191410FB16B52D9D10A64F5159FBD2D9F8391FDC68BF2B5FE1678`;
- aggiornamento reale 1.4.0 → 1.4.1 rilevato e scaricato;
- installazione passiva MSI verificata da `Downloads\MigraWinUpdates`;
- release pubblica `v1.5.0` installata con codice 0 e avviata correttamente.
- backup e ripristino Git eseguiti dall'interfaccia reale con contenuto SHA-256 identico prima e dopo.

## Prossimi incrementi

- [ ] Collaudo su una VM Windows 11 pulita distinta da quella di sviluppo.
- [x] Backup e ripristino interattivi mediante UI su una fixture di applicazione reale.
- [x] Confronto tra snapshot e rapporto differenziale visuale.
- [x] Incapsulamento e importazione dell'archivio autonomo in un unico file.
- [x] Pianificazione protetta tramite Utilità di pianificazione e chiavi DPAPI.
- [ ] Altri adattatori applicativi con controlli di versione e ripristino automatico end-to-end.
- [ ] Fornire il certificato Authenticode e pubblicare nuovamente gli artefatti firmati.
- [ ] Verifiche High Contrast, DPI 200% e Windows con effetti di trasparenza disattivati.
- [ ] Ambiente di test dedicato per Store, credenziali e applicazioni già installate.

## Limiti espliciti

Una regola di rilevamento non equivale a un adattatore di migrazione verificato. La versione dichiara solo il livello di supporto acquisito dalle prove disponibili. Gli applicativi non riconosciuti possono essere archiviati tramite cartelle esplicite, ma la reinstallazione automatica resta non dichiarata.

Sono fuori perimetro WSL, container Docker e macchine virtuali.
