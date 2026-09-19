// Interfaccia delle spese. Nessuna libreria: i grafici sono SVG costruiti a mano, cosi'
// la policy dei contenuti resta stretta e la pagina funziona anche senza rete.

const euro = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' });
const euroCorto = new Intl.NumberFormat('it-IT', { maximumFractionDigits: 0 });
const giorno = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: 'short' });
const meseAnno = new Intl.DateTimeFormat('it-IT', { month: 'long', year: 'numeric' });
const meseCorto = new Intl.DateTimeFormat('it-IT', { month: 'short' });

const SVG = 'http://www.w3.org/2000/svg';

const stato = {
  mese: primoDelMese(new Date()),
  pagina: 1,
  categorie: [],
  selezionato: null,
};

const el = (id) => document.getElementById(id);

// ---------- Utilita' ----------

function primoDelMese(data) {
  return new Date(Date.UTC(data.getUTCFullYear(), data.getUTCMonth(), 1));
}

function iso(data) {
  return data.toISOString().slice(0, 10);
}

function finePeriodo() {
  const d = new Date(stato.mese);
  d.setUTCMonth(d.getUTCMonth() + 1);
  d.setUTCDate(0);
  return d;
}

function periodoQuery() {
  return `from=${iso(stato.mese)}&to=${iso(finePeriodo())}`;
}

function avvisa(testo) {
  const nodo = el('avviso');
  nodo.textContent = testo;
  nodo.hidden = false;
  clearTimeout(nodo.dataset.timer);
  nodo.dataset.timer = setTimeout(() => { nodo.hidden = true; }, 3200);
}

async function chiedi(percorso, opzioni = {}) {
  const risposta = await fetch(percorso, {
    credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json' },
    ...opzioni,
  });

  if (risposta.status === 401) {
    mostraAccesso();
    throw new Error('sessione scaduta');
  }

  if (!risposta.ok) {
    throw new Error(`richiesta fallita: ${risposta.status}`);
  }

  return risposta.status === 204 ? null : risposta.json();
}

function svg(larghezza, altezza) {
  const nodo = document.createElementNS(SVG, 'svg');
  nodo.setAttribute('viewBox', `0 0 ${larghezza} ${altezza}`);
  nodo.setAttribute('role', 'img');
  return nodo;
}

function crea(nome, attributi) {
  const nodo = document.createElementNS(SVG, nome);
  for (const [chiave, valore] of Object.entries(attributi)) {
    nodo.setAttribute(chiave, valore);
  }
  return nodo;
}

function titolo(nodo, testo) {
  const t = document.createElementNS(SVG, 'title');
  t.textContent = testo;
  nodo.appendChild(t);
  return nodo;
}

// ---------- Grafico: uscite per categoria ----------

// Barre orizzontali e non una ciambella: con otto categorie il confronto tra lunghezze
// si legge a colpo d'occhio, e le etichette stanno accanto al dato invece che in legenda.
function disegnaCategorie(righe) {
  const contenitore = el('grafico-categorie');
  contenitore.replaceChildren();
  el('categorie-vuoto').hidden = righe.length > 0;

  if (righe.length === 0) {
    return;
  }

  const visibili = righe.slice(0, 8);
  const altezzaRiga = 34;
  const larghezza = 640;
  const etichetta = 170;
  const valore = 84;
  const altezza = visibili.length * altezzaRiga;
  const massimo = Math.max(...visibili.map((r) => r.total));
  const pista = larghezza - etichetta - valore;

  const disegno = svg(larghezza, altezza);
  disegno.setAttribute('aria-label', 'Uscite per categoria');

  visibili.forEach((riga, indice) => {
    const y = indice * altezzaRiga;
    const lunghezza = massimo > 0 ? Math.max((riga.total / massimo) * pista, 3) : 3;
    const gruppo = crea('g', { class: 'gruppo-barre' });

    gruppo.appendChild(crea('text', {
      x: etichetta - 12, y: y + 20, 'text-anchor': 'end', class: 'etichetta-grafico',
    })).textContent = riga.category;

    // Estremita' arrotondate solo sul lato del dato, la base resta ancorata all'asse.
    gruppo.appendChild(crea('rect', {
      x: etichetta, y: y + 8, width: lunghezza, height: 16, rx: 4,
      class: 'barra barra-uscite',
    }));

    gruppo.appendChild(crea('text', {
      x: etichetta + lunghezza + 10, y: y + 20, class: 'valore-grafico',
    })).textContent = euro.format(riga.total);

    titolo(gruppo, `${riga.category}: ${euro.format(riga.total)} in ${riga.count} movimenti`);
    disegno.appendChild(gruppo);
  });

  contenitore.appendChild(disegno);
}

