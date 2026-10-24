# Specifiche del programma di backup e migrazione Windows

Versione del documento: 1.2
Data: 24 ottobre 2026
Destinatari: sviluppatori, responsabile del prodotto e collaudatori
Formato: requisiti funzionali, architettura e istruzioni di realizzazione

Modifiche della revisione 1.1: aggiunti livelli di supporto verificabili, regole esplicite per la pubblicazione e il recupero del repository, una prima consegna tecnica end-to-end e collaudi per ripristino interrotto e scritture concorrenti.

Modifiche della revisione 1.2: WSL, container Docker e macchine virtuali sono esclusi dal perimetro del prodotto.

## 1. Scopo del programma

Realizzare un applicativo desktop Windows che analizzi il computer, individui il software e gli ambienti di lavoro presenti e permetta all'utente di scegliere cosa salvare, come salvarlo e come ricostruirlo su un altro PC.

Il programma deve gestire l'intero percorso: inventario, selezione, piano di backup, acquisizione, conservazione, verifica, trasferimento, reinstallazione, importazione delle configurazioni e controllo del risultato.

L'obiettivo è la migrazione dell'ambiente applicativo: versioni dei programmi, pacchetti installabili, dati, opzioni, estensioni, script, dipendenze e integrazioni supportate. I videogiochi devono essere esclusi.

La selezione dei programmi e del tipo di backup è una funzione centrale del prodotto. L'utente deve poter creare un backup molto piccolo delle sole preferenze oppure una migrazione ampia con installer, dati, componenti e ambienti collegati.

Le parole **deve** e **non deve** indicano requisiti obbligatori. Le funzioni assegnate alle fasi successive restano parte del progetto complessivo, ma non devono essere dichiarate disponibili prima di essere implementate e collaudate.

## 2. Principi obbligatori

1. **Scelta esplicita:** nessun backup dei contenuti prima della revisione della selezione. L'inventario preliminare raccoglie i metadati necessari a presentare la scelta.
2. **Selezione per programma e per componente:** salvare un programma non significa includere automaticamente tutti i suoi dati e accessi.
3. **Versione esatta come preferenza iniziale:** non sostituire silenziosamente la versione installata con quella più recente.
4. **Copertura trasparente:** distinguere rilevato, selezionato, acquisito, verificato, ripristinato e non trasferibile.
5. **Originale protetto:** il backup non disinstalla software, non modifica preferenze e non interrompe applicazioni senza un'azione prevista nel piano approvato.
6. **Ripristino pianificato:** mostrare in anticipo le modifiche al PC di destinazione e i conflitti rilevati.
7. **Archivio autonomo:** il backup deve essere utilizzabile senza il vecchio PC e senza il suo database operativo.
8. **Operazioni riprendibili:** interruzioni, errori e riavvii non devono obbligare a ricominciare tutto.
9. **Segreti separati:** password, token, chiavi e dati equivalenti richiedono regole specifiche.
10. **Videogiochi esclusi:** nessuna inclusione automatica dei loro eseguibili, contenuti, salvataggi o mod.
11. **Nessuna promessa universale falsa:** copia archivistica e reinstallazione funzionante sono risultati diversi.
12. **Funzionamento locale:** l'analisi e il backup non richiedono l'invio del contenuto del computer a servizi esterni.

## 3. Confini della migrazione

Il prodotto deve ricostruire gli applicativi su un'installazione Windows già utilizzabile. Non deve richiedere la clonazione del sistema operativo come metodo ordinario.

Una copia di `Program Files`, di `WindowsApps` o dell'intero registro non costituisce da sola un pacchetto di reinstallazione. Anche un file MSI presente nella cache di Windows non deve essere classificato automaticamente come installer completo.

Non è garantita la migrazione automatica di:

- credenziali e chiavi legate al dispositivo o non esportabili;
- attivazioni associate al vecchio hardware;
- versioni ritirate per le quali non esiste più un pacchetto recuperabile;
- driver e componenti incompatibili con il nuovo hardware;
- stato interno di programmi sconosciuti o privi di procedure di esportazione;
- dati non scaricati localmente, volumi assenti o percorsi non accessibili;
- contenuti esterni al PC che richiedono un accesso al servizio originale.

Ogni limite deve diventare un elemento visibile nel piano e nel rapporto, con un'azione concreta quando disponibile. La semplice presenza di file nell'archivio non autorizza l'etichetta «migrazione completa».

Un'eventuale immagine del disco è una funzione distinta di recupero della macchina originale. Non sostituisce la migrazione per applicativo e non garantisce l'avvio su hardware diverso.

### 3.1 Livelli di supporto

Lo stato di un applicativo deve essere espresso con uno dei seguenti livelli, senza usare «supportato» come termine ambiguo:

| Livello | Significato | Prova minima |
| --- | --- | --- |
| Rilevato | Identità e installazione sono state osservate | Fonte, data e osservazioni registrate |
| Archiviabile | I file selezionati possono essere copiati e verificati | Hash o confronto dell'acquisizione |
| Reinstallabile online | È disponibile un metodo di reinstallazione che richiede rete o account | Piano di reinstallazione e fonte verificata |
| Reinstallabile offline | Installer o distribuzione necessari sono presenti e verificati | Ripristino senza rete in ambiente di prova |
| Migrazione verificata | Installazione, componenti e configurazioni selezionati sono stati ripristinati e controllati | Collaudo end-to-end dell'adattatore e rapporto |

I livelli sono cumulativi soltanto quando ogni passaggio richiesto è stato verificato. Un'applicativo archiviabile non è automaticamente reinstallabile. Il livello dichiarato deve riferirsi al perimetro effettivamente selezionato.

## 4. Piattaforme e distribuzione

La prima versione deve supportare Windows 11 x64 come sorgente e destinazione, specificando le build effettivamente collaudate nelle note di rilascio. Il programma distribuito deve includere il runtime necessario per avviarsi offline.

Il progetto deve prevedere successivamente:

- acquisizione da Windows 10, con compatibilità verificata separatamente;
- esecuzione nativa su Windows ARM64;
- analisi delle migrazioni tra architetture differenti;
- più utenti locali e account aziendali, senza presumere di poter esportare qualsiasi loro segreto;
- sistemi gestiti dall'organizzazione, rispettandone i vincoli.

Applicativo a 32 bit, applicativo x64 e applicativo ARM64 devono essere identificati correttamente anche quando presenti sullo stesso PC. La compatibilità del programma di migrazione e quella del software migrato sono due verifiche separate.

L'interfaccia deve essere inizialmente in italiano. Testi e messaggi devono essere separati dalla logica per consentire altre lingue.

## 5. Percorso principale dell'utente

### 5.1 Creazione di un backup

1. Aprire il programma e scegliere **Analizza questo PC**.
2. Selezionare utenti e volumi da analizzare; proporre inizialmente l'utente corrente e i volumi locali accessibili.
3. Eseguire l'inventario e mostrare separatamente elementi riconosciuti, sconosciuti, esclusi e non accessibili.
4. Scegliere i programmi, anche mediante ricerca, filtri e selezione multipla.
5. Assegnare un profilo di backup e personalizzarlo per ogni programma.
6. Risolvere dipendenze, elementi dubbi, file in uso e componenti non trasferibili.
7. Scegliere destinazione, cifratura, modalità temporale e conservazione.
8. Esaminare l'anteprima con contenuti inclusi, esclusi, dimensioni stimate e limiti.
9. Avviare il backup con un comando esplicito.
10. Consultare verifica dell'archivio e rapporto finale.

La scansione non deve avviare installer, script scoperti o programmi trovati sul disco.

### 5.2 Migrazione su un altro computer

