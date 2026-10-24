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
- avvio del binario self-contained installato, finestra responsiva.
- accessibilità UI: nessun controllo interattivo visibile privo di nome;
- repository pubblico: `https://github.com/renegadeFree/MigraWin`;
- release pubblica: `https://github.com/renegadeFree/MigraWin/releases/tag/v1.4.0`;
- GitHub Action della release completata con esito `success`;
- MSI pubblico scaricato nuovamente e verificato: 59.682.816 byte, SHA-256 `341F93E945C08B43400B0FFF986E430BA924B6C6103A0BA39CCF2B0059B78622`;
- client di aggiornamento installato: release `v1.4.0` riconosciuta come versione corrente.

## Prossimi incrementi

- [ ] Collaudo su una VM Windows 11 pulita distinta da quella di sviluppo.
- [ ] Backup e ripristino interattivi mediante UI su una fixture di applicazione reale.
- [x] Confronto tra snapshot e rapporto differenziale visuale.
- [x] Incapsulamento e importazione dell'archivio autonomo in un unico file.
- [x] Pianificazione protetta tramite Utilità di pianificazione e chiavi DPAPI.
- [ ] Adattatori con controlli di versione e ripristino automatico end-to-end.
- [ ] Firma Authenticode dell'eseguibile e del pacchetto MSI.
- [ ] Verifiche High Contrast, DPI 200% e Windows con effetti di trasparenza disattivati.
- [ ] Ambiente di test dedicato per Store, credenziali e applicazioni già installate.

## Limiti espliciti

Una regola di rilevamento non equivale a un adattatore di migrazione verificato. La versione dichiara solo il livello di supporto acquisito dalle prove disponibili. Gli applicativi non riconosciuti possono essere archiviati tramite cartelle esplicite, ma la reinstallazione automatica resta non dichiarata.

Sono fuori perimetro WSL, container Docker e macchine virtuali.