// ---------- Grafico: andamento mensile ----------

function disegnaAndamento(punti) {
  const contenitore = el('grafico-andamento');
  contenitore.replaceChildren();

  if (punti.length === 0) {
    return;
  }

  const larghezza = 720;
  const altezza = 210;
  const bassoUtile = 26;
  const altoUtile = 10;
  const pista = altezza - bassoUtile - altoUtile;
  const passo = larghezza / punti.length;
  const larghezzaBarra = Math.min((passo - 10) / 2, 22);
  const massimo = Math.max(...punti.flatMap((p) => [p.spent, p.received]), 1);

  const disegno = svg(larghezza, altezza);
  disegno.setAttribute('aria-label', 'Uscite ed entrate degli ultimi dodici mesi');

  disegno.appendChild(crea('line', {
    x1: 0, y1: altezza - bassoUtile, x2: larghezza, y2: altezza - bassoUtile, class: 'griglia',
  }));

  punti.forEach((punto, indice) => {
    const centro = indice * passo + passo / 2;
    const gruppo = crea('g', { class: 'gruppo-barre' });
    const data = new Date(`${punto.date}T00:00:00Z`);

    const coppie = [
      { valore: punto.spent, classe: 'barra-uscite', scarto: -larghezzaBarra - 1 },
      { valore: punto.received, classe: 'barra-entrate', scarto: 1 },
    ];

    for (const { valore, classe, scarto } of coppie) {
      const h = massimo > 0 ? Math.max((valore / massimo) * pista, valore > 0 ? 2 : 0) : 0;
      if (h === 0) {
        continue;
      }

      gruppo.appendChild(crea('rect', {
        x: centro + scarto, y: altezza - bassoUtile - h,
        width: larghezzaBarra, height: h, rx: 4,
        class: `barra ${classe}`,
      }));
    }

    gruppo.appendChild(crea('text', {
      x: centro, y: altezza - 8, 'text-anchor': 'middle', class: 'etichetta-grafico',
    })).textContent = meseCorto.format(data);

    titolo(
      gruppo,
      `${meseAnno.format(data)}: ${euro.format(punto.spent)} di uscite, ${euro.format(punto.received)} di entrate`,
    );
    disegno.appendChild(gruppo);
  });

  // Il mese piu' recente porta il valore scritto: un numero dove serve, non su ogni barra.
  const ultimo = punti[punti.length - 1];
  if (ultimo.spent > 0) {
    const centro = (punti.length - 1) * passo + passo / 2;
    const h = (ultimo.spent / massimo) * pista;
    disegno.appendChild(crea('text', {
      x: centro - larghezzaBarra / 2 - 1, y: altezza - bassoUtile - h - 6,
      'text-anchor': 'middle', class: 'valore-grafico',
    })).textContent = euroCorto.format(ultimo.spent) + ' €';
  }

  contenitore.appendChild(disegno);
}

// ---------- Tabella dei movimenti ----------

function filtriCorrenti() {
  const parametri = new URLSearchParams();
  parametri.set('from', iso(stato.mese));
  parametri.set('to', iso(finePeriodo()));
  parametri.set('page', String(stato.pagina));
  parametri.set('pageSize', '25');

  const aggiungi = (chiave, valore) => {
    if (valore) {
      parametri.set(chiave, valore);
    }
  };

  aggiungi('text', el('filtro-testo').value.trim());
  aggiungi('category', el('filtro-categoria').value);
  aggiungi('direction', el('filtro-verso').value);
  aggiungi('minAmount', el('filtro-min').value);
  aggiungi('maxAmount', el('filtro-max').value);

  if (el('filtro-revisione').checked) {
    parametri.set('needsReview', 'true');
  }

  return parametri.toString();
}