1. Avviare il programma sul nuovo PC e aprire l'archivio.
2. Sbloccarlo e verificarne struttura e integrità.
3. Analizzare la destinazione senza modificarla.
4. Scegliere quali programmi e componenti ripristinare, anche se il backup ne contiene di più.
5. Associare utenti, directory e dischi originali alle nuove destinazioni.
6. Risolvere versioni, conflitti, dipendenze e accessi necessari.
7. Esaminare il piano concreto delle modifiche.
8. Avviare il ripristino, completare gli eventuali passaggi interattivi e riprendere dopo i riavvii.
9. Consultare la verifica e l'elenco delle attività residue.

### 5.3 Gestione continuativa

L'utente deve poter salvare profili riutilizzabili, eseguire nuove acquisizioni, confrontare copie, modificare le pianificazioni e ripristinare singoli componenti senza avviare una migrazione completa.

## 6. Scelta dei programmi

La lista degli applicativi deve consentire:

- selezione e deselezione singola;
- selezione multipla e applicazione dello stesso profilo;
- selezione di tutti i risultati filtrati, dichiarandone il numero;
- filtri per nome, produttore, origine, utente, categoria, architettura e stato di supporto;
- filtri per disponibilità della versione esatta e dell'installer offline;
- ordinamento per dimensione stimata, presenza di dati e problemi;
- selezione distinta di installazioni affiancate o appartenenti a utenti diversi;
- aggiunta manuale di software portabile, script e cartelle non riconosciute;
- esclusioni persistenti per programma, componente, percorso o categoria;
- salvataggio e riutilizzo della selezione.

Una selezione deve avere un'identità stabile: non può dipendere soltanto dal nome visualizzato. Se un programma selezionato non è più presente, il nuovo piano lo indica come assente; non deve associarlo arbitrariamente a un altro prodotto dal nome simile.

I programmi scoperti dopo la creazione di un profilo non devono essere inclusi automaticamente. Un profilo può abilitare espressamente l'inclusione futura di applicativi appartenenti a un gruppo definito; giochi, segreti e componenti non riconosciuti mantengono comunque le rispettive regole restrittive.

## 7. Tipi di backup e composizione dei contenuti

Il programma deve separare tre scelte indipendenti:

1. **Contenuto:** quali parti dell'applicativo salvare.
2. **Acquisizione del software:** come conservare o recuperare la versione da installare.
3. **Modalità temporale:** copia completa, incrementale o differenziale.

L'interfaccia non deve confondere, per esempio, «solo impostazioni» con «incrementale».

### 7.1 Profili di contenuto

| Profilo | Contenuto previsto | Uso principale |
| --- | --- | --- |
| Solo inventario | Identità, versione, riferimenti ufficiali e rapporto | Documentare l'ambiente senza acquisire dati applicativi |
| Solo impostazioni | Preferenze, configurazioni e integrazioni supportate | Ritrovare le opzioni dopo una reinstallazione |
| Solo dati | Progetti, archivi e database selezionati | Conservare il lavoro prodotto |
| Solo pacchetto installabile | Installer completo o distribuzione portabile con i metadati necessari | Conservare la versione del software |
| Impostazioni e dati | Configurazioni e lavoro dell'utente | Usare un installer ottenuto separatamente |
| Migrazione completa supportata | Software recuperabile, impostazioni, dati, componenti e dipendenze supportati | Ricostruire l'ambiente sul nuovo PC |
| Personalizzato | Combinazione esplicita dei singoli componenti | Controllo completo dell'acquisizione |

«Migrazione completa supportata» deve mostrare le eccezioni. Non include automaticamente segreti, giochi o dati non classificati. Se l'installer non può essere acquisito, il piano deve riportare che il profilo richiesto non è completamente soddisfatto.

### 7.2 Componenti selezionabili

Per ogni programma devono essere disponibili, quando pertinenti:

- pacchetto installabile e versione;
- distribuzione portabile verificata;
- copia archivistica dei file installati, esplicitamente distinta dal pacchetto installabile;
- configurazioni e preferenze;
- dati, progetti e database;
- plugin, estensioni, moduli e relativi installer;
- preset, template, macro, font e risorse aggiuntive;
- script, interpreti e manifest delle dipendenze;
- collegamenti, argomenti di avvio e directory di lavoro;
- variabili d'ambiente e integrazioni Windows supportate;
- procedure supportate di esportazione degli accessi;
- cache, log e file temporanei come opzione avanzata, inizialmente esclusa.

Una componente non supportata deve essere disabilitata con una spiegazione; non deve apparire selezionabile per poi essere ignorata.

Ogni voce deve mostrare dimensione stimata, percorso o origine, livello di supporto e metodo di ripristino. L'utente deve poter aggiungere o escludere sottoinsiemi di dati senza cambiare il profilo degli altri programmi.

### 7.3 Modalità di acquisizione del software

| Modalità | Contenuto conservato | Condizione |
| --- | --- | --- |
| Installer offline esatto | Pacchetto completo della versione, dipendenze e metadati di verifica | Versione recuperabile e pacchetto utilizzabile |
| Riferimento online esatto | Identità, versione e collegamento ufficiale | Il pacchetto non viene archiviato; serve rete al ripristino |
| Pagina ufficiale del prodotto | Identità e pagina del produttore o Store | La disponibilità della versione esatta resta non garantita |
| Copia portabile | Distribuzione e stato selezionato | Portabilità verificata dall'adattatore |
| Copia archivistica | File installati e metadati | Nessuna garanzia di reinstallazione derivante dalla sola copia |

L'utente deve poter richiedere **solo programmi ripristinabili offline** oppure accettare un backup misto. Il programma deve mostrare quali elementi richiederanno rete, account o intervento manuale.

### 7.4 Modalità temporali

| Modalità | Comportamento | Dipendenze di conservazione |
| --- | --- | --- |
| Completo autonomo | Tutti gli oggetti necessari sono presenti nel repository esportato | Ripristinabile senza repository esterni |
| Incrementale | Acquisisce lo stato corrente e riusa gli oggetti invariati delle copie precedenti | Gli oggetti referenziati devono restare disponibili |
| Differenziale | Acquisisce le differenze rispetto a una base completa scelta | Richiede base e differenziale selezionato, salvo consolidamento |

La deduplicazione interna può evitare duplicazioni fisiche anche in una copia logica completa. «Completo autonomo» significa che, esportando quella copia, si trasferiscono tutti gli oggetti necessari.

Le cancellazioni sul PC sorgente devono essere registrate nel nuovo stato, senza cancellare i contenuti delle copie precedenti ancora conservate. Il ripristino deve ricostruire lo stato del punto scelto e distinguere ripristino per unione da sostituzione della destinazione.

## 8. Profili e regole di selezione

Un profilo deve contenere nome, descrizione, utenti, applicativi selezionati, componenti, esclusioni, strategia di versione, destinazione, cifratura, modalità temporale, conservazione e pianificazione.

Precedenza delle regole:

1. vincoli tecnici, di accesso e di sicurezza;
2. esclusione obbligatoria dei videogiochi e delle loro risorse riconosciute;
3. esclusioni esplicite dell'utente;
4. personalizzazione della singola installazione;
5. impostazioni del gruppo o del profilo;
6. valori iniziali del prodotto.

Una dipendenza necessaria ma esclusa esplicitamente deve produrre un conflitto. Il programma propone di includerla o spiega quale operazione resterà bloccata; non modifica silenziosamente la scelta.

La modifica di un profilo non deve alterare un backup storico. Ogni esecuzione conserva una copia immutabile delle scelte effettive, delle versioni degli adattatori e del piano utilizzato.

Le azioni massive devono mostrare un'anteprima delle modifiche. Non devono annullare personalizzazioni individuali senza indicarlo e richiederne la conferma.

## 9. Schermate e gestione completa

### 9.1 Panoramica

Mostrare ultima scansione, ultimi backup, spazio utilizzato, destinazioni disponibili, prossime esecuzioni, problemi e attività da completare. Offrire accesso diretto a nuova analisi, backup e ripristino.

