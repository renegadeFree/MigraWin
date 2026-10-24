# Changelog

## 1.7.1 — 24 ottobre 2026

- n8n abilita per impostazione iniziale il backup del package npm installato;
- configurazione n8n separata dai dati e disponibile nel componente impostazioni;
- profilo completo include i file installati degli strumenti personali dotati di cartella applicativa;
- database WAL, workflow, storage e moduli dei nodi personalizzati vengono inclusi nei dati n8n.

## 1.7.0 — 24 ottobre 2026

- icona fornita dall'utente modernizzata con simbolo bianco su gradiente blu-verde e nodi luminosi;
- backup n8n esteso al package npm completo, database, workflow, storage e nodi personalizzati;
- configurazione n8n protetta e dump PM2 inclusi come componenti sensibili;
- rilevamento esplicito della presenza o assenza del processo n8n nel dump PM2.

## 1.6.1 — 24 ottobre 2026

- nuova icona fornita dall'utente, convertita in un ICO multi-risoluzione senza modificarne il simbolo;
- collegamento MigraWin creato automaticamente sul Desktop durante l'installazione;
- collaudo CI esteso alla presenza e alla rimozione del collegamento Desktop.

## 1.6.0 — 24 ottobre 2026

- rilevamento automatico di script personali nella cartella utente;
- riconoscimento di Enter for rename, PM2, n8n, Pi Agent e del proxy DeepSeek per Codex;
- inclusione selettiva di script, configurazioni, database e profili locali con protezione predefinita dei segreti;
- nuova icona MigraWin ispirata alla migrazione tra due ambienti, con forme essenziali e leggibilità alle dimensioni Windows.

## 1.5.0 — 24 ottobre 2026

- aggiunte le icone delle applicazioni nell'inventario, estratte da eseguibili, file ICO e manifest MSIX;
- immagini promozionali separate dall'icona e mostrate in una galleria nella scheda dell'app;
- collegamento alla pagina ufficiale o alla scheda Microsoft Store quando disponibile;
- nuova identità visiva originale per applicazione, finestra, menu Start e installer;
- versione dell'MSI ricavata automaticamente dal progetto.

## 1.4.4 — 24 ottobre 2026

- verifica aggiornamenti tramite la pagina pubblica GitHub Releases;
- rimozione della dipendenza dal limite anonimo delle API GitHub;
- fallback trasparente quando il controllo non è disponibile.

## 1.4.3 — 24 ottobre 2026

- barra laterale compatta automatica nelle finestre strette;
- layout verificato a 820×540 con scorrimento orizzontale di sicurezza;
- navigazione accessibile tramite icone, tooltip e nomi UI Automation;
- pipeline di firma Authenticode opzionale integrata nel build e nella release.

## 1.4.2 — 24 ottobre 2026

- download degli aggiornamenti spostato in `Downloads\MigraWinUpdates`;
- maggiore compatibilità con Windows Installer in contesti con token limitato;
- installazione degli aggiornamenti mantenuta tramite `msiexec`.

## 1.4.1 — 24 ottobre 2026

- corretto il rilevamento dell'adattatore Git;
- backup e ripristino della configurazione Git verificati end-to-end;
- l'aggiornamento integrato avvia MSI con installazione passiva.

## 1.4.0 — 24 ottobre 2026

Prima release pubblica.

- inventario applicazioni Windows e Microsoft Store;
- selezione per applicazione e componente;
- profili locali importabili ed esportabili;
- libreria installer con hash e rilevamento della firma;
- repository cifrato con deduplicazione, recupero e verifica;
- backup completo, incrementale e differenziale;
- archivio autonomo `.migrawin`;
- confronto tra snapshot;
- ripristino selettivo, mappature, conflitti e rollback;
- pianificazione giornaliera protetta con DPAPI;
- esclusione dei file che possono contenere segreti;
- rapporti e cronologia SQLite;
- interfaccia Fluent accessibile;
- installer MSI x64 self-contained;
- aggiornamenti tramite GitHub Releases.
