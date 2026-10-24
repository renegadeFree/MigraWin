# Changelog

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