### 9.2 Inventario

Mostrare programmi e ambienti con selezione, profilo assegnato, versione, origine, utente, dimensione stimata, disponibilità offline e stato di supporto. Le colonne avanzate devono essere configurabili.

### 9.3 Dettaglio del programma

Prevedere aree per identità, contenuti, impostazioni, dati, componenti aggiuntivi, accessi, dipendenze, pacchetti, versioni e cronologia. Mostrare le prove di rilevazione e consentire la correzione delle associazioni proposte.

### 9.4 Profili di backup

Consentire creazione, duplicazione, modifica, esportazione e importazione di profili senza segreti incorporati. I riferimenti alle credenziali devono richiedere una nuova associazione quando il profilo viene importato altrove.

### 9.5 Libreria dei pacchetti

Gestire installer acquisiti, versione, architettura, lingua, origine, firma, hash, dipendenze e backup che li utilizzano. Consentire acquisizione di una versione ufficiale disponibile, importazione di un installer posseduto dall'utente e verifica dell'artefatto.

Distinguere installer completo, bootstrapper che scarica contenuti e pacchetto non ancora verificato. Un bootstrapper non soddisfa da solo il requisito offline.

### 9.6 Archivio dei backup

Elencare copie per PC, profilo, data, programmi, dimensione logica, spazio fisico, modalità e stato. Consentire confronto, verifica, esportazione autonoma, conservazione e cancellazione controllata.

### 9.7 Centro di ripristino

Consentire scelta del punto di ripristino, selezione dei componenti, mappature, analisi dei conflitti, piano delle operazioni e avvio. Deve essere possibile ripristinare soltanto le preferenze di un singolo programma già installato, dopo verifica della compatibilità.

### 9.8 Attività e rapporti

Mostrare avanzamento, elemento corrente, quantità completate, errori, attese di interazione e riavvii. Consentire pausa ai punti sicuri, annullamento e ripresa.

### 9.9 Destinazioni e pianificazioni

Gestire dischi esterni, cartelle locali e condivisioni di rete. Mostrare spazio, raggiungibilità, identificazione della destinazione e ultimo controllo. Una lettera di unità uguale non basta a identificare lo stesso disco.

## 10. Motore di inventario

### 10.1 Fonti da integrare

| Fonte | Informazioni da raccogliere |
| --- | --- |
| Registro delle disinstallazioni | Installazioni macchina e utente, viste a 32 e 64 bit |
| API Windows Installer | Identità MSI, versioni e contesti senza attivare riparazioni |
| Appx e MSIX | Identità del pacchetto, versione, architettura, famiglia e dipendenze |
| Microsoft Store | Riferimento al prodotto quando identificabile, separato dall'identità del pacchetto |
| WinGet | Pacchetti riconosciuti, fonti e corrispondenze con le installazioni |
| Chocolatey e Scoop | Inventario e metadati tramite connettori dedicati |
| Altri gestori | Connettori estendibili e capacità dichiarate |
| File system | Software portabile, cartelle applicative, installer e script |
| Collegamenti e integrazioni | Destinazioni, argomenti, directory di lavoro e riferimenti applicativi |
| Ambienti di sviluppo | Runtime, SDK, tool globali, ambienti e manifest di dipendenze |
| Applicazioni ospitanti | Plugin, estensioni, preset e componenti elencati dal programma |

Le scansioni del file system devono coprire i volumi selezionati, rispettare i confini dei percorsi, evitare cicli di collegamenti e gestire file non accessibili. La scansione completa deve poter essere interrotta e ripresa.

I gestori non devono essere installati automaticamente per poterli interrogare. Un connettore deve riconoscere la versione e le capacità effettivamente disponibili e riportare gli errori di parsing.

### 10.2 Identità e riconciliazione

Distinguere prodotto, installazione, versione, utente e pacchetto installabile. Conservare tutte le osservazioni originali con fonte e data. Non unire due installazioni basandosi soltanto sul nome.

Una corrispondenza proposta deve riportare confidenza e motivazione. Le associazioni ambigue devono restare separate finché non vengono risolte.

### 10.3 Componenti non riconosciuti

Prevedere una sezione dedicata con eseguibili, script, cartelle e configurazioni candidate. Consentire etichetta manuale, associazione a un prodotto, inclusione archivistica e creazione di una regola personale.

Un'euristica non verificata non può autorizzare automaticamente modifiche nel ripristino. La provenienza inferita e quella confermata dall'utente devono restare distinguibili.

## 11. Esclusione dei videogiochi

Riconoscere giochi mediante catalogo, manifest, metadati dei launcher e regole specifiche. Non classificare come gioco qualsiasi programma presente nella cartella di un launcher.

Comportamento richiesto:

- giochi confermati: esclusi, con motivazione visibile;
- elementi probabilmente videoludici: in revisione e non acquisiti fino alla classificazione;
- risorse di un gioco: escluse insieme al gioco;
- runtime condivisi: mantenuti se necessari a programmi selezionati;
- cartelle miste: acquisizione filtrata o richiesta di revisione, senza copiare l'intera cartella;
- software di creazione di giochi: classificato come strumento di lavoro, salvo scelta diversa dell'utente.

Un'inclusione manuale di una cartella ampia deve riapplicare le esclusioni conosciute. Il rapporto deve indicare i limiti di riconoscimento, senza dichiarare una certezza assoluta sui file non classificabili.

## 12. Adattatori per gli applicativi

Ogni adattatore deve descrivere rilevazione, versioni supportate, contenuti, esportazione, importazione, dipendenze, trasformazioni consentite e verifiche.

Usare regole dichiarative per le operazioni semplici. Le integrazioni complesse devono essere implementate in codice controllato e collaudato. Non accettare da un archivio esterno comandi shell arbitrari da eseguire con privilegi elevati.

Le capacità dichiarate devono distinguere:

- inventario verificato;
- acquisizione archivistica;
- esportazione nativa;
- ripristino configurazioni verificato;
- reinstallazione offline verificata;
- trasferimento tra versioni verificato;
- esportazione supportata degli accessi;
- intervento manuale richiesto.

Ogni adattatore deve avere identificatore, versione, origine autenticata, intervallo di compatibilità e prove automatiche. Il catalogo installato deve poter restare utilizzabile offline. Un aggiornamento non deve cambiare retroattivamente l'interpretazione di un backup già concluso.

Un adattatore generico può acquisire file e metadati, ma deve dichiarare la minore copertura del ripristino.

## 13. Versioni e pacchetti installabili

Ordine preferito per recuperare il software:

1. installer completo già posseduto, identificato e verificato;
2. pacchetto della versione esatta disponibile tramite fonte ufficiale del produttore o canale autorizzato;
3. pacchetto identificato da un gestore supportato, verificando provenienza e artefatto;
4. riferimento ufficiale alla versione o al prodotto;
5. copia archivistica con istruzioni per l'intervento manuale.

Per ogni artefatto registrare nome, versione richiesta e rilevata, architettura, lingua, dimensione, hash, origine, data di acquisizione e stato della firma. Un hash dimostra integrità rispetto a un riferimento; da solo non dimostra che il produttore sia affidabile.

Le pagine dei risultati di ricerca non sono fonti di download. Distinguere dominio del produttore, CDN autorizzata, repository ufficiale e mirror non verificato. Non salvare negli elenchi pubblici URL contenenti token di accesso.

La gratuità del download e la licenza d'uso devono essere campi separati: gratuito, prova, commerciale o sconosciuto. Il programma non deve dedurre la gratuità dall'assenza di un pagamento durante il download.

Se la versione esatta non è recuperabile, presentare le alternative e richiedere una scelta prima di installarne una diversa. Una configurazione destinata a una versione vecchia non deve essere importata automaticamente in una versione nuova senza compatibilità dichiarata.

