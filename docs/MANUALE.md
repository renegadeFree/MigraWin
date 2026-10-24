# Manuale MigraWin

## 1. Prima analisi

Apri **Inventario** e scegli **Analizza PC**. MigraWin legge il registro delle disinstallazioni e, se abilitati, i metadati dei pacchetti Microsoft Store/MSIX. La scansione non avvia installer, script o programmi trovati.

Le applicazioni sono distinte per identità dell'installazione. Controlla sempre nome, produttore, versione, origine e livello di supporto prima di selezionare.

La lista mostra l'icona associata all'eseguibile o al pacchetto installato. Se l'icona non è leggibile, viene mostrato un monogramma; l'assenza dell'immagine non blocca inventario, backup o ripristino.

Per un software portabile non rilevato, scegli **Aggiungi portabile** e seleziona il relativo eseguibile. La cartella viene proposta come dato da acquisire; nessun programma viene avviato.

## 2. Selezione e componenti

Puoi selezionare più applicazioni con le caselle e personalizzare ciascuna dal pannello di destra:

- impostazioni e preferenze;
- dati, progetti e cartelle aggiuntive;
- installer offline associato;
- copia archivistica dei file installati;
- cache e file temporanei.

Quando MigraWin dispone di una pagina ufficiale o di una scheda Microsoft Store, il dettaglio mostra **Apri sito ufficiale**. Se la pagina espone un'immagine promozionale, questa compare in una galleria separata con etichetta **Dal sito ufficiale**. Le immagini prodotto non sostituiscono mai l'icona dell'applicazione.

La copia dei file installati non equivale a un installer. Quando un'applicazione contiene possibili credenziali, il componente viene indicato come sensibile.

I file di configurazione che contengono valori associati a password, token, chiavi API o chiavi private vengono esclusi per impostazione iniziale. Puoi autorizzarne l'acquisizione con **Includi configurazioni che possono contenere segreti**; il file resta cifrato e nel ripristino il componente sensibile non è preselezionato.

Le cartelle aggiunte come esclusioni persistenti vengono applicate a ogni backup di quella installazione. Usa **Escludi cartella…** per cache, repository locali, build temporanee o altri sottotrees che non vuoi trasferire.

I giochi confermati sono esclusi. Non puoi abilitarli dalla casella di selezione.

## 3. Profili

Un profilo registra le installazioni selezionate, i componenti, la destinazione e la modalità. Puoi salvarlo, caricarlo, esportarlo e importarlo dall'inventario. Password e chiavi non sono incluse.

Un profilo importato su un altro PC richiede di nuovo la password o la chiave di recupero del repository.

## 4. Backup

1. Scegli un preset o una selezione personalizzata.
2. Indica nome del profilo e cartella del repository.
3. Inserisci e conferma una password di almeno 10 caratteri.
4. Scegli modalità incrementale, differenziale o completa logica.
5. Scegli quanti punti di ripristino conservare automaticamente.
6. Seleziona **Crea e verifica backup**.

MigraWin scrive gli oggetti in un'area temporanea, verifica il contenuto e pubblica il manifest solo dopo il completamento. Le copie precedenti non vengono invalidate da una scrittura interrotta.

### Chiave di recupero

Alla creazione di un nuovo repository viene mostrata una chiave di recupero. Copiala e conservala fuori dal disco del repository. Password e chiave aprono la stessa chiave dei dati, ma la chiave di recupero serve se la password viene dimenticata.

La perdita di tutte le chiavi rende i contenuti non recuperabili. MigraWin non può aggirare questo vincolo.

### Modalità differenziale

La modalità differenziale usa come base lo snapshot più recente del repository. Ogni manifest continua a descrivere lo stato completo scelto e referenzia tutti gli oggetti necessari; la deduplicazione evita di duplicare fisicamente i contenuti invariati.

### Backup giornaliero

Puoi attivare una pianificazione nell'area **Backup** e impostare l'ora locale in formato `HH:mm`. Dopo un backup riuscito, l'app crea un'attività nell'Utilità di pianificazione di Windows.

La password del repository viene cifrata con DPAPI per l'utente Windows corrente. Non viene salvata nel profilo, nel registro dell'attività o nei rapporti. La chiave di recupero resta il metodo portabile di accesso e non deve essere conservata sul disco del backup.

La pianificazione richiede che l'utente Windows che l'ha creata sia disponibile. Gli esiti vengono scritti in `%LOCALAPPDATA%\MigraWin\Schedules\last-run.log`, nella cronologia e nei rapporti.

