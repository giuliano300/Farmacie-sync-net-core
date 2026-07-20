# Heron Integration System - documentazione sviluppatore

## 1. Scopo e architettura

Heron Integration System importa il catalogo Heron, arricchisce i prodotti tramite
Farmadati, risolve disponibilità e prezzi dei fornitori e sincronizza Magento.

La solution contiene tre progetti:

| Progetto | Responsabilità |
| --- | --- |
| `HeronIntegration.Api` | API HTTP amministrative, dashboard e comandi manuali |
| `HeronIntegration.Engine` | Worker, pipeline, integrazioni esterne e persistenza |
| `HeronIntegration.Shared` | Entità MongoDB, DTO, enum e singleton condivisi |

API ed Engine sono due processi separati. Condividono database e registrazioni DI,
ma non memoria o singleton. I job durevoli devono quindi essere gestiti dall'Engine;
un `Task.Run` avviato dall'API vive soltanto quanto il processo API.

```text
Frontend / operatore
        |
        v
HeronIntegration.Api -------> MongoDB <------- HeronIntegration.Engine
                                      |          |-- batch pipeline
                                      |          |-- supplier FTP
                                      |          |-- Farmadati SOAP
                                      |          `-- Magento REST/SSH
                                      `-------> GridFS (immagini Farmadati)
```

## 2. Avvio locale

Requisiti:

- .NET SDK 9.x;
- MongoDB raggiungibile;
- credenziali valide per i test reali verso Farmadati, FTP e Magento.

```powershell
dotnet restore HeronIntegrationSystem.sln
dotnet build HeronIntegrationSystem.sln
dotnet run --project HeronIntegration.Api\HeronIntegration.Api.csproj
dotnet run --project HeronIntegration.Engine\HeronIntegration.Engine.csproj
```

L'Engine può essere installato come servizio Windows con nome
`Heron Integration Engine`.

## 3. Configurazione

File principali:

- `HeronIntegration.Api/appsettings.json`;
- `HeronIntegration.Api/appsettings.Development.json`;
- `HeronIntegration.Engine/appsettings.json`.

Le impostazioni possono essere sovrascritte con variabili d'ambiente usando `__`
come separatore:

```powershell
$env:Mongo__ConnectionString = "mongodb://localhost:27017"
$env:Mongo__Database = "heron_integration"
$env:Farmadati__Username = "..."
$env:Farmadati__Password = "..."
$env:HERON_LOG_DIR = "D:\Heron\logs"
```

Chiavi principali:

| Chiave | Utilizzo |
| --- | --- |
| `Mongo:ConnectionString` | Connessione MongoDB |
| `Mongo:Database` | Database applicativo |
| `Heron:IncomingRoot` | Directory di ingresso del watcher legacy |
| `Heron:WorkingRoot` | Directory dei file presi in carico |
| `Farmadati:Username`, `Password` | Credenziali web service |
| `Farmadati:RootPath` | Directory temporanea dell'import completo |
| `Farmadati:ImagesEndpoint` | Download documenti e immagini |
| `HeronLogging:LogDirectory` | Directory log, superata da `HERON_LOG_DIR` |

Le credenziali dei fornitori attivi sono salvate nella collection `suppliers`.
Non inserire segreti reali nei file versionati; usare User Secrets, variabili
d'ambiente o il secret store dell'ambiente di deploy.

## 4. Dependency injection e lifetime

`AddHeronIntegrationCore` registra il nucleo condiviso da API ed Engine:

- `IMongoClient`, `IMongoDatabase` e repository;
- processor dei quattro step;
- client Farmadati e Magento;
- parser e servizi fornitori;
- manager singleton per cancellazione batch e Farmadati;
- servizio di rollback compensativo per MongoDB standalone.

I repository sono scoped. Un worker singleton deve creare uno scope con
`IServiceScopeFactory` per ogni ciclo o esecuzione e non deve conservare repository
tra due cicli.

Gli hosted service sono registrati esclusivamente in
`HeronIntegration.Engine/Program.cs`.

## 5. Ciclo di vita di un batch

Il documento principale è `batch_execution`; il suo `_id` è il `BatchId` usato
dalle collection di pipeline.

Un batch può essere creato manualmente da `POST /api/admin/batches/create` oppure
automaticamente da `CustomerCronBatchWorker`, in base al campo `Cron` dei customer
attivi. Il worker evita duplicati tramite `TriggerReason` e non crea un nuovo batch
se lo stesso customer ne ha già uno running.