Non devono essere disabilitati silenziosamente gli aggiornamenti del programma migrato per mantenerne la versione. Eventuali meccanismi di pinning devono essere supportati dal canale e scelti esplicitamente.

## 14. Microsoft Store e applicazioni MSIX

Il connettore deve gestire pacchetti installati per utente, dipendenze, famiglia del pacchetto e riferimenti Store quando disponibili. L'origine Store non deve essere dedotta unicamente dall'estensione del pacchetto; lo Store può distribuire anche applicativi tradizionali.

Per ogni applicativo presentare una strategia concreta:

- reinstallazione tramite Store e account necessario;
- installazione di un pacchetto autorizzato già acquisito;
- recupero online disponibile;
- conservazione dei soli dati supportati;
- intervento manuale se il pacchetto non è ottenibile.

La stessa versione non è sempre disponibile nel canale Store. Il download offline e le licenze possono richiedere condizioni e autenticazioni specifiche. Queste possibilità devono essere rilevate, non presunte.

Non ricostruire un pacchetto installabile mediante copia indiscriminata di `WindowsApps`, modifica delle protezioni della directory o assunzione che il vecchio diritto d'uso sia trasferito.

Il ripristino dello stato richiede installazione corretta, contesto dell'utente e compatibilità dell'applicazione. Copiare il profilo non garantisce il trasferimento di login, licenze o dati cifrati.

## 15. Impostazioni dati e integrazioni

L'adattatore deve distinguere dati indispensabili, preferenze, cache, log, stato temporaneo e segreti. Se un file di configurazione contiene sia opzioni sia credenziali, non può essere trattato automaticamente come semplice preferenza.

Percorsi da considerare: AppData roaming e locale, ProgramData, documenti, cartelle dell'applicativo, volumi secondari, directory personalizzate e percorsi di progetto scelti dall'utente. Non è sufficiente salvare soltanto AppData.

Per database e archivi complessi usare esportazioni supportate o acquisizione consistente. Salvare i file accessori necessari, per esempio log di transazione quando previsti dalla procedura dell'adattatore.

Per file cloud solo online, distinguere metadato e contenuto effettivamente disponibile. Un eventuale download deve essere esplicito e riportare la quantità da acquisire. I placeholder non devono essere conteggiati come dati salvati.

Gestire e documentare, per i tipi supportati, collegamenti simbolici, hard link, attributi, flussi alternativi NTFS, nomi lunghi, permessi e file cifrati. Le modalità non supportate devono produrre un'eccezione visibile. I file EFS richiedono una procedura compatibile e le chiavi necessarie; non devono risultare ripristinabili solo perché copiati.

Registro, associazioni file, protocolli, servizi, avvio automatico e attività pianificate devono essere trattati con regole specifiche. Non importare l'intero registro o riprodurre indiscriminatamente identità e autorizzazioni del vecchio PC.

Le preferenze di sistema che richiedono interazione dell'utente devono essere presentate come passaggi guidati. Non usare sostituzioni globali di stringhe per correggere percorsi dentro file binari o database.

## 16. Credenziali e attivazioni

Il prodotto deve permettere di inventariare gli account configurati e utilizzare le procedure supportate di esportazione, sincronizzazione o nuova autenticazione.

Classificazioni obbligatorie:

| Classe | Trattamento |
| --- | --- |
| Esportabile tramite funzione supportata | Acquisizione protetta, selezionabile separatamente |
| Sincronizzabile tramite il servizio originale | Istruzioni per collegare il nuovo dispositivo e verificare il risultato |
| Legata a utente o computer | Conservazione eventualmente archivistica e nuova autenticazione prevista |
| Non esportabile o legata all'hardware | Nessuna promessa di trasferimento; procedura di riassociazione |
| Stato sconosciuto | Verifica richiesta prima di dichiarare il ripristino |

DPAPI può vincolare la decifratura al contesto originale. Anche sessioni browser, token, Windows Hello e chiavi protette dall'hardware non devono essere considerati trasferibili per semplice copia.

Non implementare aggiramenti delle protezioni, estrazione indiscriminata di cookie o recupero di segreti da processi. Usare le funzioni autorizzate del sistema o dell'applicativo e richiedere l'interazione prevista.

I segreti esportati devono essere cifrati e separati logicamente dagli altri contenuti. Nessun segreto in log, riga di comando, telemetria, nomi di file o rapporti. Gli eventuali file temporanei di esportazione devono essere evitati o protetti e rimossi al termine; non promettere la cancellazione fisica sicura su ogni dispositivo di archiviazione.

Licenze, seriali esportabili, certificati e istruzioni di riattivazione devono essere associati al relativo prodotto. Non spostare automaticamente un'attivazione se serve un'operazione specifica del produttore. L'eventuale disattivazione sul vecchio PC richiede una scelta separata.

Per più utenti Windows, operare nei contesti previsti. I privilegi amministrativi non devono essere interpretati come garanzia di poter leggere o decifrare tutti i loro accessi.

## 17. Script ambienti e componenti aggiuntivi

Per ogni script salvare sorgente, versione dell'interprete richiesta, dipendenze dichiarate, directory di lavoro, argomenti e variabili necessarie. Identificare separatamente gli eventuali segreti.

La ricostruzione deve preferire manifest, file di lock e pacchetti recuperabili. Non assumere che la copia di un ambiente virtuale o di una directory di dipendenze funzioni su un altro percorso o su un'altra architettura.

Per plugin ed estensioni conservare identità, versione, compatibilità con l'applicativo ospitante, impostazioni, risorse e metodo di reinstallazione. La disponibilità del plugin deve essere verificata separatamente da quella del programma principale.

WSL, container Docker e macchine virtuali sono fuori dal perimetro del prodotto.

## 18. Pianificazione ed esecuzione del backup

### 18.1 Controlli preliminari

Verificare permessi, accessibilità, spazio di destinazione e spazio temporaneo, compatibilità del file system, disponibilità di chiavi e installer, vincoli di consistenza e dipendenze.

Le stime devono distinguere dimensione logica, quantità nuova stimata e spazio fisico già occupato. La compressione non può essere prevista con precisione assoluta; un valore stimato deve essere etichettato come tale.

### 18.2 Acquisizione consistente

Per ogni applicativo scegliere una procedura: esportazione nativa, chiusura concordata, snapshot VSS con writer compatibile o acquisizione dichiarata consistente solo a livello di file system.

Uno snapshot VSS senza coordinamento applicativo non garantisce che ogni database sia internamente consistente. File modificati durante l'acquisizione devono essere rilevati e gestiti con retry limitato o eccezione.

### 18.3 Esecuzione

Il motore deve creare un piano immutabile, acquisire in area temporanea, verificare gli oggetti e pubblicare la nuova copia solo dopo il commit. Un'interruzione non deve rendere inutilizzabili le copie precedenti.

La pubblicazione deve seguire un protocollo di commit che scriva e sincronizzi prima gli oggetti, poi il manifest e infine il riferimento visibile alla copia. La sostituzione del riferimento deve essere atomica o recuperabile tramite transazione; una copia parziale non deve comparire tra i punti di ripristino validi.

Consentire pausa tra operazioni sicure. Un installer, un'esportazione o una fase di freeze non devono essere interrotti arbitrariamente. Se l'annullamento richiede attesa, mostrare il motivo.

### 18.4 Pianificazioni

Consentire esecuzioni manuali e pianificate, con frequenza, finestra oraria, limite di banda, comportamento a batteria, recupero di un'esecuzione saltata e politica quando il disco non è collegato.

La pianificazione può usare l'Utilità di pianificazione di Windows. Non avviare due scritture concorrenti nello stesso repository senza un coordinamento supportato.

Per le esecuzioni non presidiate, le chiavi devono essere protette localmente tramite meccanismi del sistema; deve comunque esistere un metodo separato e portabile di recupero sul nuovo PC. Non memorizzare la password in chiaro negli argomenti dell'attività.

