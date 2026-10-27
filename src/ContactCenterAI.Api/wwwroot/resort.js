'use strict';
// Caribbean Horizon resort demo. The page only presents server decisions: identity, ownership, prices,
// availability and execution are decided by the API. Nothing stored or typed here grants authority.

const $ = id => document.getElementById(id);
const TZ = 'America/Santo_Domingo';
// Tab-scoped UI conveniences only: the server never trusts them. Login returns to the fixed /resort.html,
// so a room picked (or a chat message typed) before login must survive the OIDC round trip here.
const PICK = 'ccai-resort-pick', FLASH = 'ccai-resort-flash', CHAT_DRAFT = 'ccai-resort-chat', PICK_TTL = 15 * 60000;
let lang = 'es', token, session, catalog, offer, changeId = null, draft = 0, profile = null, calendarData = null;
let stays = [], dropNextConfirm = false;
const receipts = {}, calls = [];

const words = {
  skip: ['Ir al chat', 'Skip to chat'], navReserve: ['Reservar', 'Book'], navScenarios: ['Escenarios', 'Scenarios'], navHow: ['Cómo funciona', 'How it works'], logout: ['Salir', 'Sign out'],
  eyebrow: ['UN RESORT FICTICIO. UN FLUJO COMPLETO.', 'A FICTIONAL RESORT. A COMPLETE WORKFLOW.'], title: ['Tu próxima estadía, desde el chat.', 'Your next stay, from the chat.'],
  intro: ['Pregunta por habitaciones y fechas, revisa una propuesta y confírmala. El servidor verifica tu identidad, revalida precio y disponibilidad y guarda un recibo.', 'Ask about rooms and dates, review a proposal and confirm it. The server verifies who you are, rechecks price and availability and stores a receipt.'],
  tryChat: ['Probar el chat ↓', 'Try the chat ↓'], seeScenarios: ['Ver escenarios', 'See scenarios'],
  fiction: ['Demo de portafolio · 3 habitaciones · Tarifas ficticias en USD · Sin pagos ni hotel real', 'Portfolio demo · 3 rooms · Fictional USD rates · No payments or real hotel'],
  chatEyebrow: ['ASISTENTE DE RESERVAS', 'BOOKING ASSISTANT'], chatTitle: ['Conversa con el resort', 'Chat with the resort'],
  chatHello: ['Hola. Puedo mostrar habitaciones y precios, consultar disponibilidad y preparar, cambiar o cancelar reservas ficticias. Nada se ejecuta sin tu confirmación.', 'Hi. I can show rooms and prices, check availability and prepare, change or cancel fictional bookings. Nothing runs without your confirmation.'],
  chatLabel: ['Mensaje', 'Message'], chatPlaceholder: ['Escribe tu mensaje', 'Type your message'], send: ['Enviar', 'Send'],
  chatNote: ['Las respuestas son deterministas salvo que el modo indique IA local. Un "sí" escrito no confirma nada.', 'Answers are deterministic unless the mode says local AI. A typed "yes" confirms nothing.'],
  loginTitle: ['Gestiona tus reservas', 'Manage your bookings'], loginIntro: ['Inicia sesión como customer-a o customer-b. Clave de demo: 123456Aa!', 'Sign in as customer-a or customer-b. Demo password: 123456Aa!'],
  login: ['Iniciar sesión →', 'Sign in →'], loginNote: ['Habitaciones, precios y disponibilidad no requieren sesión.', 'Rooms, prices and availability need no sign-in.'],
  proposal: ['PRIMERO UNA PROPUESTA', 'A QUOTE FIRST'], offerTitle: ['Revisa antes de confirmar', 'Review before confirming'],
  offerNote: ['La propuesta no retiene la habitación. Al confirmar, el servidor revalida disponibilidad, precio y permisos.', 'The quote does not hold the room. On confirmation the server rechecks availability, price and permissions.'],
  confirm: ['Confirmar esta propuesta', 'Confirm this quote'], dismiss: ['Volver', 'Back'], checkReceipt: ['Consultar recibo', 'Check receipt'],
  myBookings: ['Mis reservas ficticias', 'My fictional bookings'], refresh: ['Actualizar', 'Refresh'], manualTitle: ['Formulario manual', 'Manual form'],
  newBooking: ['Nueva reserva ficticia', 'New fictional booking'], category: ['Habitación', 'Room'], arrival: ['Entrada', 'Check-in'], departure: ['Salida', 'Check-out'], guests: ['Huéspedes', 'Guests'],
  preview: ['Consultar propuesta', 'Get quote'], reset: ['Nueva reserva', 'New booking'],
  policy: ['Estadías de 1 a 14 noches, hasta 90 días. Cambios y cancelaciones sin penalidad hasta 72 horas antes de la llegada actual. Sin cobros ni reembolsos reales.', 'Stays of 1–14 nights, up to 90 days ahead. Free changes/cancellation at least 72 hours before current arrival. No actual charges or refunds.'],
  integrationsTitle: ['Integración opcional: WhatsApp', 'Optional integration: WhatsApp'],
  integrationsNote: ['No forma parte de la demo básica. Requiere tu propia configuración de prueba de Meta, el ChannelHost y la clave del puente.', 'Not part of the basic demo. Requires your own Meta test setup, the ChannelHost and the bridge key.'],
  linkOne: ['Genera un código desde esta sesión.', 'Generate a code from this session.'], linkTwo: ['Envía el comando al chat de WhatsApp de la demo.', 'Send the command to the demo WhatsApp chat.'],
  linkThree: ['Actualiza el estado y confirma el vínculo aquí.', 'Refresh the status and confirm the link here.'], generate: ['Generar código', 'Generate code'], status: ['Actualizar estado', 'Refresh status'],
  confirmLink: ['Confirmar vínculo', 'Confirm link'], unlink: ['Desvincular', 'Unlink'],
  linkLimit: ['El código vence en 5 minutos y se usa una vez. El vínculo termina al cerrar o vencer la sesión (máximo 15 minutos). Tu teléfono no define tu identidad.', 'The code expires in 5 minutes and works once. The link ends when you log out or your session expires (maximum 15 minutes). Your phone does not determine identity.'],
  source: ['DISPONIBILIDAD DE LA FUENTE', 'SOURCE AVAILABILITY'], calendarFrom: ['Desde', 'From'], calendarTitle: ['Un calendario que sí cambia', 'A calendar that actually changes'],
  available: ['Disponible', 'Available'], occupied: ['Ocupada', 'Occupied'], maintenance: ['Mantenimiento', 'Maintenance'],
  range: ['Cada celda representa una noche. La salida no ocupa esa noche. Una propuesta no retiene la habitación: la disponibilidad se valida otra vez al confirmar.', 'Each cell is one night. Check-out does not occupy that night. Quotes do not hold inventory: availability is checked again at confirmation.'],
  stay: ['ELIGE TU ESTADÍA', 'CHOOSE YOUR STAY'], rooms: ['Tres formas de desconectar', 'Three ways to unwind'], hours: ['Entrada 15:00 · Salida 11:00 · Santo Domingo', 'Check-in 15:00 · Check-out 11:00 · Santo Domingo'],
  scenariosEyebrow: ['SEIS RECORRIDOS', 'SIX WALKTHROUGHS'], scenariosTitle: ['Escenarios para probar', 'Scenarios to try'],
  scenariosIntro: ['Cada recorrido usa la aplicación real con datos ficticios. Los botones rellenan el chat; tú decides cuándo enviar.', 'Each walkthrough uses the real application with fictional data. Buttons fill the chat; you decide when to send.'],
  labDrop: ['Laboratorio: descartar en este navegador la respuesta de la próxima confirmación', 'Lab: discard the response of the next confirmation in this browser'],
  labNote: ['Fallo inyectado solo en la página: el servidor sí procesa la confirmación, pero la página ignora su respuesta para que puedas recuperar el resultado con el mismo recibo.', 'Fault injected only in the page: the server does process the confirmation, but the page ignores its response so you can recover the outcome from the same receipt.'],
  howEyebrow: ['CÓMO FUNCIONA', 'HOW IT WORKS'], howTitle: ['El texto propone; el servidor decide', 'Text proposes; the server decides'],
  howOne: ['1 · Interpretar', '1 · Interpret'], howOneText: ['Un intérprete convierte el mensaje en intención y campos. En modo determinista son reglas; en modo IA local, un modelo con esquema cerrado. Ninguno concede permisos.', 'An interpreter turns the message into an intent and fields. In deterministic mode these are rules; in local AI mode, a model with a closed schema. Neither grants permissions.'],
  howTwo: ['2 · Validar y proponer', '2 · Validate and propose'], howTwoText: ['El servidor usa tu sesión verificada, filtra por propietario, pide los datos que falten y calcula una propuesta con vencimiento de 5 minutos.', 'The server uses your verified session, filters by owner, asks for missing details and prices a proposal that expires in 5 minutes.'],
  howThree: ['3 · Confirmar', '3 · Confirm'], howThreeText: ['Solo el botón Confirmar ejecuta. La transacción revalida todo, ocupa cada noche una sola vez y guarda un recibo; repetir devuelve el mismo recibo.', 'Only the Confirm button executes. The transaction rechecks everything, books each night once and stores a receipt; repeating returns the same receipt.'],
  modesCaption: ['Qué es real en esta demo', 'What is real in this demo'], modesPart: ['Parte', 'Part'], modesState: ['Estado', 'Status'],
  modesBooking: ['Reservas, permisos, transacciones, recibos', 'Bookings, permissions, transactions, receipts'], modesReal: ['Implementado (C#, SQLite)', 'Implemented (C#, SQLite)'],
  modesLogin: ['Inicio de sesión', 'Sign-in'], modesLoginState: ['Real: Keycloak OIDC local con usuarios ficticios', 'Real: local Keycloak OIDC with fictional users'],
  modesChat: ['Interpretación del chat', 'Chat interpretation'], modesChatState: ['Determinista por defecto; IA local opcional (modelo fijado, no incluido)', 'Deterministic by default; optional local AI (pinned model, not included)'],
  modesData: ['Hotel, precios, huéspedes', 'Hotel, prices, guests'], modesDataState: ['Ficticios; sin pagos', 'Fictional; no payments'],
  modesChannels: ['WhatsApp, Genesys, agentes', 'WhatsApp, Genesys, agents'], modesChannelsState: ['Opcional o simulado; fuera de la demo básica', 'Optional or simulated; outside the basic demo'],
  techTitle: ['Panel técnico', 'Technical panel'], techNote: ['Datos que el servidor devolvió en el último paso. No incluye secretos, tokens ni razonamiento interno de un modelo.', 'Data the server returned in the last step. No secrets, tokens or internal model reasoning.'],
  techCalls: ['Últimas llamadas', 'Recent calls'], admin: ['Gestiona el inventario ficticio', 'Manage fictional inventory'],
  adminIntro: ['Los bloqueos no pueden desplazar reservas. Cambiar una tarifa invalida las propuestas pendientes con ese precio.', 'Blocks cannot displace bookings. Changing a rate invalidates pending quotes at that price.'],
  block: ['Bloquear por mantenimiento', 'Add maintenance block'], rateLabel: ['USD por noche', 'USD per night'], rate: ['Cambiar tarifa', 'Update rate'],
  footer: ['C# / .NET · SQLite · Keycloak OIDC · Confirmación explícita · Datos ficticios · Genesys simulado', 'C# / .NET · SQLite · Keycloak OIDC · Explicit confirmation · Fictional data · Simulated Genesys'],
};
const messages = {
  DATES_OCCUPIED: ['Esas noches están ocupadas o bloqueadas. La reserva anterior se conserva.', 'Those nights are occupied or blocked. Your previous booking is unchanged.'],
  OUTSIDE_FREE_WINDOW: ['Se requieren al menos 72 horas antes de la llegada actual.', 'At least 72 hours before current arrival are required.'],
  OFFER_EXPIRED: ['La propuesta venció. Solicita otra.', 'Quote expired. Request another.'],
  SESSION_REQUIRED: ['Tu sesión terminó. Vuelve a entrar.', 'Session expired. Sign in again.'],
  VERIFIED_CUSTOMER_REQUIRED: ['Se requiere una cuenta de cliente.', 'Customer account required.'],
  RESOURCE_NOT_FOUND: ['Recurso no disponible para esta cuenta.', 'Resource unavailable for this account.'],
  PRICE_CHANGED: ['Cambió la tarifa. Solicita otra propuesta.', 'Rate changed. Request a new quote.'],
  VERSION_CONFLICT: ['El estado cambió. Actualiza y solicita otra propuesta.', 'State changed. Refresh and request a new quote.'],
};

