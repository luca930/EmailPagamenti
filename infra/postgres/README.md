# Postgres condiviso

Un container Postgres solo, da avviare una volta sul server, che poi tutte le applicazioni
usano ciascuna con la propria base dati. Non fa parte del compose di EmailPagamenti apposta:
si avvia e si aggiorna per conto suo, e resta su anche se un'applicazione viene rifatta da zero.

## Avvio, una volta sola

```bash
cd infra/postgres
cp .env.example .env
$EDITOR .env          # password del superutente
docker compose up -d
```

## Aggiungere una base dati per una nuova applicazione

Ogni applicazione ha il proprio utente e la propria base dati: nessuna vede quella delle altre.

```bash
docker exec -it postgres-condiviso psql -U postgres
```

```sql
CREATE DATABASE nome_applicazione;
CREATE USER nome_applicazione WITH ENCRYPTED PASSWORD 'una-password-lunga-e-diversa';
GRANT ALL PRIVILEGES ON DATABASE nome_applicazione TO nome_applicazione;
\c nome_applicazione
GRANT ALL ON SCHEMA public TO nome_applicazione;
```

L'applicazione (EmailPagamenti compreso) usa poi una stringa di connessione con questo host,
username e password:

```
Host=postgres-condiviso;Port=5432;Database=nome_applicazione;Username=nome_applicazione;Password=...
```

L'host e' `postgres-condiviso` (il nome del container) e non `localhost`: funziona perche' ogni
container dell'applicazione si collega alla stessa rete Docker `condiviso`, che questo compose
crea. Nel docker-compose.yml dell'applicazione basta:

```yaml
networks:
  condiviso:
    external: true
```

e aggiungere quella rete al servizio dell'applicazione.

## Backup

Tutti i dati vivono nel volume `postgres-condiviso-dati`. Un dump di tutte le basi dati insieme:

```bash
docker exec postgres-condiviso pg_dumpall -U postgres > backup-$(date +%F).sql
```

## Perche' cosi' e non un database per applicazione

Un Postgres solo consuma meno memoria di uno per applicazione, ed e' un solo posto da
aggiornare e da salvare. Il costo e' che un riavvio del container ferma tutte le applicazioni
insieme: su un homelab, dove le applicazioni sono poche e gestite da una persona sola, il
risparmio conta piu' di quell'isolamento.