Le attività che richiedono login interattivo devono risultare in attesa o parziali, senza simulare un successo.

## 19. Repository cifrato e conservazione

### 19.1 Formato

Usare un repository con specifica versionata, manifest, oggetti di contenuto, pacchetti, regole di migrazione e rapporti. Il formato non deve dipendere dal percorso originario del database di lavoro.

Esempio logico, da tradurre in una specifica del formato prima dell'implementazione:

```text
repository/
  format.json                 metadati minimi e parametri non segreti
  keyslots/                   chiavi dei dati protette per il recupero
  snapshots/                  manifest cifrati dei punti di backup
  objects/                    contenuti cifrati e deduplicati
  transactions/               stato delle scritture incomplete
  indexes/                    indici ricostruibili e protetti
```

Nomi di utenti, percorsi, elenco dei programmi, riferimenti a servizi e rapporti devono essere protetti insieme ai dati. L'intestazione pubblica deve contenere solo ciò che serve per riconoscere il formato e sbloccarlo.

### 19.2 Cifratura e integrità

Usare primitive standard e librerie mantenute, senza inventare algoritmi crittografici. Prevedere una chiave casuale dei dati protetta mediante password e una derivazione resistente ai tentativi, con parametri versionati, oppure una chiave di recupero indipendente.

I contenuti grandi devono essere suddivisi in blocchi con un formato versionato e limiti espliciti. Ogni blocco deve essere autenticato insieme alla sua posizione e al suo ruolo; il manifest deve autenticare l'ordine, la lunghezza e l'elenco dei blocchi, così da rilevare anche troncamento, riordino e sostituzione. Con una modalità come AES GCM, progettare e testare esplicitamente l'unicità dei nonce per chiave. Non usare hash modificabili insieme al contenuto come unica difesa dalla manomissione.

La deduplicazione deve restare interna al repository e al suo dominio di chiavi. Non introdurre deduplicazione tra utenti o archivi indipendenti che esponga l'esistenza di contenuti uguali.

Il cambio della password deve poter riproteggere la chiave dei dati senza riscrivere tutti i contenuti. La chiave di recupero deve essere generata separatamente, presentata una sola volta e richiedere una conferma che l'utente l'abbia conservata. La perdita di tutte le chiavi di sblocco deve essere spiegata al momento della creazione; nessuna chiave di recupero deve essere inserita nello stesso archivio in chiaro.

Prima di usare un archivio con dati reali, devono essere collaudati decifratura corretta, password errata, chiave di recupero, blocco mancante e blocco manomesso. Un errore di autenticazione deve fermare la lettura dell'oggetto coinvolto prima di qualsiasi ripristino dipendente.

### 19.3 Conservazione

Consentire conservazione per numero di copie, età, copie marcate come permanenti e spazio obiettivo. Mostrare un'anteprima prima delle eliminazioni manuali e applicare le politiche automatiche concordate.

L'eliminazione deve rimuovere prima i riferimenti non più necessari e poi gli oggetti non raggiungibili da alcuna copia conservata. Una copia marcata come permanente non può essere invalidata dalla cancellazione di una base condivisa.

Consentire consolidamento ed esportazione di un punto come archivio autonomo. Verificare che tutti gli oggetti richiesti siano stati inclusi prima di dichiararlo trasferibile.

Non cancellare automaticamente l'ultima copia verificata per rispettare un limite di spazio. Segnalare il conflitto e offrire alternative.

### 19.4 Destinazioni

Supportare inizialmente cartella locale, disco esterno e condivisione SMB collaudata. Rilevare disconnessioni, file system senza capacità necessarie, limiti delle dimensioni dei file e semantiche di scrittura non affidabili.

La modalità di pubblicazione atomica deve essere verificata per il tipo di destinazione. Non presumere che una condivisione di rete si comporti sempre come un disco locale.

Un repository modificabile dallo stesso utente non è automaticamente immutabile contro ransomware. Le copie su dispositivi separati o supporti con protezioni proprie vanno presentate come opzioni distinte, senza promettere protezioni inesistenti.

## 20. Ripristino selettivo

Il ripristino deve essere indipendente dalla selezione originaria: da un backup ampio si possono scegliere un solo programma, un plugin, le sole impostazioni o una cartella dati.

Prima dell'esecuzione, costruire un grafo delle operazioni e delle dipendenze:

```text
verifica archivio
  -> verifica destinazione e mappature
  -> verifica o acquisizione dei pacchetti
  -> prerequisiti e runtime
  -> installazione applicativo
  -> eventuale inizializzazione richiesta dall'adattatore
  -> componenti aggiuntivi
  -> dati e configurazioni nell'ordine previsto dall'adattatore
  -> integrazioni supportate
  -> autenticazioni e attivazioni necessarie
  -> verifica finale
```

L'ordine preciso può cambiare per applicativo. Alcune preferenze richiedono che il programma sia già stato inizializzato; altri dati devono essere presenti prima del primo avvio. La regola deve essere esplicita e verificata.

Se il programma è già presente, scegliere tra utilizzo dell'installazione compatibile, installazione affiancata se supportata, sostituzione o esclusione. Non disinstallare o retrocedere automaticamente una versione esistente.

Installatori concorrenti, file condivisi e lock devono essere coordinati. La copia dei dati indipendenti può essere parallela, ma l'ordine imposto dalle dipendenze deve essere rispettato.

Per un ripristino dichiarato offline, tutti i pacchetti richiesti devono essere già disponibili. Se mancano, fermare le operazioni dipendenti e spiegare il motivo prima di modificarle.

## 21. Conflitti mappature e rollback

### 21.1 Mappature

Associare esplicitamente utenti originali e nuovi utenti, cartelle note, dischi, directory dei progetti e percorsi di rete. Gestire nomi utente diversi senza replicare indiscriminatamente SID e ACL originali.

Mantenere i permessi necessari attraverso regole compatibili con la destinazione. I percorsi assoluti dentro configurazioni strutturate possono essere aggiornati solo da trasformazioni previste dall'adattatore.

### 21.2 Conflitti

Le politiche disponibili devono includere: mantieni destinazione, sostituisci con copia preventiva, salva entrambe, unisci tramite procedura supportata, salta e decidi singolarmente.

L'unione non deve essere disponibile per un database o formato che non la supporta. Una nuova scansione deve rilevare modifiche avvenute dopo l'approvazione del piano e bloccare le operazioni distruttive coinvolte.

I file presenti soltanto sulla destinazione non devono essere cancellati per impostazione iniziale. La sostituzione esatta di un albero richiede una scelta esplicita e un'anteprima delle cancellazioni.

### 21.3 Rollback

Prima delle modifiche, acquisire le configurazioni e i dati della destinazione coinvolti. Conservare un journal delle operazioni, dei risultati e delle possibili azioni inverse.

Il rollback deve essere dichiarato per operazione: completo, parziale o non disponibile. Installer, attivazioni e aggiornamenti esterni possono produrre modifiche non interamente annullabili. Un punto di ripristino Windows non è una garanzia universale di ritorno allo stato precedente.

Non modificare o cancellare il contenuto sul vecchio PC come conseguenza automatica del successo sul nuovo.

## 22. Architettura tecnica

### 22.1 Scelte iniziali

- Linguaggio e runtime: C# e .NET 10, con build supportata e aggiornata.
- Interfaccia: WPF, con logica di dominio separata dalla presentazione.
- Persistenza operativa: SQLite, schema versionato e migrazioni testate.
- Manifest e profili: JSON con schema, identificatori stabili e versioni esplicite.
- Integrazioni: API Windows e strumenti ufficiali, con wrapper tipizzati e parsing verificato.
- Cifratura: primitive standard tramite librerie mantenute e parametri documentati.
- Distribuzione: build autonoma per architettura, firmata per il rilascio.

