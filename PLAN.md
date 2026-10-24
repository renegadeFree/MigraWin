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
- [x] Identità v1.6.0 semplificata secondo le linee guida Microsoft e Apple per icone leggibili a 16 px.
- [x] Rilevamento di script personali nella radice del profilo utente con filtro dedicato.
- [x] Riconoscimento end-to-end dei percorsi per Enter for rename, PM2, n8n, Pi Agent e proxy DeepSeek per Codex.
- [x] Inclusione selettiva di configurazioni e database locali, mantenendo l'esclusione predefinita dei segreti.
- [x] Icona fornita dall'utente integrata come sorgente immutata e convertita in ICO multi-risoluzione.
- [x] Collegamento Desktop creato dall'MSI per l'utente corrente e rimosso alla disinstallazione.
- [x] Icona fornita dall'utente modernizzata mantenendo il simbolo originale, con nodi luminosi e fondo blu-verde.
- [x] n8n include package npm completo, database, workflow, storage e moduli dei nodi personalizzati.
- [x] Dump PM2 e configurazione n8n sono classificati come componenti sensibili con attivazione esplicita.
- [x] La scheda n8n verifica la presenza del processo nel dump PM2 senza assumerne l'esistenza.
- [x] n8n porta attivo il backup del package installato e include database/WAL, storage e nodi personalizzati.
- [x] Configurazione n8n spostata nel componente impostazioni e profilo completo esteso agli strumenti personali installati.
- [x] Rilevato e descritto l'avvio n8n tramite `avvia_n8n.vbs`, mantenendo il ripristino come file non eseguito automaticamente.
- [x] Aggiunti predefiniti globali persistenti per impostazioni, dati e copia archivistica dei file installati.
- [x] I predefiniti vengono applicati alle nuove scansioni e ai programmi portabili, con salvataggio JSON atomico.
- [x] Aggiunta la scelta della lingua con 11 localizzazioni e cambio immediato a runtime.
- [x] Persistita la lingua in `settings.json` e aggiunto fallback inglese per i testi tecnici.
- [x] Aggiunto separatore accessibile per ridimensionare il pannello dettagli tra 280 e 620 pixel.
- [x] Corretto lo scorrimento orizzontale che tagliava il riquadro selezionato e i suoi angoli.
- [x] Nilesoft Shell include tutti i file `.nss`, backup e script in `imports`.
- [x] ExplorerBlurMica include DLL, configurazione e comandi di registrazione.

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
- avvio del binario self-contained installato dalla release 1.9.0, finestra responsiva.
- accessibilità UI: nessun controllo interattivo visibile privo di nome;
- icone applicazione estratte dal PC e galleria promozionale ufficiale verificate visivamente in Visual Studio Code;
- icona fornita e modernizzata verificata nell'API, nell'eseguibile e nella finestra installata;
- repository pubblico: `https://github.com/renegadeFree/MigraWin`;
- release pubblica: `https://github.com/renegadeFree/MigraWin/releases/tag/v1.9.0`;
- GitHub Action della release `v1.9.0` completata con esito `success`;
- Windows CI sul commit `5aa67ce` completata con esito `success`: build, test, installazione MSI, self-check, menu Start, collegamento Desktop e disinstallazione;
- MSI pubblico v1.9.0 scaricato e verificato: 60.080.128 byte, SHA-256 `B425A8D510ADF3E18227D3E860FAED544F05B9632B8870D0D5F82F3BDDBC92AD`;
- aggiornamento reale 1.4.0 → 1.4.1 rilevato e scaricato;
- installazione passiva MSI verificata da `Downloads\MigraWinUpdates`;
- release pubblica `v1.9.0` installata con codice 0 e avviata correttamente;
- collegamento Desktop verificato prima dell'installazione, dopo l'installazione e dopo la disinstallazione;
- scansione reale con applicazioni e strumenti, inclusi script personali e configurazioni locali;
- strumenti verificati nell'inventario: Enter for rename, PM2, n8n, Pi Agent e Proxy DeepSeek per Codex;
- n8n verificato con package da circa 1 GB, database/WAL, storage, nodi personalizzati, wrapper CLI e dump PM2 nel piano di backup;
- avvio n8n tramite `avvia_n8n.vbs` rilevato e descritto nell'interfaccia;
- assenza del processo n8n nel dump PM2 corrente rilevata e mostrata esplicitamente.
- predefiniti globali verificati nell'interfaccia con impostazioni, dati e copia dei file installati attivi;
- persistenza di `settings.json` verificata dal test automatico.
- cambio lingua verificato a runtime in inglese, cinese, giapponese, coreano, spagnolo, turco, russo, greco, francese e tedesco;
- ritorno all'italiano e persistenza della scelta in `settings.json` verificati.
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
