# Documentazione sviluppatore

## Obiettivo

`HeronIntegrationSystem` integra file Heron, Farmadati, stock fornitori e Magento.
La solution e pensata come due host separati che condividono la logica applicativa:

- `HeronIntegration.Api`: espone controller HTTP per dashboard, batch, mapping e operazioni amministrative.
- `HeronIntegration.Engine`: esegue worker background per intake file, orchestrazione batch e import stock fornitori.
- `HeronIntegration.Shared`: contiene entita, DTO, enum e singleton condivisi.

## Flusso batch

1. `HeronFileWatcherWorker` controlla `Heron:IncomingRoot`.
2. Per ogni XML in una cartella customer crea un batch `Running`.
3. Crea gli step standard: `HeronImport`, `Farmadati`, `Suppliers`, `Magento`.
4. Sposta il file in `Heron:WorkingRoot`.
5. `BatchOrchestratorWorker` prende i batch running e avanza uno step alla volta.
6. Quando non ci sono step pending, esegue cron Magento, salva il report e chiude il batch.

## Requisiti locali

- .NET SDK 9.x.
- MongoDB locale o raggiungibile via connection string.
- Accessi esterni solo per test reali verso Farmadati, FTP fornitori e Magento.

Comandi principali:

```powershell
dotnet --version
dotnet build HeronIntegrationSystem.sln
dotnet run --project HeronIntegration.Api\HeronIntegration.Api.csproj
dotnet run --project HeronIntegration.Engine\HeronIntegration.Engine.csproj
```

## Configurazione

File principali:

- `HeronIntegration.Api/appsettings.json`
- `HeronIntegration.Api/appsettings.Development.json`
- `HeronIntegration.Engine/appsettings.json`

Non versionare segreti reali. Usare variabili d'ambiente o User Secrets.

```powershell
$env:Mongo__ConnectionString = "mongodb://localhost:27017"
$env:Mongo__Database = "heron_integration"
$env:Farmadati__Username = "..."
$env:Farmadati__Password = "..."
$env:SupplierFtp__SOFARMA__Host = "..."
$env:SupplierFtp__SOFARMA__Username = "..."
$env:SupplierFtp__SOFARMA__Password = "..."
```

Chiavi importanti:

- `Mongo:ConnectionString`: connection string MongoDB.
- `Mongo:Database`: database applicativo.
- `Heron:IncomingRoot`: cartella monitorata dal watcher.
- `Heron:WorkingRoot`: cartella di lavoro dopo presa in carico.
- `Farmadati:Endpoint`: endpoint SOAP Farmadati.
- `Farmadati:Username` e `Farmadati:Password`: credenziali Farmadati.
- `SupplierFtp:{CODE}:Host`, `Username`, `Password`, `RemoteFolder`: FTP dei client supplier dedicati.
- `Suppliers`: lista usata da `SupplierFileImporterWorker` per import periodico file stock.

## Dependency injection

Le registrazioni condivise stanno in:

`HeronIntegration.Engine/DependencyInjection/HeronIntegrationServiceCollectionExtensions.cs`

Regole:

- servizi comuni ad API e worker: `AddHeronIntegrationCore`.
- hosted service: solo in `HeronIntegration.Engine/Program.cs`.
- controller, CORS, middleware HTTP e logging web: solo in `HeronIntegration.Api/Program.cs`.
- segreti: mai nei costruttori e mai hardcoded, solo configurazione.

## Worker

### BatchOrchestratorWorker

Scopo: avanzare i batch running.

Comportamento:

- poll ogni 10 secondi;
- crea uno scope DI per ciclo;
- recupera batch running;
- esegue il prossimo step pending;
- finalizza il batch quando non ci sono step pending;
- logga errori per batch senza fermare il worker;
- esce pulito su cancellazione host.

Diagnosi:

- cercare log `Batch Orchestrator started`;
- se Mongo non risponde, il worker logga errore e riprova al ciclo successivo;
- se uno step fallisce, controllare `step_execution.ErrorMessage`.

### HeronFileWatcherWorker

Scopo: creare batch da file XML Heron.

Comportamento:

- valida `Heron:IncomingRoot`;
- legge sottocartelle per customer;
- processa file `*.xml`;
- crea batch e step standard;
- sposta il file in `Heron:WorkingRoot`;
- logga errori per singolo file senza fermare la scansione.

Struttura attesa:

```text
IncomingRoot/
  CUSTOMER_ID/
    file.xml
WorkingRoot/
  CUSTOMER_ID/
    file.xml
```

### SupplierFileImporterWorker

Scopo: aggiornare periodicamente gli stock fornitori.

Comportamento:

- poll ogni 30 minuti;
- legge la sezione `Suppliers`;
- scarica il file FTP con FluentFTP;
- parse CSV `AIC;PRICE;AVAILABILITY`;
- sostituisce lo snapshot stock del supplier;
- scarta righe non valide con warning.

### SupplierStockProcessor

Scopo: download/import manuale o orchestrato per supplier attivi da Mongo.

Comportamento:

- usa configurazioni supplier salvate a database;
- scarica il file FTP piu recente;
- se il download fallisce, prova a usare l'ultimo file locale;
- importa il file tramite parser specifico.

## Verifica worker

Build:

```powershell
dotnet build HeronIntegrationSystem.sln
```

Avvio Engine:

```powershell
dotnet run --project HeronIntegration.Engine\HeronIntegration.Engine.csproj
```

Controlli attesi:

- il processo resta attivo;
- console mostra avvio dei worker;
- se `Heron:IncomingRoot` non esiste, compare warning ma il processo non termina;
- se Mongo e spento, l'orchestrator logga errore e ritenta;
- se `Suppliers` e vuoto, il supplier importer logga debug e non fallisce.

Smoke test manuale Heron:

1. Avviare MongoDB.
2. Creare cartella `Heron:IncomingRoot\CUSTOMER_ID`.
3. Inserire un XML Heron valido.
4. Avviare `HeronIntegration.Engine`.
5. Verificare creazione documenti in `batch_execution` e `step_execution`.
6. Verificare spostamento file in `Heron:WorkingRoot\CUSTOMER_ID`.

## API

I controller sono in `HeronIntegration.Api/Controllers`.

Attenzione prima di esposizione pubblica:

- aggiungere autenticazione;
- aggiungere autorizzazioni per endpoint admin;
- restringere CORS;
- preferire `POST` per operazioni con effetti collaterali;
- aggiungere rate limiting su import/export.

## Persistenza

`MongoContext` centralizza l'accesso alle collection.

Collection principali:

- `batch_execution`
- `step_execution`
- `export_execution`
- `raw_product`
- `enriched_product`
- `resolved_product`
- `supplier_stock`
- `customers`
- `farmadati_cache`
- `batch_report`

