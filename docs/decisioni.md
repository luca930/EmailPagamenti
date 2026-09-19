# Decisioni prese nello scaffold

La conversazione originale non era leggibile da qui, quindi questi sono i default scelti.
Ognuno e' isolato dietro un'interfaccia o una chiave di configurazione: cambiarlo costa poco.

## Da confermare

| Tema | Default scelto | Alternative |
| --- | --- | --- |
| Accesso alla posta | IMAP con MailKit | Gmail API, Microsoft Graph |
| Database | SQLite in sviluppo, Postgres in esercizio | SQL Server, MySQL |
| Cosa si estrae | importo, valuta, esercente, data, riferimento, verso, tipo | IVA, metodo di pagamento, IBAN |
| Corpo delle email | estratto di 500 caratteri | niente corpo, oppure corpo completo cifrato |
| Allegati | solo nome e tipo | scaricare e archiviare le fatture PDF |
| Interfaccia | nessuna, solo worker | web minimale per ricerca e revisione |

## Perche' una classificazione a regole e non un modello

Le email di pagamento sono ripetitive e arrivano da pochi mittenti ricorrenti. Le regole
danno risultati spiegabili, girano in microsecondi e non mandano dati personali a servizi
esterni. `IPaymentExtractor` resta l'aggancio per affiancare un modello sui soli casi
finiti in `NeedsReview`.

## Perche' l'hash del Message-Id come chiave di deduplica

Il `Message-Id` e' l'unico identificatore stabile tra riletture della stessa casella e tra
cartelle diverse: gli UID IMAP cambiano se la cartella viene ricreata. L'hash tiene l'indice
a lunghezza fissa anche con `Message-Id` molto lunghi.

## Perche' tre stati invece di un booleano

`Classified`, `NeedsReview` e `Ignored` separano il caso sicuro da quello dubbio. Senza la
fascia intermedia si sceglie tra perdere pagamenti veri e riempire il database di rumore.