// ---------- small helpers ----------
const t = (es, en) => lang === 'en' ? en : es;
const money = c => (c / 100).toFixed(2) + ' USD';
const notice = text => { $('notice').textContent = text; };
const when = v => new Date(v).toLocaleString(lang, { timeZone: TZ });
const iso = days => new Date(Date.parse(catalog.today + 'T00:00:00Z') + days * 86400000).toISOString().slice(0, 10);
const roomName = id => { const c = catalog?.categories.find(x => x.id === id); return c ? (lang === 'en' ? c.nameEn : c.nameEs) : id; };
function node(tag, text, cls) { const n = document.createElement(tag); n.textContent = text; if (cls) n.className = cls; return n; }
function button(text, onclick, cls) { const b = node('button', text, cls); b.type = 'button'; b.onclick = onclick; return b; }
function stash(k, v) { try { sessionStorage.setItem(k, JSON.stringify(v)); } catch { } }
function unstash(k) { try { const v = sessionStorage.getItem(k); sessionStorage.removeItem(k); return v ? JSON.parse(v) : null; } catch { return null; } }
async function busy(fn) { try { await fn(); } catch (e) { notice(e.message); } }

async function req(path, method = 'GET', data, headers = {}) {
  const r = await fetch(path, {
    method, body: data ? JSON.stringify(data) : undefined,
    headers: { ...(data ? { 'Content-Type': 'application/json' } : {}), ...(method !== 'GET' ? { 'X-CSRF-TOKEN': token } : {}), ...headers },
  });
  // The technical panel records method, path and status only: never headers, tokens or bodies.
  calls.unshift(method + ' ' + path.split('?')[0] + ' → ' + r.status); calls.length = Math.min(calls.length, 8); renderCalls();
  const raw = await r.text(); const b = raw ? JSON.parse(raw) : null;
  if (!r.ok) {
    if (r.status === 401 && session?.authenticated) sessionExpired();
    const known = messages[b?.code];
    throw Object.assign(new Error(known ? t(...known) : t('No se completó este paso: ', 'Step not completed: ') + (b?.code || r.status)), { status: r.status, code: b?.code });
  }
  return b;
}
function sessionExpired() {
  session = { authenticated: false };
  $('account').textContent = t('Sesión vencida', 'Session expired');
  $('login-panel').hidden = false; $('customer-panel').hidden = true; $('logout').hidden = true;
}