function disegnaMovimenti(pagina) {
  const corpo = el('corpo-movimenti');
  corpo.replaceChildren();
  el('movimenti-vuoto').hidden = pagina.items.length > 0;

  for (const movimento of pagina.items) {
    const riga = document.createElement('tr');
    riga.tabIndex = 0;

    const data = document.createElement('td');
    data.className = 'cella-data';
    data.textContent = giorno.format(new Date(movimento.occurredAt));

    const esercente = document.createElement('td');
    esercente.className = 'cella-esercente';
    esercente.textContent = movimento.merchant || movimento.subject || '—';
    if (movimento.needsReview) {
      const segno = document.createElement('span');
      segno.className = 'segno-revisione';
      segno.textContent = 'da rivedere';
      esercente.appendChild(segno);
    }

    const categoria = document.createElement('td');
    const etichetta = document.createElement('span');
    etichetta.className = 'etichetta';
    etichetta.textContent = movimento.category;
    categoria.appendChild(etichetta);

    const importo = document.createElement('td');
    importo.className = 'cella-importo destra';
    if (movimento.direction === 'Incoming') {
      importo.classList.add('importo-entrata');
    }
    importo.textContent = movimento.amount == null
      ? '—'
      : (movimento.direction === 'Incoming' ? '+' : '−') + euro.format(movimento.amount);

    riga.append(data, esercente, categoria, importo);
    riga.addEventListener('click', () => apriDettaglio(movimento));
    riga.addEventListener('keydown', (evento) => {
      if (evento.key === 'Enter' || evento.key === ' ') {
        evento.preventDefault();
        apriDettaglio(movimento);
      }
    });

    corpo.appendChild(riga);
  }

  const ultimaPagina = Math.max(1, Math.ceil(pagina.total / pagina.pageSize));
  el('paginazione-stato').textContent = `${pagina.total} movimenti · pagina ${pagina.page} di ${ultimaPagina}`;
  el('pagina-precedente').disabled = pagina.page <= 1;
  el('pagina-successiva').disabled = pagina.page >= ultimaPagina;
}

// ---------- Dettaglio e correzione ----------

function apriDettaglio(movimento) {
  stato.selezionato = movimento;

  el('dettaglio-data').textContent = new Intl.DateTimeFormat('it-IT', {
    dateStyle: 'full',
  }).format(new Date(movimento.occurredAt));

  el('dettaglio-importo').textContent = movimento.amount == null
    ? 'Importo non riconosciuto'
    : euro.format(movimento.amount);

  el('dettaglio-oggetto').textContent = movimento.subject || '';
  el('dettaglio-esercente').value = movimento.merchant || '';
  el('dettaglio-modifica-importo').value = movimento.amount ?? '';
  el('dettaglio-verso').value = movimento.direction === 'Incoming' ? 'Incoming' : 'Outgoing';

  const categoria = el('dettaglio-categoria');
  categoria.replaceChildren();
  for (const nome of stato.categorie) {
    const opzione = document.createElement('option');
    opzione.value = nome;
    opzione.textContent = nome;
    categoria.appendChild(opzione);
  }
  categoria.value = movimento.category;

  el('dettaglio-regola').textContent = movimento.matchedRule
    ? `Riconosciuto da: ${movimento.matchedRule}`
    : '';

  el('dettaglio').showModal();
}

async function salvaCorrezione() {
  if (!stato.selezionato) {
    return;
  }

  const importo = el('dettaglio-modifica-importo').value;

  try {
    await chiedi(`/api/transactions/${stato.selezionato.id}`, {
      method: 'PUT',
      body: JSON.stringify({
        direction: el('dettaglio-verso').value,
        kind: stato.selezionato.kind,
        amount: importo === '' ? null : Number(importo),
        currency: stato.selezionato.currency || 'EUR',
        merchant: el('dettaglio-esercente').value.trim() || null,
        category: el('dettaglio-categoria').value,
      }),
    });

    el('dettaglio').close();
    avvisa('Movimento aggiornato.');
    await caricaTutto();
  } catch {
    avvisa('Non sono riuscito a salvare la correzione.');
  }
}

// ---------- Caricamento ----------

async function caricaTutto() {
  el('periodo-nome').textContent = meseAnno.format(stato.mese);

  const periodo = periodoQuery();

  const [riepilogo, categorie, mensile, movimenti] = await Promise.all([
    chiedi(`/api/summary?${periodo}`),
    chiedi(`/api/categories?${periodo}`),
    chiedi('/api/timeline/monthly?months=12'),
    chiedi(`/api/transactions?${filtriCorrenti()}`),
  ]);

  el('totale-uscite').textContent = euro.format(riepilogo.spent);
  el('totale-entrate').textContent = euro.format(riepilogo.received);
  el('totale-saldo').textContent = euro.format(riepilogo.net);
  el('totale-movimenti').textContent = String(riepilogo.count);

  el('nota-saldo').textContent = riepilogo.net >= 0 ? 'messi da parte' : 'in rosso';

  const daRivedere = el('vai-revisione');
  daRivedere.hidden = riepilogo.needsReview === 0;
  daRivedere.textContent = `${riepilogo.needsReview} da rivedere`;

  disegnaCategorie(categorie);
  disegnaAndamento(mensile);
  disegnaMovimenti(movimenti);
}

async function caricaMovimenti() {
  disegnaMovimenti(await chiedi(`/api/transactions?${filtriCorrenti()}`));
}