Usare un'applicazione modulare locale. Non introdurre microservizi, backend cloud o un servizio Windows sempre attivo senza una necessità concreta.

La build iniziale deve fissare una versione esatta dell'SDK .NET e usare il canale di supporto LTS. La distribuzione self-contained deve essere collaudata su una macchina Windows 11 priva di runtime di sviluppo.

### 22.2 Processi

**Interfaccia e coordinatore:** operano come utente normale, gestiscono piani, stato e interazione.

**Worker utente:** esegue le operazioni che richiedono il contesto dell'utente interessato. Non sostituire quel contesto con quello dell'account amministrativo.

**Broker amministrativo:** viene avviato quando necessario per le operazioni consentite, come accessi di sistema, VSS e installazioni macchina. Deve terminare quando non serve più.

Usare un canale IPC locale autenticato con ACL e verifica dell'identità del chiamante. I messaggi devono avere schemi e dimensioni limitate. Nessuna operazione generica «esegui questa stringa come amministratore».

### 22.3 Moduli applicativi

| Modulo | Responsabilità |
| --- | --- |
| Discovery | Raccogliere osservazioni dalle fonti |
| Catalog | Identificare prodotti, versioni e installazioni |
| Selection | Applicare profili, esclusioni e scelte per componente |
| Adapters | Descrivere esportazione, importazione e verifica |
| Package Library | Risolvere e conservare gli artefatti installabili |
| Planner | Produrre operazioni, dipendenze e conflitti |
| Backup | Acquisire uno stato consistente |
| Repository | Cifrare, deduplicare, pubblicare e conservare |
| Restore | Applicare il piano sulla destinazione |
| Verification | Valutare integrità e risultato applicativo |
| Reporting | Produrre cronologia, eccezioni e rapporti |

Questi sono confini logici. Non occorre trasformare ogni modulo in un processo o in un pacchetto separato. Creare astrazioni dove esistono implementazioni diverse, come i connettori e gli adattatori.

### 22.4 Confini di fiducia

Trattare come non attendibili backup importati, manifest, archivi compressi, percorsi, pacchetti e risposte dei gestori. Validare dimensioni, versioni e contenuti prima dell'uso.

Impedire attraversamento di directory, scrittura fuori destinazione, estrazione attraverso reparse point, decompressioni senza limiti e attacchi tramite collegamenti. Verificare nuovamente il percorso effettivo al momento dell'operazione privilegiata.

Un hash corretto non autorizza un eseguibile. Il piano deve distinguere integrità del file, identità della fonte e autorizzazione all'esecuzione. Gli adattatori inclusi nel backup sono dati da verificare, non codice automaticamente fidato.

## 23. Modello dei dati

| Entità | Contenuti essenziali |
| --- | --- |
| Machine | Identità locale, sistema, architettura e capacità |
| UserProfile | Utente sorgente o destinazione, contesto e cartelle note |
| Product | Identità del software, produttore e classificazione |
| Installation | Prodotto, versione, utente, ambito, architettura e percorsi |
| Observation | Fonte, data, valori osservati, confidenza e prova |
| Component | Tipo, proprietario, percorsi, sensibilità e supporto |
| Dependency | Relazione, versione richiesta e motivazione |
| Adapter | Identità, versione, capacità e compatibilità |
| PackageArtifact | Versione, origine, hash, firma e disponibilità |
| BackupProfile | Selezioni, esclusioni e politiche riutilizzabili |
| SelectionSnapshot | Scelte effettive immutabili dell'esecuzione |
| Plan | Operazioni, prerequisiti, conflitti e azioni approvate |
| Run | Avanzamento, risultati, attese e interruzioni |
| Snapshot | Stato logico del backup e oggetti referenziati |
| ContentObject | Identità interna, dimensione e contenuto protetto |
| SecretReference | Riferimento al dato protetto, senza segreto in chiaro |
| Mapping | Associazione utenti, volumi e percorsi |
| OperationResult | Richiesto, acquisito, verificato, errore e azione residua |
| Report | Risultati, eccezioni e riepilogo esportabile |

Le osservazioni non devono sovrascrivere silenziosamente informazioni precedenti. Percorsi e nomi utente possono essere dati sensibili; il database locale deve risiedere in un percorso protetto, e gli eventuali campi sensibili devono avere una protezione adeguata. SQLite da solo non fornisce automaticamente la cifratura del database.

Il database operativo può velocizzare il lavoro, ma non deve essere l'unica fonte necessaria a ripristinare un backup.

## 24. Esempio del profilo di selezione

Il JSON seguente è un contratto illustrativo da formalizzare con JSON Schema. Identificatori, versione e nomi sono fittizi. Il profilo descrive le intenzioni; non attesta che i contenuti siano stati acquisiti.

```json
{
  "schemaVersion": 1,
  "profileId": "profilo-lavoro",
  "name": "Ambiente di lavoro selezionato",
  "newApplicationsPolicy": "review",
  "excludeGames": true,
  "users": ["source-user-01"],
  "applications": [
    {
      "installationRef": "installation-editor-01",
      "productRef": "example.editor",
      "selected": true,
      "preset": "custom",
      "components": {
        "installer": true,
        "installedFilesArchive": false,
        "settings": true,
        "data": true,
        "extensions": true,
        "scripts": false,
        "integrations": true,
        "supportedCredentialExport": false,
        "cache": false
      },
      "versionPolicy": {
        "requestedVersion": "4.2.0",
        "strategy": "exact",
        "alternativeVersionAction": "ask"
      },
      "packagePolicy": {
        "mode": "archive-offline",
        "onUnavailable": "require-decision"
      },
      "dataRoots": [
        {
          "path": "${UserDocuments}/ProgettiEditor",
          "excludePatterns": ["**/cache/**", "**/temp/**"]
        }
      ]
    },
    {
      "installationRef": "installation-tool-02",
      "productRef": "example.utility",
      "selected": true,
      "preset": "settings-only",
      "versionPolicy": {
        "strategy": "compatible-existing",
        "alternativeVersionAction": "ask"
      }
    }
  ],
  "backup": {
    "mode": "incremental",
    "repositoryRef": "external-repository-01",
    "encryptionRequired": true,
    "keyReference": "local-protected-key-reference",
    "verification": "all-new-objects-and-manifest"
  },
  "retention": {
    "keepLast": 10,
    "respectPinnedSnapshots": true,
    "protectLastVerifiedSnapshot": true
  }
}
```

I placeholder devono appartenere a un elenco consentito e risolversi nel contesto corretto. Non devono essere espansi da una shell. I riferimenti alle chiavi non devono contenere password e non devono diventare utilizzabili su un altro PC per la sola importazione del profilo.

Lo schema definitivo deve specificare valori iniziali, campi obbligatori, combinazioni incompatibili, trattamento dei campi sconosciuti e migrazioni tra versioni. Un profilo non valido deve essere rifiutato con errori leggibili.

## 25. Manifest del backup e contratti degli adattatori

Il manifest acquisito deve essere distinto dal profilo richiesto. Per ciascuna installazione deve contenere selezione effettiva, identità, versione rilevata, adattatore utilizzato, oggetti presenti, metodo di consistenza, eventuali omissioni e verifiche effettuate.

Per ciascun componente conservare almeno:

- selezionato sì o no e motivazione;
- modalità richiesta e modalità effettiva;
- elenco degli oggetti acquisiti o loro riferimento;
- quantità attesa, acquisita e verificata;
- metadati necessari al ripristino;
- sensibilità e trattamento degli accessi;
- stato di supporto e azioni residue.

Un componente selezionato ma non acquisito deve avere un errore o un'esclusione esplicita. Non deve scomparire dal manifest.

