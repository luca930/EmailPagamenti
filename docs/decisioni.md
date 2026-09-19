# Decisioni prese

## Cosa fa davvero questa applicazione

Le email in arrivo sono le **notifiche di movimento del conto ING** (bonifici, pagamenti con
carta, prelievi, accrediti), non ricevute di negozi. Cambia tutto: il testo e' prevedibile e a
campi etichettati, quindi si legge con precisione invece che a intuito, e la domanda a cui
rispondere non e' "dov'e' quella ricevuta" ma "quanto ho speso questo mese, e in cosa".

## Scelte e motivi

### Gli importi sono interi in centesimi

Sembra un dettaglio ed e' la decisione piu' importante del progetto. SQLite tiene i `decimal`
come testo: non li sa ordinare, e li somma convertendoli in virgola mobile. Con gli importi in
centesimi ogni totale e' esatto e ogni ordinamento funziona, su SQLite come su Postgres.
Il dominio continua a esporre `decimal`: la conversione sta ai bordi.

Lo stesso vale per le date, che sono `DateTime` in UTC e non `DateTimeOffset`, perche' SQLite
non sa ordinare nemmeno quelli. Ci si e' arrivati per la via difficile, con i test.

### Regole e non un modello

Le notifiche bancarie sono ripetitive e arrivano da un mittente solo. Le regole danno risultati
spiegabili, girano in microsecondi e non mandano i movimenti del conto a un servizio esterno.
`ITransactionExtractor` resta l'aggancio se un giorno servisse un modello sui soli casi finiti
in revisione.

### Tre stati invece di un booleano

`Classified`, `NeedsReview` e `Ignored`. Senza la fascia intermedia si sceglie tra perdere
movimenti veri e riempire il database di rumore. Quello che finisce in revisione la dashboard
lo mostra in evidenza, e una correzione umana porta la riga a `Classified` per sempre.

### Un contenitore solo

L'acquisizione periodica gira dentro la stessa applicazione web. Su un server di casa un
contenitore e' piu' semplice da aggiornare e da salvare di due. `EmailPagamenti.Worker` resta
per chi volesse la sola acquisizione senza interfaccia.

### Interfaccia senza librerie

Nessun framework, nessun passo di build, nessun CDN: i grafici sono SVG costruiti a mano. Cosi'
la `Content-Security-Policy` puo' essere stretta davvero, la pagina funziona anche se il server
non ha internet, e non c'e' una catena di dipendenze npm da sorvegliare.

I due colori dei grafici sono gli slot 1 e 2 di una palette verificata per la cecita' ai colori,
controllata in modalita' chiara e scura. Le categorie usano barre orizzontali e non una ciambella:
con otto voci il confronto tra lunghezze si legge a colpo d'occhio e le etichette stanno accanto
al dato.

### Deduplica sull'hash del Message-Id

E' l'unico identificatore stabile tra riletture della stessa casella e tra cartelle diverse: gli
UID IMAP cambiano se la cartella viene ricreata. L'hash tiene l'indice a lunghezza fissa.

## Cosa resta da confermare

| Tema | Stato |
| --- | --- |
| Bonifico ING in uscita | Verificato su una notifica reale, coperto da test |
| Altre notifiche ING (carta, prelievo, addebito, accredito) | Formule plausibili, **da confermare su un esempio vero** |
| Categorie | Elenco di partenza sulle catene italiane piu' diffuse, da estendere con l'uso |
| Allegati | Si salvano nome e tipo, non il file. Le contabili PDF di ING restano nell'Area Riservata |
| Database | SQLite per l'uso in casa; Postgres cambiando una sola chiave |