Gli step standard, in ordine, sono:

1. `HeronImport`: legge l'XML e popola `raw_product`;
2. `Farmadati`: arricchisce e popola `enriched_product`;
3. `Suppliers`: risolve fornitore, disponibilità e prezzo in `resolved_product`;
4. `Magento`: inserisce/aggiorna prodotti, quantità e immagini.

`BatchOrchestratorWorker` interroga MongoDB ogni 10 secondi. Per ogni batch running
riprende uno step rimasto `Running` oppure esegue il successivo `Pending`. MongoDB
funge quindi da coda durevole e conserva lo stato dopo un riavvio dell'Engine.

Quando non rimangono step, l'orchestrator esegue il cron Magento, crea il report,
chiude il batch e rimuove i dati intermedi. Gli errori dello step sono salvati in
`step_execution.ErrorMessage`.

`HeronFileWatcherWorker` esiste nel codice ma non è registrato nell'host corrente:
non crea batch finché non viene aggiunto esplicitamente a `Program.cs`.

## 6. Worker e pianificazioni

Gli orari sono calcolati nel fuso locale della macchina che esegue l'Engine.

| Worker | Frequenza | Funzione |
| --- | --- | --- |
| `BatchOrchestratorWorker` | ogni 10 secondi | Avanza le pipeline running |
| `CustomerCronBatchWorker` | ogni 30 secondi | Crea batch secondo `Customer.Cron` |
| `NightBatchFinalizerService` | ogni giorno 00:00 | Finalizza batch aperti iniziati prima del giorno corrente |
| `BatchRetentionWorker` | ogni giorno 00:00 | Elimina batch terminati da oltre 7 giorni |
| `SupplierFileImporterWorker` | ogni giorno 01:00 | Sincronizza tutti i fornitori attivi |
| `WeeklyFarmadatiImportWorker` | domenica 22:00 | Esegue l'import Farmadati `Full` (`importType=1`) |

La sincronizzazione fornitori replica il flusso di
`GET /api/admin/suppliers/sync`: download dell'ultimo file FTP, parser specifico,
sostituzione dello snapshot `supplier_stock` e aggiornamento di `LastUpdate`.

## 7. Rollback su MongoDB standalone

L'installazione corrente è standalone e non supporta transazioni multi-documento.
`MongoCompensationService` implementa quindi un rollback compensativo mediante
collection temporanee `_rollback_*`:

- retention: salva soltanto i documenti dei batch da eliminare;
- supplier: salva lo stock del singolo fornitore prima della sostituzione;
- Farmadati: salva `farmadati_cache`, `fs.files` e `fs.chunks`.

Se l'operazione fallisce, i documenti correnti interessati vengono eliminati e il
backup viene reinserito per `_id`. Al termine le collection temporanee vengono
rimosse. Errore originale, avvio rollback, esito del ripristino ed eventuale errore
di cleanup sono registrati nei log.

Considerazioni operative:

- l'import Farmadati richiede spazio libero sufficiente a duplicare cache e GridFS;
- un arresto forzato del processo può lasciare una collection `_rollback_*`, da
  analizzare prima della rimozione manuale;
- chiamate già eseguite su FTP, SOAP, Magento o SSH non sono annullabili da MongoDB;
- i file temporanei Farmadati vengono rimossi dopo il rollback;
- non avviare contemporaneamente import Farmadati da API ed Engine: i singleton che
  rilevano un job running non sono condivisi tra i due processi.

## 8. Persistenza MongoDB

`MongoContext` è il catalogo centralizzato delle collection.

| Collection | Contenuto / relazione |
| --- | --- |
| `batch_execution` | Testata batch; relazione tramite `_id` |
| `step_execution` | Stato degli step; `BatchId` ObjectId |
| `export_execution` | Stato export per AIC; `BatchId` ObjectId |
| `raw_product` | Prodotti letti da Heron; `BatchId` ObjectId |
| `enriched_product` | Prodotti arricchiti; `BatchId` ObjectId |
| `resolved_product` | Prodotti risolti per export; `BatchId` ObjectId |
| `import_to_magento_status` | Contatori Magento; `BatchId` stringa |
| `batch_report` | Report finale; `BatchId` stringa |
| `customers` | Customer, cron e configurazione Magento |
| `suppliers` | Configurazioni e credenziali FTP fornitori |
| `supplier_stock` | Snapshot disponibilità/prezzo per fornitore |
| `farmadati_cache` | Catalogo Farmadati consolidato |
| `farmadati_updates` | Storico e progresso import Farmadati |
| `fs.files`, `fs.chunks` | Immagini Farmadati in GridFS |
| mapping/cache | Mapping categorie/produttori e cache gestionali |

