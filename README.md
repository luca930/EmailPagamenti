# EmailPagamenti

Legge una casella di posta, riconosce le email che riguardano pagamenti, ne estrae importo,
esercente, data e riferimento, e le salva su database per poterle ritrovare mesi dopo.

Scritto in C# su .NET 9. Lo scaffold e' pensato per essere avviato oggi con SQLite e una
casella IMAP, e spostato domani su Postgres o su Gmail API senza riscrivere la pipeline.

## Come funziona

```
IMAP  ──▶  deduplica  ──▶  classificazione a regole  ──▶  database  ──▶  ricerca
        (Message-Id)         (mittente, parole,          (EF Core)
                              importo, allegati)
```

1. **Acquisizione.** `ImapEmailSource` apre la cartella in sola lettura e restituisce le email
   in streaming a partire dall'ultima gia' vista, con una sovrapposizione configurabile perche'
   la posta puo' arrivare fuori ordine.
2. **Deduplica.** Ogni email e' identificata dall'hash SHA-256 del `Message-Id`, con indice unico
   a database. Gli hash di un intero lotto si controllano con una sola query.
3. **Classificazione.** `RuleBasedPaymentExtractor` assegna un punteggio di confidenza sommando
   segnali: regola esercente, mittente attendibile, parole chiave, importo riconosciuto, allegato PDF.
   Ogni riga salvata porta con se' la regola che l'ha prodotta, quindi un falso positivo si spiega.
4. **Persistenza.** Sopra `MinConfidence` la riga e' classificata, sotto finisce in `NeedsReview`,
   piu' in basso viene scartata. Gli indici sono su data, esercente e stato: le ricerche tipiche.

## Struttura

| Progetto | Contiene |
| --- | --- |
| `EmailPagamenti.Domain` | Entita', enum e il value object `Money`. Nessuna dipendenza esterna. |
| `EmailPagamenti.Application` | Interfacce, regole di parsing, pipeline di acquisizione. |
| `EmailPagamenti.Infrastructure` | IMAP (MailKit), EF Core, registrazione dei servizi. |
| `EmailPagamenti.Worker` | Servizio che esegue una passata ogni `PollInterval`. |
| `EmailPagamenti.Tests` | Test su parser degli importi e regole di classificazione. |

## Avvio rapido

```bash
git clone https://github.com/luca930/EmailPagamenti.git
cd EmailPagamenti
dotnet restore
dotnet test
```

Poi i segreti, che non vanno mai in `appsettings.json`:

```bash
cd src/EmailPagamenti.Worker
dotnet user-secrets set "Imap:UserName" "tuo.indirizzo@gmail.com"
dotnet user-secrets set "Imap:Password" "password-per-app-a-16-cifre"
dotnet user-secrets set "ConnectionStrings:Payments" "Data Source=pagamenti.db"
dotnet run
```

In esecuzione fuori da sviluppo si usano le variabili d'ambiente:

```bash
export Imap__UserName='tuo.indirizzo@gmail.com'
export Imap__Password='...'
export ConnectionStrings__Payments='Host=localhost;Database=pagamenti;Username=app;Password=...'
export Database__Provider='Postgres'
```

## Configurazione

`appsettings.json` contiene solo valori non sensibili.

| Sezione | Chiave | Significato |
| --- | --- | --- |
| `Imap` | `Host`, `Port`, `Folders` | Server e cartelle da leggere. Porta 993 con TLS implicito. |
| `Imap` | `MaxMessagesPerRun` | Tetto per passata, per non bloccarsi su una casella storica enorme. |
| `Ingestion` | `InitialLookback` | Quanto indietro guardare la prima volta. |
| `Ingestion` | `Overlap` | Quanto ripescare all'indietro a ogni passata. |
| `Ingestion` | `MinConfidence` | Sotto questa soglia la riga e' marcata `NeedsReview`. |
| `Ingestion` | `ReviewThreshold` | Sotto questa soglia l'email non viene salvata. |
| `Ingestion` | `BodySnippetLength` | Caratteri di corpo conservati. A `0` non se ne salva nessuno. |
| `Classification` | `TrustedSenders` | Domini di cui ci si fida: alzano molto la confidenza. |
| `Classification` | `Merchants` | Regole per esercente, valutate prima di quelle generiche. |
| `Database` | `Provider` | `Sqlite` oppure `Postgres`. |

Aggiungere un esercente non richiede di ricompilare:

```json
{
  "Classification": {
    "Merchants": [
      { "Name": "Enel", "SenderContains": "enel.it", "Kind": "Invoice", "Direction": "Outgoing" }
    ]
  }
}
```

## Migrazioni

Alla prima esecuzione, se non esiste ancora nessuna migrazione, lo schema viene creato con
`EnsureCreated` cosi' il progetto parte appena clonato. Per andare in esercizio servono le migrazioni:

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add Iniziale \
  --project src/EmailPagamenti.Infrastructure \
  --startup-project src/EmailPagamenti.Worker
```

## Sicurezza

Le scelte prese, e il perche':

- **Nessun segreto nel repository.** Credenziali solo da user-secrets o variabili d'ambiente;
  `.gitignore` esclude `appsettings.*.Local.json`, `.env` e i database locali.
- **Casella in sola lettura.** La cartella IMAP viene aperta con `FolderAccess.ReadOnly`: il
  programma non puo' cancellare o modificare la posta nemmeno per errore.
- **TLS obbligatorio.** Porta 993 con TLS implicito; la modalita' in chiaro esiste solo per i
  server di test locali.
- **Minimizzazione dei dati.** Non si salva il corpo completo delle email ne' il contenuto degli
  allegati: solo un estratto configurabile (azzerabile) e i metadati.
- **Password per app.** Su Gmail e Outlook si usa una password dedicata o OAuth, mai quella
  dell'account.
- **Analisi statica.** CodeQL gira a ogni push e ogni lunedi'.

## Stato

Scaffold funzionante con classificazione a regole. Le estensioni gia' previste dalle interfacce:

- `IEmailSource` per Gmail API o Microsoft Graph al posto di IMAP.
- `IPaymentExtractor` per affiancare alle regole un classificatore statistico sui casi incerti.
- Una interfaccia web minimale sopra `IPaymentEmailRepository.SearchAsync` per la ricerca.