function translate() {
  document.documentElement.lang = lang;
  document.querySelectorAll('[data-i]').forEach(n => {
    const w = words[n.dataset.i]; if (!w) return;
    if (n.tagName === 'INPUT') n.placeholder = w[lang === 'en' ? 1 : 0]; else n.textContent = w[lang === 'en' ? 1 : 0];
  });
  renderMode();
  if (catalog) { renderCatalog(); renderExamples(); renderScenarios(); }
}

// ---------- catalog, calendar, mode ----------
function renderMode() {
  const ai = profile?.mode === 'local-llm';
  const label = !profile ? t('Modo desconocido', 'Unknown mode') : ai ? t('IA local', 'Local AI') : t('Modo determinista', 'Deterministic mode');
  $('mode').textContent = label; $('mode').className = 'mode' + (ai ? ' ai' : ''); $('mode').title = profile?.providerId || '';
  $('chat-mode').textContent = profile ? label + ' · ' + profile.providerId : '';
}
function renderCatalog() {
  $('catalog').replaceChildren();
  for (const c of catalog.categories) {
    const card = node('article', '', 'card ' + c.id);
    card.append(node('div', c.id.toUpperCase(), 'room-top'), node('h3', lang === 'en' ? c.nameEn : c.nameEs), node('p', money(c.nightlyCents) + t(' / noche', ' / night'), 'price'), node('p', t('Hasta ', 'Up to ') + c.capacity + t(' huéspedes', ' guests'), 'small'));
    const ul = node('ul', ''); for (const a of lang === 'en' ? c.amenitiesEn : c.amenitiesEs) ul.append(node('li', a)); card.append(ul);
    card.append(button(t('Elegir habitación', 'Choose room'), () => chooseRoom(c)));
    $('catalog').append(card);
  }
}
async function loadCalendar() {
  const narrow = typeof matchMedia === 'function' && matchMedia('(max-width: 900px)').matches;
  const remaining = Math.round((Date.parse(catalog.today + 'T00:00:00Z') + 90 * 86400000 - Date.parse($('calendar-from').value + 'T00:00:00Z')) / 86400000);
  const count = Math.max(1, Math.min(narrow ? 7 : 14, remaining));
  let data;
  try { data = await req('/v2/resort/calendar?from=' + encodeURIComponent($('calendar-from').value) + '&days=' + count); }
  catch (e) { $('calendar-grid').replaceChildren(node('p', t('No se pudo cargar el calendario: ', 'Could not load the calendar: ') + e.message, 'empty'), button(t('Reintentar', 'Retry'), () => busy(loadCalendar))); return; }
  calendarData = data;
  const dates = [...new Set(data.days.map(d => d.date))];
  const table = node('table', ''), caption = node('caption', t('Disponibilidad por habitación y noche', 'Availability per room and night'), 'sr'), head = node('tr', '');
  const corner = node('th', t('Habitación', 'Room')); corner.setAttribute('scope', 'col'); head.append(corner);
  dates.forEach(d => { const th = node('th', d.slice(5)); th.setAttribute('scope', 'col'); head.append(th); });
  const thead = node('thead', ''); thead.append(head); table.append(caption, thead);
  const body = node('tbody', '');
  for (const c of catalog.categories) {
    const row = node('tr', ''); const th = node('th', lang === 'en' ? c.nameEn : c.nameEs); th.setAttribute('scope', 'row'); row.append(th);
    for (const d of dates) {
      const cell = data.days.find(x => x.unit === c.id && x.date === d);
      const label = (words[cell.state.toLowerCase()] || ['', ''])[lang === 'en' ? 1 : 0] + ' ' + d;
      const td = node('td', cell.state === 'Available' ? '·' : cell.state === 'Occupied' ? '●' : '—', cell.state.toLowerCase());
      td.title = label; td.setAttribute('aria-label', label); row.append(td);
    }
    body.append(row);
  }
  table.append(body); $('calendar-grid').replaceChildren(table);
  $('observed').textContent = t('Consultado: ', 'Observed: ') + when(data.observedAt) + ' · ' + TZ;
  renderScenarios();
}

