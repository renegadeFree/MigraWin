# Formato del repository MigraWin 1

Il formato è versionato e indipendente dal database operativo.

```text
repository/
  format.json
  .migrawin.lock
  objects/
    aa/
      <object-id>.mwo
  snapshots/
    <snapshot-id>.snapshot
  transactions/
  reports/
```

## format.json

Contiene solo i dati necessari a riconoscere il formato e sbloccare la chiave:

- `schemaVersion`;
- `repositoryId`;
- data di creazione;
- key slot `password` e `recovery`.

Ogni key slot contiene salt casuale, 600.000 iterazioni PBKDF2-HMAC-SHA256, nonce, chiave master cifrata e tag AES-GCM. La chiave master non viene mai scritta in chiaro.

Il cambio password sostituisce soltanto il key slot `password`; la chiave master e il key slot `recovery` restano invariati. Gli oggetti non vengono riscritti.

## Oggetti

Il nome dell'oggetto è HMAC-SHA256 del contenuto con una chiave derivata dal repository. Questo evita deduplicazione cross-repository e non espone un hash del contenuto in chiaro.

Un oggetto `.mwo` usa blocchi da 4 MiB:

```text
MWO1 | fileId(16) | chunkSize(4) | plainLength(8)
  [chunkLength(4) | GCM tag(16) | ciphertext]
  ...
```

Ogni blocco autentica `fileId`, indice, lunghezza, lunghezza totale e flag finale. Il ripristino verifica l'identità HMAC del contenuto ricostruito e rifiuta blocchi mancanti, riordinati o manomessi.

## Manifest

`snapshots/<id>.snapshot` contiene:

```text
MWM1 | nonce(12) | plainLength(4) | GCM tag(16) | ciphertext
```

Il manifest cifrato descrive:

- identità macchina e profilo;
- snapshot padre;
- applicazioni e versioni;
- componenti, sensibilità e root logiche;
- oggetti, percorsi relativi, dimensioni e timestamp;
- eccezioni e conteggi.

Il repository viene aperto in scrittura in modo esclusivo. Gli oggetti sono immutabili; un manifest viene scritto in un file temporaneo, sincronizzato e poi rinominato. Una transazione interrotta non rende visibile un punto di ripristino parziale.

## Compatibilità

Un lettore che non riconosce `schemaVersion` deve fermarsi prima di scrivere. Versioni future richiedono una migrazione esplicita e un collaudo di compatibilità.