Il contratto di un adattatore deve comprendere identità, rilevazione, versioni supportate, componenti, piano di acquisizione, piano di ripristino e verifiche. Le azioni dichiarative consentite devono essere limitate e validate, per esempio copia di un percorso ammesso, esportazione di valori di registro ammessi o invocazione di un'integrazione interna identificata.

Modificare il formato del repository richiede test di compatibilità e strumenti di migrazione. Una versione del lettore non compatibile deve fermarsi prima di scrivere, senza tentare interpretazioni approssimative.

## 26. Stati errori e verifica

Ogni esecuzione deve distinguere almeno:

```text
bozza -> analisi -> pianificazione -> pronto -> in esecuzione
                                         -> in attesa di interazione
                                         -> in attesa di riavvio
                                         -> in pausa
                                         -> verifica -> concluso
```

Gli esiti terminali sono: concluso senza eccezioni nel perimetro selezionato, concluso con eccezioni, fallito e annullato. La ripresa deve conservare l'identità dell'esecuzione e ricontrollare i prerequisiti delle operazioni ancora da fare.

Per il singolo elemento distinguere escluso per scelta, non supportato, bloccato, acquisito, verificato nell'archivio, ripristinato e verificato sulla destinazione. Le prove di verifica devono essere registrate: presenza dei file, confronto di hash, rilevazione della versione o controllo specifico dell'adattatore.

La verifica dell'integrità del backup e quella del funzionamento dell'applicativo sono risultati diversi. Anche un avvio riuscito non dimostra da solo che tutti i dati e tutte le preferenze siano corretti.

Il programma non deve dichiarare «concluso senza eccezioni» se una parte selezionata è stata saltata o se l'archivio non è leggibile. Deve poter concludere correttamente un backup di sole impostazioni, purché sia quello il perimetro scelto e verificato.

## 27. Rapporti e confronti

Ogni esecuzione deve produrre un riepilogo comprensibile e un rapporto dettagliato esportabile in Markdown e JSON. Il rapporto deve essere selezionabile senza credenziali o segreti e deve evitare di esporre percorsi sensibili senza necessità.

Contenuti obbligatori:

- PC, profilo, data, utenti e perimetro;
- programmi e componenti selezionati;
- versioni richieste, acquisite e installate;
- quantità acquisite e verificate;
- contenuti esclusi e relativa ragione;
- pacchetti disponibili offline e riferimenti online;
- eccezioni di consistenza, accesso o compatibilità;
- login, attivazioni e azioni manuali necessarie;
- risultato del ripristino e possibilità di rollback.

Il confronto tra copie deve evidenziare programmi aggiunti o rimossi, cambi di versione, modifiche alle configurazioni, dati aggiunti o cancellati e variazioni della selezione. Il programma deve distinguere «non più selezionato» da «non più presente sul PC».

Il confronto sorgente destinazione deve mostrare le differenze residue senza dichiarare una corrispondenza perfetta basata sul solo numero di applicativi installati.

## 28. Requisiti operativi e qualità

- Eseguire l'analisi in background senza bloccare l'interfaccia; permettere annullamento cooperativo.
- Limitare e misurare uso di CPU, memoria, disco e rete. Offrire limiti operativi quando utili.
- Gestire grandi file e molti file piccoli senza caricare tutti i contenuti in memoria.
- Rispettare i confini dei percorsi, Unicode, nomi lunghi e cartelle note reindirizzate.
- Rilevare spazio insufficiente durante l'esecuzione e interrompere in sicurezza.
- Evitare scritture concorrenti non coordinate e doppie attività sullo stesso profilo.
- Mantenere una cronologia persistente e log con dati sensibili rimossi.
- Non richiedere una connessione cloud per aprire un backup locale.
- Consentire uso da tastiera, lettori di schermo, ridimensionamento e DPI elevati.
- Documentare formato, limiti, prerequisiti e procedure di recupero delle operazioni interrotte.
- Prevedere aggiornamenti autenticati dell'applicazione e del catalogo; non aggiornare il formato di un repository senza un'azione prevista e una strategia di recupero.

I tempi e le prestazioni devono essere misurati su dataset dichiarati. Non fissare promesse commerciali di velocità prima dei benchmark.

## 29. Collaudi e criteri di accettazione

Il prodotto deve avere prove automatiche della logica e collaudi su macchine virtuali Windows pulite. Le funzionalità legate a hardware, Store e identità richiedono anche ambienti compatibili dedicati.

| ID | Scenario | Risultato richiesto |
| --- | --- | --- |
| AC01 | Tre programmi rilevati, uno selezionato | Vengono acquisiti soltanto i componenti del programma scelto e le dipendenze autorizzate |
| AC02 | Profilo solo impostazioni | Nessun installer o dato progetto viene incluso senza una scelta aggiuntiva |
| AC03 | Personalizzazione di un programma in un gruppo | La scelta individuale è rispettata e compare nel piano |
| AC04 | Nuovo programma dopo un backup pianificato | Resta in revisione salvo regola esplicita compatibile |
| AC05 | Gioco e programma di lavoro con runtime condiviso | Gioco escluso e runtime necessario conservato |
| AC06 | Versione esatta indisponibile | Nessun passaggio silenzioso alla versione più recente |
| AC07 | Applicativo Store senza pacchetto offline | Limite visibile; nessuna dichiarazione di ripristino offline |
| AC08 | File contenente anche un segreto | Classificazione sensibile e nessuna esposizione nel rapporto |
| AC09 | Credenziale legata al vecchio PC | Stato di nuova autenticazione, senza falso trasferimento riuscito |
| AC10 | Database in uso | Procedura consistente o eccezione esplicita |
| AC11 | Incrementale dopo modifica e cancellazione di file | Ogni punto ricostruisce il proprio stato e la copia precedente resta valida |
| AC12 | Cancellazione di una copia che condivide oggetti | Nessun backup conservato perde oggetti necessari |
| AC13 | Esportazione autonoma di un punto incrementale | Ripristino riuscito senza accesso al repository originale |
| AC14 | Interruzione durante una scrittura | Ultima copia valida integra e transazione incompleta recuperabile |
| AC15 | Disconnessione del disco o della rete | Nessun commit incompleto e messaggio di ripresa comprensibile |
| AC16 | Password errata o oggetto manomesso | Rifiuto della lettura coinvolta prima dell'installazione dipendente |
| AC17 | Ripristino su utente e percorsi differenti | Mappatura corretta per i dati supportati, senza vecchi SID copiati indiscriminatamente |
| AC18 | Ripristino delle sole preferenze da backup completo | Vengono modificate soltanto le componenti autorizzate |
| AC19 | Dati già presenti sulla destinazione | Anteprima dei conflitti e nessuna sovrascrittura non approvata |
| AC20 | Riavvio richiesto da un installer | Ripresa dal punto previsto, senza duplicare operazioni già concluse |
| AC21 | Collegamento che esce dalla destinazione o archivio malevolo | Scrittura rifiutata e nessuna elevazione arbitraria |
| AC22 | File cloud non disponibile localmente | Non conteggiato come contenuto acquisito |
| AC23 | Adattatore assente o non compatibile | Funzione bloccata o acquisizione archivistica chiaramente etichettata |
| AC24 | Nuovo PC senza rete durante migrazione offline | Tutti i componenti dichiarati offline vengono ricostruiti dai pacchetti inclusi |
| AC25 | Piano approvato e destinazione cambiata nel frattempo | Conflitto ricontrollato prima delle modifiche coinvolte |
| AC26 | Conservazione con ultima copia verificata | Nessuna eliminazione automatica dell'ultima copia protetta |
| AC27 | Due utenti con lo stesso programma | Dati e configurazioni restano associati al rispettivo utente |
| AC28 | Applicativo sconosciuto | File archiviabili, ma ripristino non dichiarato verificato senza una regola adeguata |
| AC29 | Errore di autenticazione durante il ripristino | Il contenuto coinvolto viene rifiutato e nessuna modifica dipendente viene applicata |
| AC30 | Interruzione durante il ripristino | Il journal mostra le operazioni incerte; il rollback disponibile è eseguito o dichiarato esplicitamente |
| AC31 | Due backup avviati sullo stesso repository | Un'operazione attende o viene rifiutata; nessun commit o indice viene corrotto |