Attenzione alla doppia rappresentazione del `BatchId`: le collection di pipeline
usano `ObjectId`, mentre report e stato Magento usano stringhe esadecimali.

## 9. API

L'API usa controller attribute-routed. Gruppi principali:

| Prefisso | Responsabilità |
| --- | --- |
| `/api/admin/batches` | Creazione, avvio, restart, stato e finalizzazione batch |
| `/api/admin/steps` | Esecuzione e retry step/pipeline |
| `/api/batches-report` | Report, storico e batch odierni |
| `/api/dashboard` | Stato aggregato e reindex |
| `/api/admin/customers` | CRUD customer e login |
| `/api/admin/suppliers` | CRUD e sincronizzazione singolo supplier |
| `/api/admin/supplier-stock` | Download/import/run singolo o massivo |
| `/api/farmadati-updates` | Storico e import completo Farmadati |
| `/api/category-mappings` | Mapping categorie |
| `/api/Producer-mappings` | Mapping produttori |
| `/api/product-to-exclude` | Esclusioni prodotto |
| `/api/admin/export` | Retry export per AIC o batch |
| `/api/Magento` | Operazioni Magento diagnostiche/amministrative |
| `/api/test/farmadati` | Endpoint diagnostici Farmadati |

Gli endpoint con effetti collaterali devono essere trattati come comandi anche dove
il codice storico usa `GET`.

### Sicurezza

Lo stato corrente richiede protezioni infrastrutturali:

- CORS è aperto a qualsiasi origine, header e metodo;
- non è configurato middleware standard di autenticazione/autorizzazione;
- diversi endpoint amministrativi espongono operazioni distruttive o costose;
- non è configurato rate limiting.

Prima dell'esposizione pubblica aggiungere autenticazione, policy admin, CORS
ristretto, rate limiting e conversione dei `GET` con side effect in `POST`/`DELETE`.

## 10. Logging

API ed Engine usano Serilog con file giornalieri condivisi:

- `application-YYYYMMDD.txt`: log applicativi generali;
- `farmadati-import-YYYYMMDD.txt`: import Farmadati nell'API;
- `magento-exporter-YYYYMMDD.txt`: operazioni Magento;
- `serilog-selflog.txt`: problemi interni di Serilog.

La directory è risolta in questo ordine:

1. variabile `HERON_LOG_DIR`;
2. `HeronLogging:LogDirectory`;
3. `C:\inetpub\wwwroot\logs`;
4. fallback `logs` accanto all'eseguibile.

Per investigare un batch cercare il suo ObjectId in application e Magento log, poi
controllare `batch_execution`, `step_execution`, `import_to_magento_status` e
`batch_report`.

## 11. Diagnostica e smoke test

### Pipeline batch

1. Avviare MongoDB, API ed Engine.
2. Creare o scegliere un customer attivo con Magento e `HeronFolder` validi.
3. Creare il batch da API oppure attendere il cron customer.
4. Verificare `batch_execution` e i quattro record `step_execution`.
5. Seguire il `BatchId` nei log e nelle collection intermedie.
6. A chiusura verificare `batch_report` e lo stato `Closed`.

### Supplier

1. Verificare `Active=true`, credenziali FTP e parser per il codice.
2. Eseguire `/api/admin/suppliers/sync?code=...`.
3. Controllare `supplier_stock` e `suppliers.LastUpdate`.
4. Per testare il rollback provocare un errore di scrittura in ambiente non
   produttivo e verificare log e assenza di collection `_rollback_*` residue.

### Farmadati

1. Verificare credenziali, spazio disco e raggiungibilità SOAP.
2. Eseguire `POST /api/farmadati-updates/import-full?importType=1` soltanto se il
   worker settimanale non è attivo.
3. Controllare `farmadati_updates`, log dedicato, cache e GridFS.

## 12. Regole per le modifiche

- Non registrare hosted service nell'API.
- Non iniettare servizi scoped direttamente in un `BackgroundService`; creare scope.
- Propagare `CancellationToken` nelle operazioni lunghe.
- Isolare gli errori per batch o supplier per non fermare l'intero worker.
- Aggiornare questa guida quando cambiano orari, collection o ordine degli step.
- Verificare sempre con:

```powershell
dotnet build HeronIntegrationSystem.sln --no-restore
git diff --check
```