Per rimuovere una pianificazione senza creare un altro backup, seleziona il profilo nell'inventario e scegli **Rimuovi pianificazione**. La disinstallazione rimuove attività e credenziali protette, lasciando intatti profili e repository.

## 5. Libreria pacchetti

Importa un installer posseduto dalla libreria **Pacchetti**. MigraWin calcola SHA-256 e rileva la presenza di una firma, senza dichiarare automaticamente attendibile il produttore.

Seleziona un'applicazione nell'inventario e associa il pacchetto. Il pacchetto viene incluso nel backup solo quando il relativo componente è selezionato.

## 6. Archivio

Inserisci percorso e password o chiave di recupero e scegli **Apri archivio**. Puoi:

- elencare i punti di ripristino;
- verificare crittograficamente gli oggetti;
- preparare un ripristino;
- esportare una copia autonoma `.migrawin` contenente solo gli oggetti della copia scelta;
- importare un file `.migrawin` con estrazione controllata in una cache locale;
- cambiare la password del repository mantenendo valida la chiave di recupero;
- eliminare un backup conservando gli oggetti usati da altre copie.

Per cambiare password, il repository deve essere aperto e la password o chiave corrente deve essere ancora disponibile nel campo di apertura. La nuova password protegge la stessa chiave master; gli oggetti non vengono riscritti. Le pianificazioni collegate allo stesso repository ricevono una nuova credenziale DPAPI.

Per trasferire un solo punto di ripristino, esporta un archivio `.migrawin`. Sul nuovo PC usa **Apri file…**, inserisci la password o la chiave e apri il repository estratto. MigraWin rifiuta percorsi che escono dalla cartella di importazione, collegamenti simbolici e archivi con struttura non valida; se la chiave è già disponibile, verifica anche tutti gli oggetti.

## 7. Ripristino

Dopo **Prepara il ripristino**:

1. deseleziona applicazioni e componenti che non vuoi ripristinare;
2. controlla ogni mappatura dei percorsi;
3. scegli la politica per i file già presenti;
4. decide se avviare gli installer ripristinati;
5. rianalizza i conflitti;
6. conferma il ripristino.

Politiche disponibili:

- **Sostituisci con copia preventiva**: il file esistente viene copiato nel rollback;
- **Mantieni i file esistenti**: il file di destinazione non viene toccato;
- **Salva entrambi**: il nuovo file riceve un nome progressivo.

Le operazioni esterne, come installer e attivazioni, possono non essere interamente annullabili.

## 8. Rapporti e cronologia

Ogni backup e ripristino produce Markdown e JSON in `Documenti\MigraWin\Reports`. La cronologia locale è in SQLite sotto `%LOCALAPPDATA%\MigraWin`.

I report rimuovono i percorsi personali noti e non includono password o chiavi.

## 9. Confronto tra copie

Apri il repository, entra in **Confronto** e scegli i due punti di ripristino. Il confronto distingue:

- applicazioni aggiunte o rimosse;
- versioni, architetture e livelli di supporto cambiati;
- file aggiunti, rimossi o modificati;
- presenza di contenuti equivalenti.

Il risultato viene salvato nella cartella dei rapporti in Markdown. Una voce non più presente descrive la differenza tra due snapshot e non prova da sola una cancellazione successiva sul PC.

## 10. Recupero

Se un backup è stato interrotto:

1. non cancellare la cartella del repository;
2. riapri il repository con la chiave corretta;
3. verifica l'ultimo punto di ripristino;
4. elimina solo copie non più necessarie.

Gli oggetti temporanei non raggiungibili dal manifest possono essere rimossi dalla pulizia della conservazione, ma non sostituiscono una verifica riuscita.

## 11. Aggiornamenti

In **Impostazioni → Aggiornamenti** puoi controllare l'ultima release pubblica di MigraWin. Il programma confronta la versione installata con il tag GitHub e mostra il risultato senza modificare automaticamente l'applicazione.

**Scarica e installa** richiede una conferma, scarica l'MSI, verifica SHA-256 usando `SHA256SUMS.txt` e avvia l'installer di Windows. Il download incompleto o con hash diverso viene eliminato.

Il pacchetto verificato viene conservato in `Downloads\MigraWinUpdates`, così Windows Installer può accedervi anche quando l'app è avviata con un token limitato.