// ---------- room pick across login (F05) ----------
// Visitors cannot book: keep the pick for after login instead of scrolling to a hidden form.
function chooseRoom(c) {
  const name = lang === 'en' ? c.nameEn : c.nameEs;
  if (!session?.authenticated) {
    stash(PICK, { type: c.id, at: Date.now() });
    notice(t('Inicia sesión para reservar: ', 'Sign in to book: ') + name + t('. Conservaremos tu elección.', '. Your choice will be kept.'));
    $('login-panel').scrollIntoView({ behavior: 'smooth' }); $('login').querySelector('button').focus(); return;
  }
  if (session.isEmployee) { notice(t('Esta cuenta no gestiona reservas de cliente.', 'This account does not manage customer bookings.')); return; }
  invalidate(); $('type').value = c.id; prefillChat(t('Reservar ', 'Book ') + name + ' ');
}
function restorePick() {
  const p = unstash(PICK);
  if (!p || Date.now() - p.at > PICK_TTL || !catalog.categories.some(c => c.id === p.type)) return;
  $('type').value = p.type; prefillChat(t('Reservar ', 'Book ') + roomName(p.type) + ' ');
  notice(t('Habitación elegida antes de iniciar sesión: ', 'Room chosen before signing in: ') + p.type);
}
function prefillChat(text) { $('chat-input').value = text; $('chat-input').scrollIntoView({ behavior: 'smooth' }); $('chat-input').focus(); }