async function caricaMeta() {
  const meta = await chiedi('/api/meta');
  stato.categorie = meta.categories;

  const filtro = el('filtro-categoria');
  filtro.replaceChildren();

  const tutte = document.createElement('option');
  tutte.value = '';
  tutte.textContent = 'Tutte';
  filtro.appendChild(tutte);

  for (const nome of meta.categories) {
    const opzione = document.createElement('option');
    opzione.value = nome;
    opzione.textContent = nome;
    filtro.appendChild(opzione);
  }
}

// ---------- Accesso ----------

function mostraAccesso() {
  el('accesso').hidden = false;
  el('applicazione').hidden = true;
  document.body.dataset.stato = 'accesso';
}

async function mostraApplicazione() {
  el('accesso').hidden = true;
  el('applicazione').hidden = false;
  document.body.dataset.stato = 'pronto';

  await caricaMeta();
  await caricaTutto();
}

// ---------- Avvio ----------

function ritarda(funzione, attesa) {
  let timer;
  return (...argomenti) => {
    clearTimeout(timer);
    timer = setTimeout(() => funzione(...argomenti), attesa);
  };
}

function collegaEventi() {
  el('modulo-accesso').addEventListener('submit', async (evento) => {
    evento.preventDefault();
    el('accesso-errore').hidden = true;

    const risposta = await fetch('/api/accesso', {
      method: 'POST',
      credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ password: el('password').value }),
    });

    if (risposta.ok) {
      el('password').value = '';
      await mostraApplicazione();
    } else {
      el('accesso-errore').textContent = risposta.status === 429
        ? 'Troppi tentativi. Riprova tra un minuto.'
        : 'Password non corretta.';
      el('accesso-errore').hidden = false;
    }
  });

  el('esci').addEventListener('click', async () => {
    await fetch('/api/uscita', { method: 'POST', credentials: 'same-origin' });
    mostraAccesso();
  });

  el('tema').addEventListener('click', () => {
    const scuro = document.documentElement.dataset.theme === 'dark';
    document.documentElement.dataset.theme = scuro ? 'light' : 'dark';
    try {
      localStorage.setItem('tema', scuro ? 'light' : 'dark');
    } catch {
      // Navigazione privata o archiviazione bloccata: il tema vale per questa sessione.
    }
  });

  el('mese-precedente').addEventListener('click', () => {
    stato.mese.setUTCMonth(stato.mese.getUTCMonth() - 1);
    stato.pagina = 1;
    caricaTutto();
  });

  el('mese-successivo').addEventListener('click', () => {
    stato.mese.setUTCMonth(stato.mese.getUTCMonth() + 1);
    stato.pagina = 1;
    caricaTutto();
  });

  el('aggiorna').addEventListener('click', async (evento) => {
    const bottone = evento.currentTarget;
    bottone.disabled = true;
    bottone.textContent = 'Leggo…';

    try {
      const esito = await chiedi('/api/ingest', { method: 'POST' });
      avvisa(`Lette ${esito.lette} email, ${esito.salvate} nuovi movimenti.`);
      await caricaTutto();
    } catch {
      avvisa('Non sono riuscito a leggere la casella.');
    } finally {
      bottone.disabled = false;
      bottone.textContent = 'Aggiorna';
    }
  });

  el('vai-revisione').addEventListener('click', () => {
    el('filtro-revisione').checked = true;
    stato.pagina = 1;
    caricaMovimenti();
  });

  const ricarica = () => { stato.pagina = 1; caricaMovimenti(); };

  el('filtro-testo').addEventListener('input', ritarda(ricarica, 280));
  for (const id of ['filtro-categoria', 'filtro-verso', 'filtro-revisione']) {
    el(id).addEventListener('change', ricarica);
  }
  for (const id of ['filtro-min', 'filtro-max']) {
    el(id).addEventListener('input', ritarda(ricarica, 380));
  }

  el('pagina-precedente').addEventListener('click', () => {
    stato.pagina = Math.max(1, stato.pagina - 1);
    caricaMovimenti();
  });

  el('pagina-successiva').addEventListener('click', () => {
    stato.pagina += 1;
    caricaMovimenti();
  });

  el('dettaglio-salva').addEventListener('click', salvaCorrezione);
}

function applicaTemaSalvato() {
  try {
    const salvato = localStorage.getItem('tema');
    if (salvato === 'dark' || salvato === 'light') {
      document.documentElement.dataset.theme = salvato;
    }
  } catch {
    // Senza archiviazione si resta sul tema di sistema.
  }
}

async function avvia() {
  applicaTemaSalvato();
  collegaEventi();

  const sessione = await (await fetch('/api/sessione', { credentials: 'same-origin' })).json();

  if (sessione.entrato) {
    await mostraApplicazione();
  } else {
    mostraAccesso();
  }
}

avvia().catch(() => avvisa('Qualcosa non ha funzionato all’avvio.'));