Per ogni adattatore dichiarato supportato, collaudare almeno rilevazione, selezione parziale, acquisizione, ripristino e verifica. Aggiungere prove specifiche per versioni affiancate, plugin e compatibilità tra versioni quando tali capacità sono dichiarate.

## 30. Ordine di realizzazione

### Fase 0 Prima consegna tecnica verificabile

Costruire un percorso verticale minimo prima di ampliare connettori, adattatori e interfaccia:

1. Definire gli schemi minimi di selezione, manifest, piano e rapporto con una fixture versionata.
2. Rilevare un applicativo MSI e, se disponibile, la relativa corrispondenza WinGet, senza avviarne installer o riparazioni.
3. Consentire la selezione delle sole impostazioni tramite un adattatore di prova sicuro.
4. Acquisire in un repository cifrato locale e verificare integrità e manomissione.
5. Ripristinare su un utente di prova in una VM Windows pulita e produrre un rapporto con gli esiti reali.

La prima consegna è completata solo quando il quinto passaggio è riproducibile da una fixture e da un archivio autonomo. L'installer esatto offline e la copertura di più prodotti appartengono ai checkpoint successivi, non a una simulazione della Fase 0.

### Fase 1 Percorso completo con copertura definita

Realizzare inventario di base, selezione per programma e componente, profili, esclusione giochi, libreria installer, backup completo e incrementale, repository cifrato, verifica, ripristino selettivo e rapporti.

Includere il riconoscimento Store con limiti espliciti, software portabile e un gruppo dichiarato di adattatori verificati. Implementare subito il ripristino su un secondo ambiente Windows: non rimandarlo dopo la costruzione del motore di backup.

La fase è conclusa soltanto quando una selezione reale può essere acquisita, trasferita e verificata sul nuovo PC, comprese le eccezioni dichiarate.

### Fase 2 Gestione estesa

Ampliare connettori e adattatori, aggiungere pianificazioni, conservazione avanzata, modalità differenziale, più utenti, gestione plugin e procedure supportate per credenziali e licenze.

Integrare i percorsi Store effettivamente disponibili e i controlli della libreria pacchetti. Ampliare i test di compatibilità e le trasformazioni tra percorsi.

### Fase 3 Ambienti avanzati

Aggiungere database complessi, architetture ulteriori e trasferimento diretto tra PC. Il trasferimento diretto deve riutilizzare manifest e verifiche del repository, evitando un secondo motore con regole differenti.

Ogni fase deve riportare chiaramente le capacità disponibili. Non mostrare come funzionanti opzioni per cui esiste soltanto l'interfaccia.

## 31. Istruzioni per chi sviluppa

1. Trattare questo documento come specifica iniziale del prodotto e mantenere una matrice tra requisiti, implementazioni e collaudi.
2. Definire prima gli schemi di selezione, manifest, piano e risultati. Distinguere intenzione, acquisizione e ripristino.
3. Implementare il percorso verticale minimo su Windows: rileva un programma, seleziona soltanto le preferenze, acquisisci, verifica e ripristina su una macchina pulita.
4. Aggiungere la conservazione di un installer esatto e il ripristino offline, mantenendo separati i relativi esiti.
5. Introdurre il repository cifrato e la gestione delle interruzioni prima di usarlo con dati reali importanti.
6. Costruire selezione multipla, personalizzazioni e profili usando gli stessi contratti del motore; non duplicare la logica nella UI.
7. Aggiungere adattatori uno alla volta, con fixture e prove di migrazione ripetibili.
8. Usare API native e dipendenze mantenute; evitare architetture distribuite e framework aggiuntivi non necessari.
9. Eseguire gli installer soltanto nel contesto e nell'ordine previsti dal piano. Gestire codici di uscita, interazione e riavvii.
10. Non lasciare percorsi di errore che registrino un successo a fronte di contenuti mancanti.
11. Documentare per ogni rilascio piattaforme collaudate, adattatori supportati, limiti e formato del repository.
12. Prima di dichiarare una versione utilizzabile, eseguire un ripristino completo di prova da un archivio autonomo su un PC differente.

### Consegne di sviluppo richieste

- Sorgenti e istruzioni di compilazione riproducibili.
- Applicativo Windows distribuibile, con versione e architettura identificabili.
- Procedura di firma e rilascio, separata dalle build di sviluppo.
- Schemi JSON, specifica del repository e gestione delle versioni del formato.
- Catalogo degli adattatori e relative prove.
- Test automatici e procedure di collaudo su Windows.
- Manuale per selezione, backup, conservazione e ripristino.
- Procedura di recupero dopo interruzione o perdita del database operativo.
- Rapporti esemplificativi privi di dati personali e segreti.
- Elenco chiaro dei limiti ancora aperti.

### Definizione del risultato completo

Il programma soddisfa il progetto quando l'utente può scegliere gli applicativi, scegliere separatamente cosa salvare per ciascuno, conoscere in anticipo i limiti, ottenere una copia verificata e ripristinare i componenti scelti sul nuovo PC con un rapporto fedele del risultato.

La completezza è misurata rispetto al perimetro selezionato e alle capacità dichiarate, senza nascondere eccezioni. La quantità di file copiati o il numero di programmi elencati non bastano a dimostrare una migrazione riuscita.

## 32. Riferimenti tecnici ufficiali

Le possibilità effettive dei sistemi e dei servizi esterni devono essere ricontrollate durante lo sviluppo e il collaudo. I riferimenti seguenti motivano i principali vincoli dell'architettura.

- [WinGet Export](https://learn.microsoft.com/en-us/windows/package-manager/winget/export): esportazione dell'elenco, versioni opzionali e limiti della corrispondenza con le fonti.
- [WinGet Download](https://learn.microsoft.com/windows/package-manager/winget/download): download di installer, dipendenze e condizioni per pacchetti e licenze Store.
- [Get AppxPackage](https://learn.microsoft.com/en-us/powershell/module/appx/get-appxpackage?view=windowsserver2019-ps): enumerazione dei pacchetti per utente e requisiti per gli altri profili.
- [Versioni e requisiti dei pacchetti Store](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/package-version-numbering?pivots=store-installer-msix): identità, architetture e selezione della versione applicabile.
- [CryptProtectData e DPAPI](https://learn.microsoft.com/en-us/windows/win32/api/dpapi/nf-dpapi-cryptprotectdata): vincoli di decifratura legati a utente e computer, con le eccezioni documentate.
- [Volume Shadow Copy Service](https://learn.microsoft.com/en-us/windows-server/storage/file-server/volume-shadow-copy-service): coordinamento di snapshot, writer e consistenza.
- [Shadow Copies and Shadow Copy Sets](https://learn.microsoft.com/en-us/windows/win32/vss/shadow-copies-and-shadow-copy-sets): distinzione tra consistenza al crash e consistenza applicativa.
- [Inventario e limiti di Win32 Product](https://devblogs.microsoft.com/scripting/use-powershell-to-find-installed-software/): effetti indesiderati delle interrogazioni che provocano controlli e riparazioni.
- [API Windows Installer per verificare un prodotto](https://support.microsoft.com/en-us/servicing/os/windows/2020/04/how-to-programmatically-check-for-the-presence-of-a-windows-installer-based-product-by-using-its-pro): interrogazione attraverso identità e contesto MSI.
- [WPF](https://github.com/dotnet/wpf): framework Windows desktop proposto.
- [.NET 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0): runtime e distribuzioni desktop.