// ---------- proposal, confirmation, receipt (F06) ----------
// Any new draft retires the visible quote; a late quote response from an older draft is discarded, so Confirm always matches the form.
function invalidate() { draft++; offer = null; $('offer').hidden = true; $('operation').textContent = ''; $('receipt-check').hidden = true; }
function reset() { changeId = null; invalidate(); $('booking-title').textContent = t('Nueva reserva ficticia', 'New fictional booking'); }
function fact(dl, label, value) { dl.append(node('dt', label), node('dd', value)); }
function showOffer(o) {
  offer = o; const r = o.request; const dl = $('offer-detail'); dl.replaceChildren();
  const nights = r.arrival && r.departure ? Math.round((Date.parse(r.departure) - Date.parse(r.arrival)) / 86400000) : 0;
  fact(dl, t('Acción', 'Action'), ({ create: t('reservar', 'book'), change: t('cambiar fechas', 'change dates'), cancel: t('cancelar', 'cancel') })[r.action] || r.action);
  if (r.stayId) fact(dl, t('Reserva', 'Booking'), r.stayId);
  fact(dl, t('Habitación', 'Room'), roomName(r.type));
  fact(dl, t('Fechas', 'Dates'), r.arrival + ' → ' + r.departure + ' · ' + nights + t(' noches', ' nights'));
  fact(dl, t('Huéspedes', 'Guests'), String(r.guests));
  fact(dl, t('Total ficticio', 'Fictional total'), money(o.totalCents));
  if (r.action === 'change') fact(dl, t('Anterior / diferencia', 'Previous / difference'), money(o.previousTotalCents) + ' / ' + money(o.totalCents - o.previousTotalCents));
  fact(dl, t('Vence', 'Expires'), when(o.expiresAt) + ' · ' + TZ);
  fact(dl, t('Estado', 'Status'), t('Todavía no se ejecutó.', 'Not executed yet.'));
  $('offer').hidden = false; $('confirm').disabled = false; $('confirm').textContent = t('Confirmar esta propuesta', 'Confirm this quote');
  $('operation').textContent = ''; $('receipt-check').hidden = true;
  $('offer').scrollIntoView({ behavior: 'smooth' }); $('offer-title').focus();
  renderTech({ code: 'OFFER', offer: o });
}
async function quote(r) {
  invalidate(); const mine = draft;
  const o = await req('/v2/resort/offers', 'POST', r, { 'Idempotency-Key': crypto.randomUUID() });
  if (mine === draft) showOffer(o);
}
async function confirmOffer() {
  if (!offer) return;
  const o = offer, drop = dropNextConfirm;
  dropNextConfirm = false; $('lab-drop').checked = false;
  $('confirm').disabled = true; $('receipt-check').hidden = true; $('operation').textContent = t('Confirmando con el servidor…', 'Confirming with the server…');
  try {
    const r = await req('/v2/resort/offers/' + o.id + '/confirm', 'POST', { decision: 'confirm' });
    if (drop) throw new Error(t('Respuesta descartada por el laboratorio (fallo inyectado en este navegador).', 'Response discarded by the lab (fault injected in this browser).'));
    await showReceipt(o, r, false);
  } catch (e) {
    const m = t('Resultado sin verificar. Revisa Mis reservas antes de otra acción. ', 'Result unverified. Check My bookings before another action. ') + e.message;
    $('operation').textContent = m + ' ' + t('Consulta el recibo de esta misma propuesta.', 'Check the receipt of this same proposal.');
    $('receipt-check').hidden = false; notice(m);
  } finally { $('confirm').disabled = false; }
}
async function showReceipt(o, r, recovered) {
  const same = receipts[o.id] && JSON.stringify(receipts[o.id]) === JSON.stringify(r);
  receipts[o.id] = r; $('receipt-check').hidden = true;
  const done = r.status === 'Completed';
  const head = done ? t('Acción confirmada', 'Action confirmed') : t('Acción rechazada', 'Action rejected');
  const reason = done ? '' : ' · ' + (messages[r.reasonCode] ? t(...messages[r.reasonCode]) : r.reasonCode);
  const extra = (recovered ? t(' · Recuperado del recibo de la misma propuesta.', ' · Recovered from the same proposal\'s receipt.') : '') + (same ? t(' · Mismo recibo que antes: no se repitió la operación.', ' · Same receipt as before: the operation did not run again.') : '');
  $('operation').textContent = head + (r.stay ? ' · ' + r.stay.id : '') + reason + extra;
  notice(done ? t('La fuente confirmó la acción ficticia. No se realizó ningún cobro.', 'Source confirmed the fictional action. No money charged.') + extra : head + reason);
  if (done) $('confirm').textContent = t('Repetir confirmación (mismo recibo)', 'Repeat confirmation (same receipt)');
  chatSay($('operation').textContent, done ? 'bot' : 'bot error');
  renderTech({ code: r.reasonCode, offer: o, receipt: r });
  if (session?.authenticated && !session.isEmployee) await loadStays();
  await loadCalendar();
}
async function checkReceipt() {
  const o = offer; if (!o) return;
  const r = await req('/v2/resort/operations/' + o.id);
  if (r) await showReceipt(o, r, true);
  else $('operation').textContent = t('Todavía no hay recibo para esta propuesta: no se ejecutó o sigue en cola. Puedes confirmarla de nuevo; si ya se ejecutó, recibirás el mismo recibo.', 'No receipt yet for this proposal: it did not run or is still queued. You can confirm it again; if it already ran, you will get the same receipt.');
}

// ---------- bookings ----------
async function loadStays() {
  stays = await req('/v2/resort/stays'); $('stays').replaceChildren();
  if (stays.length === 0) $('stays').append(node('p', t('Aún no tienes reservas. Pide una en el chat.', 'No bookings yet. Ask for one in the chat.'), 'empty'));
  for (const s of stays) {
    const card = node('article', '', 'card');
    card.append(node('h3', s.id), node('p', roomName(s.type) + ' · ' + s.arrival + ' → ' + s.departure), node('p', money(s.totalCents) + ' · ' + t(s.status === 'Confirmed' ? 'Confirmada' : 'Cancelada', s.status)), node('p', s.guests + t(' huéspedes', ' guests'), 'small'));
    if (s.status === 'Confirmed') {
      const change = button(t('Cambiar fechas', 'Change dates'), () => {
        invalidate(); changeId = s.id; $('type').value = s.type; $('arrival').value = s.arrival; $('departure').value = s.departure; $('guests').value = s.guests;
        $('booking-title').textContent = t('Cambiar ', 'Change ') + s.id; $('manual').open = true; $('booking').scrollIntoView({ behavior: 'smooth' });
      });
      const cancel = button(t('Consultar cancelación', 'Preview cancellation'), () => busy(() => quote({ action: 'cancel', stayId: s.id })));
      const row = node('div', '', 'row'); row.append(change, cancel); card.append(row);
    }
    $('stays').append(card);
  }
  renderScenarios();
}
async function linkStatus() {
  const s = await req('/v2/resort/link-status');
  $('link-status').textContent = s.active ? t('WhatsApp vinculado a esta sesión.', 'WhatsApp linked to this session.') : s.pending ? t('Código recibido de ', 'Code received from ') + s.route : t('Sin vínculo activo.', 'No active link.');
  $('link-confirm').hidden = !s.pending;
}
async function loadBlocks() {
  $('blocks').replaceChildren();
  for (const b of await req('/v2/resort/blocks')) {
    const n = node('p', b.unit + ' · ' + b.arrival + ' → ' + b.departure);
    n.append(button(t('Quitar bloqueo', 'Remove block'), () => busy(async () => { await req('/v2/resort/blocks/' + b.id + '?version=' + b.version, 'DELETE'); await loadBlocks(); await loadCalendar(); })));
    $('blocks').append(n);
  }
}

// ---------- chat ----------
function chatSay(text, cls) { const li = node('li', '', 'msg ' + cls); li.append(node('p', text)); $('chat-log').append(li); li.scrollIntoView?.({ block: 'nearest' }); return li; }
function chips(items) { const row = node('div', '', 'row'); for (const [label, fn] of items) row.append(button(label, fn)); return row; }
function renderExamples() {
  const list = profile?.examples?.[lang] || [];
  $('chat-examples').replaceChildren(...list.map(text => button(text, () => busy(() => sendChat(text)))));
}
async function sendChat(text, key = crypto.randomUUID()) {
  text = text.trim(); if (!text) return;
  $('chat-input').value = ''; chatSay(text, 'user');
  const reply = chatSay(t('Consultando…', 'Checking…'), 'bot pending');
  $('chat-send').disabled = true; const sentDraft = draft;
  try {
    const turn = await req('/v2/resort/chat', 'POST', { text, language: lang }, { 'Idempotency-Key': key });
    renderTurn(reply, turn, sentDraft, text);
  } catch (e) {
    reply.className = 'msg bot error';
    // Retrying reuses the same Idempotency-Key, so a resend can never create a second proposal.
    reply.replaceChildren(node('p', e.message), chips([[t('Reintentar', 'Retry'), () => busy(() => sendChat(text, key))]]));
  } finally { $('chat-send').disabled = false; }
}
function renderTurn(li, turn, sentDraft, text) {
  const problem = /REQUIRED|INVALID|NOT_FOUND|OCCUPIED|OUT_OF_SCOPE|REJECTED|UNAVAILABLE|MISMATCH|TIMEOUT|CONFLICT|SENSITIVE/.test(turn.code);
  li.className = 'msg bot' + (problem && turn.code !== 'RESORT_DETAILS_REQUIRED' ? ' error' : '');
  li.replaceChildren(node('p', turn.text));
  for (const a of turn.assumptions || []) li.append(node('span', t('Supuesto: ', 'Assumption: ') + a, 'meta'));
  const actions = [];
  if (turn.offer) {
    // A chat proposal replaces the visible one unless the visitor started another draft meanwhile.
    if (sentDraft === draft) { invalidate(); showOffer(turn.offer); actions.push([t('Revisar propuesta', 'Review proposal'), () => $('offer-title').focus()]); }
    else li.append(node('span', t('Propuesta no mostrada: iniciaste otro borrador después de enviar este mensaje.', 'Proposal not shown: you started another draft after sending this message.'), 'meta'));
  }
  const example = turn.code === 'RESORT_DETAILS_REQUIRED' ? turn.text.split(/Ejemplo: |Example: /)[1] : null;
  if (example) actions.push([t('Usar ejemplo', 'Use example'), () => prefillChat(example)]);
  for (const o of turn.availability || []) for (const range of o.ranges.slice(0, 3)) {
    const [a, d] = range.split(' → ');
    actions.push([roomName(o.type) + ' ' + range, () => prefillChat(t('Reservar ', 'Book ') + roomName(o.type) + ' ' + a + t(' a ', ' to ') + d + t(' para ', ' for '))]);
  }
  for (const s of (turn.stays || []).filter(x => x.status === 'Confirmed').slice(0, 4)) {
    actions.push([t('Cambiar ', 'Change ') + s.id, () => prefillChat(t('Cambiar ', 'Change ') + s.id + ' ')]);
    actions.push([t('Cancelar ', 'Cancel ') + s.id, () => prefillChat(t('Cancelar ', 'Cancel ') + s.id)]);
  }
  if (turn.code === 'SIGN_IN_REQUIRED') actions.push([t('Iniciar sesión', 'Sign in'), () => { stash(CHAT_DRAFT, text); const f = $('login'); f.requestSubmit ? f.requestSubmit() : f.submit(); }]);
  for (const s of (turn.suggestions || []).slice(0, 3)) actions.push([s, () => busy(() => sendChat(s))]);
  if (actions.length) li.append(chips(actions));
  li.append(node('span', (turn.mode === 'local-llm' ? t('IA local', 'local AI') : t('determinista', 'deterministic')) + ' · ' + turn.code, 'meta'));
  if (turn.receipt) { receipts[turn.receipt.commandId] = turn.receipt; busy(loadCalendar); if (session?.authenticated && !session.isEmployee) busy(loadStays); }
  renderTech(turn);
}

// ---------- technical panel ----------
function renderTech(turn) {
  const dl = $('tech-detail'); dl.replaceChildren();
  fact(dl, t('Modo', 'Mode'), profile ? profile.mode + ' · ' + profile.providerId : '—');
  fact(dl, t('Código', 'Code'), turn.code || '—');
  if (turn.interpreted) fact(dl, t('Interpretación (no autoriza)', 'Interpretation (no authority)'), JSON.stringify(turn.interpreted));
  if (turn.missing?.length) fact(dl, t('Datos que faltan', 'Missing details'), turn.missing.join(', '));
  if (turn.assumptions?.length) fact(dl, t('Supuestos', 'Assumptions'), turn.assumptions.join(' | '));
  if (turn.catalog || turn.availability) fact(dl, t('Fuente', 'Source'), t('Catálogo e inventario del resort (SQLite, en el servidor)', 'Resort catalog and inventory (SQLite, server side)'));
  if (turn.offer) fact(dl, t('Propuesta', 'Proposal'), turn.offer.id + ' · ' + turn.offer.status + ' · ' + t('vence ', 'expires ') + when(turn.offer.expiresAt));
  if (turn.receipt) fact(dl, t('Recibo', 'Receipt'), turn.receipt.commandId + ' · ' + turn.receipt.status + ' · ' + turn.receipt.reasonCode + (turn.receipt.stay ? ' · ' + turn.receipt.stay.id : ''));
  fact(dl, t('Validaciones del servidor', 'Server validations'), t('sesión verificada (cookie OIDC) · propietario filtrado · fechas, capacidad y horizonte · precio calculado en el servidor · confirmación explícita ligada a la propuesta · revalidación e idempotencia al ejecutar', 'verified session (OIDC cookie) · owner filter · dates, capacity and horizon · server-side price · explicit confirmation bound to the proposal · recheck and idempotency on execution'));
}
function renderCalls() { $('tech-calls').replaceChildren(...calls.map(c => node('li', c))); }

// ---------- scenarios ----------
function occupiedRun() {
  if (!calendarData) return null;
  for (const unit of ['suite', 'deluxe', 'standard']) {
    const busyNights = calendarData.days.filter(d => d.unit === unit && d.state !== 'Available').map(d => d.date);
    if (busyNights.length) { const a = busyNights[0]; const d = new Date(Date.parse(a) + 2 * 86400000).toISOString().slice(0, 10); return { unit, a, d }; }
  }
  return null;
}
function renderScenarios() {
  if (!catalog) return;
  const mine = stays.find(s => s.status === 'Confirmed')?.id || 'STAY-A0000001';
  const run = occupiedRun();
  const es = lang !== 'en';
  const s1 = es ? `Reservar Deluxe ${iso(21)} a ${iso(23)} para 2 personas` : `Book Deluxe ${iso(21)} to ${iso(23)} for 2 guests`;
  const s2 = run ? (es ? `Cambiar ${mine} ${run.unit} ${run.a} a ${run.d}` : `Change ${mine} ${run.unit} ${run.a} to ${run.d}`) : null;
  const s5 = es ? `Reservar Estándar ${iso(30)} a ${iso(32)} para 2 personas` : `Book Standard ${iso(30)} to ${iso(32)} for 2 guests`;
  const items = [
    [t('1 · Reserva completa', '1 · Complete booking'), null, [t('Inicia sesión como customer-a.', 'Sign in as customer-a.'), t('Envía la petición y revisa la propuesta: habitación, fechas, huéspedes, total y vencimiento.', 'Send the request and review the proposal: room, dates, guests, total and expiry.'), t('Pulsa Confirmar. El calendario marca las noches como Ocupada y aparece en Mis reservas.', 'Press Confirm. The calendar marks the nights Occupied and it appears in My bookings.')], [s1]],
    [t('2 · Cambio rechazado', '2 · Rejected change'), null, [t('Con sesión de cliente, pide mover tu reserva a noches ocupadas o en mantenimiento del calendario.', 'Signed in as a customer, ask to move your booking onto occupied or maintenance nights from the calendar.'), t('El servidor responde DATES_OCCUPIED y tu reserva original no cambia.', 'The server answers DATES_OCCUPIED and your original booking is unchanged.')], s2 ? [s2] : []],
    [t('3 · Confirmación repetida', '3 · Repeated confirmation'), null, [t('Tras confirmar una propuesta, pulsa "Repetir confirmación".', 'After confirming a proposal, press "Repeat confirmation".'), t('Recibes el mismo recibo y Mis reservas no muestra un duplicado: idempotencia.', 'You get the same receipt and My bookings shows no duplicate: idempotency.')], []],
    [t('4 · Reserva de otro cliente', '4 · Another customer\'s booking'), null, [t('Como customer-a, intenta cancelar la reserva sembrada de customer-b.', 'As customer-a, try to cancel customer-b\'s seeded booking.'), t('Respuesta RESOURCE_NOT_FOUND, sin revelar fechas ni datos: conocer un código no da acceso.', 'Answer RESOURCE_NOT_FOUND, revealing no dates or data: knowing a code grants no access.')], [es ? 'Cancelar STAY-B0000001' : 'Cancel STAY-B0000001']],
    [t('5 · Resultado recuperado', '5 · Recovered outcome'), t('Fallo inyectado en el navegador', 'Fault injected in the browser'), [t('Activa el laboratorio de abajo y prepara una propuesta.', 'Turn on the lab below and prepare a proposal.'), t('Confirma: la página descarta la respuesta y muestra "sin verificar".', 'Confirm: the page discards the response and shows "unverified".'), t('Pulsa "Consultar recibo": el servidor devuelve el recibo de esa misma propuesta, sin repetir la operación.', 'Press "Check receipt": the server returns the receipt of that same proposal without running it again.')], [s5]],
    [t('6 · Ambigua o fuera de alcance', '6 · Ambiguous or out of scope'), null, [t('Un número de huéspedes dudoso se pregunta en vez de suponerse.', 'A doubtful guest count is asked for instead of assumed.'), t('Una pregunta ajena al resort se declina sin inventar información.', 'A question outside the resort is declined without inventing information.')], [es ? `Reservar Suite ${iso(40)} a ${iso(42)} para 2 o 3 personas` : `Book Suite ${iso(40)} to ${iso(42)} for 2 or 3 guests`, es ? '¿Qué tiempo hace en París?' : 'What is the weather in Paris?']],
  ];
  $('scenario-list').replaceChildren(...items.map(([title, tag, steps, tries]) => {
    const card = node('article', '', 'card'); card.append(node('h3', title)); if (tag) card.append(node('span', tag, 'tag'));
    const ol = node('ol', ''); for (const s of steps) ol.append(node('li', s)); card.append(ol);
    if (tries.length) card.append(chips(tries.map(text => [t('Probar: ', 'Try: ') + text, () => prefillChat(text)])));
    return card;
  }));
}

// ---------- logout (F07) ----------
// Session revocation must not depend on link cleanup: a link only authorizes through its live session, so revoking the session also ends the link's authority.
async function logout() {
  let linkPending = false;
  if (!session.isEmployee) try { await req('/v2/resort/unlink', 'POST'); } catch { linkPending = true; }
  let ended = t('Sesión cerrada.', 'Signed out.');
  try { await req('/v1/session/logout', 'POST'); }
  catch (e) {
    if (e.status !== 401) {
      const rejected = e.status >= 400 && e.status < 500;
      notice((rejected ? t('El servidor rechazó el cierre de sesión: sigue activa. Vuelve a intentarlo.', 'The server rejected sign-out: your session is still active. Try again.') : t('No se pudo verificar el cierre de sesión. Vuelve a intentarlo o recarga la página para comprobar el estado.', 'Sign-out could not be verified. Try again or reload the page to check your status.')) + (linkPending ? t(' Tampoco se pudo retirar el vínculo de WhatsApp.', ' The WhatsApp link could not be removed either.') : ''));
      return;
    }
    ended = t('Tu sesión ya había terminado en el servidor.', 'Your session had already ended on the server.');
  }
  stash(FLASH, ended + (linkPending ? t(' No se pudo borrar el registro del vínculo de WhatsApp; ya no autoriza acciones porque dependía de esa sesión.', ' The WhatsApp link record could not be cleared; it no longer authorizes actions because it depended on that session.') : ''));
  location.reload();
}

// ---------- wiring ----------
$('language').onchange = () => busy(async () => { lang = $('language').value; translate(); await loadCalendar(); if (session?.authenticated && !session.isEmployee) await loadStays(); });
$('refresh').onclick = () => busy(loadCalendar); $('reset').onclick = reset; $('stays-refresh').onclick = () => busy(loadStays);
$('booking').onsubmit = e => { e.preventDefault(); busy(() => quote({ action: changeId ? 'change' : 'create', stayId: changeId, type: $('type').value, arrival: $('arrival').value, departure: $('departure').value, guests: Number($('guests').value) })); };
$('booking').oninput = $('booking').onchange = invalidate;
$('confirm').onclick = () => busy(confirmOffer);
$('receipt-check').onclick = () => busy(checkReceipt);
$('dismiss').onclick = () => { invalidate(); notice(t('Propuesta descartada. No se ejecutó nada; vencerá sola.', 'Proposal dismissed. Nothing was executed; it will expire on its own.')); };
$('chat-form').onsubmit = e => { e.preventDefault(); busy(() => sendChat($('chat-input').value)); };
$('lab-drop').onchange = () => { dropNextConfirm = $('lab-drop').checked; };
$('link-code').onclick = () => busy(async () => { const c = await req('/v2/resort/link-codes', 'POST'); $('code').textContent = (lang === 'en' ? 'Link ' : 'Vincular ') + c.code; await linkStatus(); });
$('link-refresh').onclick = () => busy(linkStatus);
$('link-confirm').onclick = () => busy(async () => { await req('/v2/resort/link-confirm', 'POST'); $('code').textContent = ''; await linkStatus(); notice(t('Vínculo confirmado. Usa Mis reservas en WhatsApp.', 'Link confirmed. Use My bookings in WhatsApp.')); });
$('unlink').onclick = () => busy(async () => { await req('/v2/resort/unlink', 'POST'); $('code').textContent = ''; await linkStatus(); });
$('logout').onclick = () => busy(logout);
$('block-form').onsubmit = e => { e.preventDefault(); busy(async () => { await req('/v2/resort/blocks', 'POST', { unit: $('block-type').value, arrival: $('block-from').value, departure: $('block-to').value }); await loadBlocks(); await loadCalendar(); notice(t('Mantenimiento guardado.', 'Maintenance saved.')); }); };
$('rate-form').onsubmit = e => { e.preventDefault(); busy(async () => { await req('/v2/resort/rates', 'POST', { type: $('rate-type').value, cents: Math.round(Number($('rate').value) * 100) }); catalog = await req('/v2/resort/catalog'); renderCatalog(); notice(t('Tarifa ficticia actualizada.', 'Fictional rate updated.')); }); };

async function main() {
  token = (await req('/v1/session/csrf')).token; $('csrf-form').value = token;
  catalog = await req('/v2/resort/catalog');
  $('calendar-from').value = catalog.today; $('calendar-from').min = catalog.today;
  $('calendar-from').max = new Date(Date.parse(catalog.today + 'T00:00:00Z') + 89 * 86400000).toISOString().slice(0, 10);
  for (const id of ['arrival', 'departure', 'block-from', 'block-to']) $(id).min = catalog.today;
  // The chat profile is informative; if it fails the page still works and says the mode is unknown.
  try { profile = await req('/v2/resort/chat/profile'); } catch { profile = null; }
  renderCatalog(); renderExamples(); await loadCalendar();
  const r = await fetch('/v1/session');
  if (r.ok) {
    session = await r.json();
    if (session.authenticated) {
      $('account').textContent = session.displayName; $('login-panel').hidden = true; $('logout').hidden = false;
      token = (await req('/v1/session/csrf')).token;
      if (session.isOperationsAdmin) { $('admin-panel').hidden = false; await loadBlocks(); }
      else if (!session.isEmployee) { $('customer-panel').hidden = false; await loadStays(); await linkStatus(); restorePick(); const typed = unstash(CHAT_DRAFT); if (typed && !$('chat-input').value) prefillChat(typed); }
      if ($('customer-panel').hidden) { unstash(PICK); unstash(CHAT_DRAFT); }
    }
  }
  translate();
  const f = unstash(FLASH); if (f) notice(f);
}
main().catch(e => notice(e.message));
