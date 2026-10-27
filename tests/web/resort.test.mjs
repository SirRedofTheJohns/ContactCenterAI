// Behavior checks for wwwroot/resort.js using only Node's built-in runner: `node --test tests/web`.
// The page script runs unmodified in a vm context over a minimal DOM parsed from the real resort.html,
// with a recording fake fetch. This is isolated UI evidence, not a browser, Keycloak or end-to-end test.
// RESORT_JS=<path> runs the same checks against another copy of the script (e.g. the pre-fix version).
process.env.TZ = 'UTC'; // Makes the browser-local vs. property-timezone difference observable.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const root = fileURLToPath(new URL('../../', import.meta.url));
const html = readFileSync(root + 'src/ContactCenterAI.Api/wwwroot/resort.html', 'utf8');
const script = readFileSync(process.env.RESORT_JS ?? root + 'src/ContactCenterAI.Api/wwwroot/resort.js', 'utf8');
const TZ = 'America/Santo_Domingo';

class FakeText { constructor(text) { this.textContent = text; } findAll() { return []; } }
class FakeElement {
  constructor(doc, tag, attrs = {}) {
    Object.assign(this, { ownerDocument: doc, tagName: tag.toUpperCase(), attrs, children: [], text: '', id: attrs.id ?? '',
      className: attrs.class ?? '', hidden: 'hidden' in attrs, value: attrs.value ?? '', disabled: false, title: '', min: '', max: '',
      dataset: Object.fromEntries(Object.entries(attrs).filter(([k]) => k.startsWith('data-')).map(([k, v]) => [k.slice(5), v])), scrolls: 0 });
  }
  setAttribute(name, value) { this.attrs[name] = String(value); }
  getAttribute(name) { return this.attrs[name] ?? null; }
  get textContent() { return this.text + this.children.map(c => c.textContent).join(''); }
  set textContent(v) { this.text = String(v); this.children = []; }
  append(...nodes) { this.children.push(...nodes.map(n => typeof n === 'string' ? new FakeText(n) : n)); }
  replaceChildren(...nodes) { this.text = ''; this.children = []; this.append(...nodes); }
  scrollIntoView() { this.scrolls++; this.ownerDocument.scrolledTo = this; }
  focus() { this.ownerDocument.activeElement = this; }
  findAll(match) { return this.children.flatMap(c => [...(match(c) ? [c] : []), ...c.findAll(match)]); }
  querySelector(tag) { return this.findAll(e => e.tagName === tag.toUpperCase())[0] ?? null; }
}

function parseDocument() {
  const doc = { activeElement: null, scrolledTo: null };
  const top = new FakeElement(doc, '#document');
  const stack = [top];
  const voids = new Set(['input', 'meta', 'link', 'br', 'img']);
  for (const [, close, tag, rawAttrs, text] of html.matchAll(/<(\/?)([a-zA-Z0-9]+)([^>]*)>|([^<]+)/g)) {
    const parent = stack.at(-1);
    if (text !== undefined) { if (text.trim()) parent.append(text); continue; }
    if (close) { stack.pop(); continue; }
    const attrs = {};
    for (const [, name, value] of rawAttrs.matchAll(/([a-zA-Z-]+)(?:="([^"]*)")?/g)) attrs[name] = value ?? '';
    const element = new FakeElement(doc, tag, attrs);
    parent.append(element);
    if (!voids.has(tag.toLowerCase())) stack.push(element);
  }
  for (const select of top.findAll(e => e.tagName === 'SELECT')) select.value = select.querySelector('option')?.attrs.value ?? '';
  const all = top.findAll(e => e instanceof FakeElement);
  Object.assign(doc, {
    documentElement: all.find(e => e.tagName === 'HTML'),
    getElementById: id => all.find(e => e.id === id) ?? null,
    querySelectorAll: selector => { const key = /^\[data-([a-z]+)\]$/.exec(selector)?.[1]; assert.ok(key, 'unsupported selector ' + selector); return all.filter(e => e.dataset[key] !== undefined); },
    createElement: tag => new FakeElement(doc, tag),
  });
  return doc;
}

class MemoryStorage {
  constructor() { this.items = new Map(); }
  getItem(k) { return this.items.has(k) ? this.items.get(k) : null; }
  setItem(k, v) { this.items.set(k, String(v)); }
  removeItem(k) { this.items.delete(k); }
}
const brokenStorage = { getItem() { throw new Error('blocked'); }, setItem() { throw new Error('blocked'); }, removeItem() { throw new Error('blocked'); } };

const catalog = {
  fictional: true, currency: 'USD', today: '2026-10-27',
  categories: ['standard', 'deluxe', 'suite'].map((id, i) => ({ id, nameEs: 'Hab ' + id, nameEn: 'Room ' + id, nightlyCents: 12000 + i * 6000, capacity: id === 'suite' ? 4 : 2, amenitiesEs: ['Wi-Fi'], amenitiesEn: ['Wi-Fi'] })),
};
const observedAt = '2026-10-27T03:30:00Z'; // 23:30 on 26 Oct in Santo Domingo, 03:30 on 27 Oct in UTC.
const stays = [
  { id: 'STAY-AAAA0001', type: 'deluxe', arrival: '2026-11-20', departure: '2026-11-22', guests: 2, totalCents: 36000, status: 'Confirmed' },
  { id: 'STAY-BBBB0002', type: 'suite', arrival: '2026-12-01', departure: '2026-12-03', guests: 3, totalCents: 56000, status: 'Confirmed' },
];
const customer = { authenticated: true, displayName: 'Demo customer-a', isEmployee: false, isOperationsAdmin: false };
const admin = { authenticated: true, displayName: 'Demo operations-admin', isEmployee: true, isOperationsAdmin: true };
const offerFor = (request, id = 'aaaaaaaa-0000-0000-0000-000000000001') => ({ id, request, totalCents: 56000, previousTotalCents: 0, expiresAt: '2026-10-27T03:35:00Z' });
const json = (status, body) => ({ status, body });

async function loadPage({ session = { authenticated: false }, storage = new MemoryStorage(), routes = {} } = {}) {
  const doc = parseDocument();
  const calls = [];
  const state = { reloads: 0 };
  const defaults = {
    'GET /v1/session/csrf': () => json(200, { token: 'csrf-token' }),
    'GET /v2/resort/catalog': () => json(200, catalog),
    'GET /v2/resort/calendar': () => json(200, { observedAt, days: catalog.categories.map(c => ({ unit: c.id, date: '2026-10-27', state: 'Available' })) }),
    'GET /v1/session': () => json(200, session),
    'GET /v2/resort/stays': () => json(200, stays),
    'GET /v2/resort/link-status': () => json(200, { active: false, pending: false }),
    'GET /v2/resort/blocks': () => json(200, []),
    'POST /v2/resort/offers': body => json(200, offerFor(body)),
    'POST /v2/resort/unlink': () => json(204, null),
    'POST /v1/session/logout': () => json(204, null),
    'GET /v2/resort/chat/profile': () => json(200, { mode: 'deterministic', providerId: 'resort-rules-v0.12', examples: { es: ['Mis reservas'], en: ['My bookings'] } }),
  };
  async function fakeFetch(url, options = {}) {
    const method = options.method ?? 'GET';
    const path = url.split('?')[0];
    const body = options.body ? JSON.parse(options.body) : undefined;
    calls.push({ method, path, url, body, headers: options.headers ?? {} });
    const key = method + ' ' + path;
    const handler = routes[key] ?? defaults[key] ?? (path.endsWith('/confirm') ? () => json(200, { status: 'Completed', reasonCode: 'CREATE_COMPLETED' }) : null);
    if (!handler) return response(json(404, { code: 'NOT_MOCKED' }));
    return response(await handler(body));
  }
  function response({ status, body }) {
    const raw = body === null || body === undefined ? '' : JSON.stringify(body);
    return { ok: status >= 200 && status < 300, status, text: async () => raw, json: async () => JSON.parse(raw) };
  }
  const context = vm.createContext({ document: doc, fetch: fakeFetch, crypto: globalThis.crypto, sessionStorage: storage, location: { reload: () => state.reloads++ }, console });
  vm.runInContext(script, context, { filename: 'resort.js' });
  await settle();
  return { doc, $: id => doc.getElementById(id), calls, state, storage };
}
async function settle() { for (let i = 0; i < 20; i++) await new Promise(r => setImmediate(r)); }
const deferred = () => { let resolve; const promise = new Promise(r => { resolve = r; }); return { promise, resolve }; };
const findButton = (container, text) => container.findAll(e => e.tagName === 'BUTTON' && e.textContent === text)[0];
const roomCard = (page, id) => page.$('catalog').children.find(card => card.className.split(' ').includes(id));
const stayCard = (page, id) => page.$('stays').children.find(card => card.children[0]?.textContent === id);
const privateCalls = calls => calls.filter(c => c.path.startsWith('/v2/resort/') && !['/v2/resort/catalog', '/v2/resort/calendar', '/v2/resort/chat/profile'].includes(c.path));

// F05 — choosing a room before login

test('visitor choosing a room is sent to sign-in and the pick is kept, without private calls', async () => {
  const page = await loadPage();
  findButton(roomCard(page, 'suite'), 'Elegir habitación').onclick();
  await settle();
  assert.equal(page.doc.scrolledTo, page.$('login-panel'));
  assert.equal(page.doc.activeElement, page.$('login').querySelector('button'));
  assert.match(page.$('notice').textContent, /Inicia sesión para reservar: Hab suite/);
  assert.equal(JSON.parse(page.storage.getItem('ccai-resort-pick')).type, 'suite');
  assert.deepEqual(privateCalls(page.calls), []);
  assert.equal(page.$('customer-panel').hidden, true);
});

test('login form uses the resort return flag whose server-side return is the fixed /resort.html', async () => {
  // ProductApi maps ?resort=1 to the constant return /resort.html (no caller-supplied URL), so state must live in the tab.
  const page = await loadPage();
  assert.equal(page.$('login').attrs.action, '/v1/session/login?resort=1');
  assert.equal(page.$('login').attrs.method, 'post');
});

test('after returning from OIDC as a customer the picked room is restored once', async () => {
  const storage = new MemoryStorage();
  const visitor = await loadPage({ storage });
  findButton(roomCard(visitor, 'suite'), 'Elegir habitación').onclick();
  const returned = await loadPage({ session: customer, storage }); // Second page load = browser back on /resort.html after login.
  assert.equal(returned.$('type').value, 'suite');
  assert.equal(returned.$('chat-input').value, 'Reservar Hab suite ');
  assert.equal(returned.doc.activeElement, returned.$('chat-input'));
  assert.match(returned.$('notice').textContent, /Habitación elegida antes de iniciar sesión: suite/);
  assert.equal(storage.getItem('ccai-resort-pick'), null);
  const again = await loadPage({ session: customer, storage });
  assert.equal(again.$('type').value, 'standard');
});

test('expired or unknown picks are discarded instead of applied', async () => {
  for (const pick of [{ type: 'suite', at: Date.now() - 16 * 60000 }, { type: 'penthouse', at: Date.now() }]) {
    const storage = new MemoryStorage();
    storage.setItem('ccai-resort-pick', JSON.stringify(pick));
    const page = await loadPage({ session: customer, storage });
    assert.equal(page.$('type').value, 'standard');
    assert.equal(storage.getItem('ccai-resort-pick'), null);
  }
});

test('an employee session clears a pending pick without applying it', async () => {
  const storage = new MemoryStorage();
  storage.setItem('ccai-resort-pick', JSON.stringify({ type: 'suite', at: Date.now() }));
  const page = await loadPage({ session: admin, storage });
  assert.equal(storage.getItem('ccai-resort-pick'), null);
  assert.equal(page.$('customer-panel').hidden, true);
  findButton(roomCard(page, 'deluxe'), 'Elegir habitación').onclick();
  assert.match(page.$('notice').textContent, /no gestiona reservas de cliente/);
});

test('blocked sessionStorage does not break the page or the sign-in guidance', async () => {
  const page = await loadPage({ storage: brokenStorage });
  assert.equal(page.$('catalog').children.length, 3);
  findButton(roomCard(page, 'suite'), 'Elegir habitación').onclick();
  assert.equal(page.doc.activeElement, page.$('login').querySelector('button'));
  const customerPage = await loadPage({ session: customer, storage: brokenStorage });
  assert.equal(customerPage.$('customer-panel').hidden, false);
});

test('a signed-in customer choosing a room fills the form and retires a visible quote', async () => {
  const page = await loadPage({ session: customer });
  page.$('booking').onsubmit({ preventDefault() {} });
  await settle();
  assert.equal(page.$('offer').hidden, false);
  findButton(roomCard(page, 'deluxe'), 'Elegir habitación').onclick();
  assert.equal(page.$('type').value, 'deluxe');
  assert.equal(page.$('offer').hidden, true);
});

// F06 — stale quotes

test('changing another booking retires the previous quote and Confirm sends nothing', async () => {
  const page = await loadPage({ session: customer });
  findButton(stayCard(page, 'STAY-AAAA0001'), 'Consultar cancelación').onclick();
  await settle();
  assert.equal(page.$('offer').hidden, false);
  assert.match(page.$('offer-detail').textContent, /STAY-AAAA0001/);
  findButton(stayCard(page, 'STAY-BBBB0002'), 'Cambiar fechas').onclick();
  assert.equal(page.$('offer').hidden, true);
  assert.equal(page.$('booking-title').textContent, 'Cambiar STAY-BBBB0002');
  page.$('confirm').onclick();
  await settle();
  assert.equal(page.calls.filter(c => c.path.endsWith('/confirm')).length, 0);
});

test('editing the form after a quote retires it', async () => {
  const page = await loadPage({ session: customer });
  page.$('booking').onsubmit({ preventDefault() {} });
  await settle();
  assert.equal(page.$('offer').hidden, false);
  page.$('arrival').value = '2026-11-05';
  page.$('booking').oninput({ target: page.$('arrival') }); // A field's input event bubbles to the form handler.
  assert.equal(page.$('offer').hidden, true);
  page.$('confirm').onclick();
  await settle();
  assert.equal(page.calls.filter(c => c.path.endsWith('/confirm')).length, 0);
});

test('a late quote response for an edited form is not shown', async () => {
  const slow = deferred();
  const page = await loadPage({ session: customer, routes: { 'POST /v2/resort/offers': () => slow.promise } });
  page.$('booking').onsubmit({ preventDefault() {} });
  await settle();
  page.$('guests').value = '3';
  page.$('booking').oninput({ target: page.$('guests') });
  slow.resolve(json(200, offerFor({ action: 'create', type: 'standard', arrival: '', departure: '', guests: 2 })));
  await settle();
  assert.equal(page.$('offer').hidden, true);
});

test('an older quote finishing after a newer one does not replace it', async () => {
  const first = deferred();
  let n = 0;
  const page = await loadPage({ session: customer, routes: { 'POST /v2/resort/offers': body => (++n === 1 ? first.promise : json(200, offerFor(body, 'bbbbbbbb-0000-0000-0000-000000000002'))) } });
  findButton(stayCard(page, 'STAY-AAAA0001'), 'Consultar cancelación').onclick();
  await settle();
  findButton(stayCard(page, 'STAY-BBBB0002'), 'Consultar cancelación').onclick();
  await settle();
  first.resolve(json(200, offerFor({ action: 'cancel', stayId: 'STAY-AAAA0001', type: 'deluxe', arrival: '', departure: '', guests: 2 })));
  await settle();
  assert.match(page.$('offer-detail').textContent, /STAY-BBBB0002/);
  page.$('confirm').onclick();
  await settle();
  assert.deepEqual(page.calls.filter(c => c.path.endsWith('/confirm')).map(c => c.path), ['/v2/resort/offers/bbbbbbbb-0000-0000-0000-000000000002/confirm']);
});

test('a confirmation with a lost response stays visible as unverified', async () => {
  const page = await loadPage({ session: customer, routes: { 'POST /v2/resort/offers/aaaaaaaa-0000-0000-0000-000000000001/confirm': () => json(503, { code: 'OPERATIONAL_UNAVAILABLE' }) } });
  page.$('booking').onsubmit({ preventDefault() {} });
  await settle();
  page.$('confirm').onclick();
  await settle();
  assert.match(page.$('notice').textContent, /Resultado sin verificar/);
});

// F07 — logout independent of unlink

test('logout is attempted when unlink fails, and the pending link cleanup is reported after reload', async () => {
  const storage = new MemoryStorage();
  const page = await loadPage({ session: customer, storage, routes: { 'POST /v2/resort/unlink': () => json(500, { code: 'OPERATIONAL_UNAVAILABLE' }) } });
  page.$('logout').onclick();
  await settle();
  assert.deepEqual(page.calls.filter(c => c.method === 'POST').map(c => c.path), ['/v2/resort/unlink', '/v1/session/logout']);
  assert.equal(page.calls.find(c => c.path === '/v1/session/logout').headers['X-CSRF-TOKEN'], 'csrf-token');
  assert.equal(page.state.reloads, 1);
  const after = await loadPage({ storage });
  assert.match(after.$('notice').textContent, /Sesión cerrada\. No se pudo borrar el registro del vínculo de WhatsApp/);
});

test('a server error during logout is reported as unverified, never as signed out', async () => {
  for (const unlinkStatus of [204, 500]) {
    const page = await loadPage({ session: customer, routes: { 'POST /v2/resort/unlink': () => json(unlinkStatus, unlinkStatus === 204 ? null : { code: 'X' }), 'POST /v1/session/logout': () => json(500, { code: 'OPERATIONAL_UNAVAILABLE' }) } });
    page.$('logout').onclick();
    await settle();
    assert.equal(page.state.reloads, 0);
    assert.match(page.$('notice').textContent, /No se pudo verificar el cierre de sesión/);
    assert.equal(/Tampoco se pudo retirar el vínculo/.test(page.$('notice').textContent), unlinkStatus === 500);
  }
});

test('a network failure during logout neither claims the session ended nor that it is active', async () => {
  const page = await loadPage({ session: customer, routes: { 'POST /v1/session/logout': () => { throw new TypeError('Failed to fetch'); } } });
  page.$('logout').onclick();
  await settle();
  assert.equal(page.state.reloads, 0);
  assert.match(page.$('notice').textContent, /No se pudo verificar el cierre de sesión/);
  assert.doesNotMatch(page.$('notice').textContent, /sigue activa|Sesión cerrada/);
});

test('a logout rejected by the server reports the session as still active', async () => {
  const page = await loadPage({ session: customer, routes: { 'POST /v1/session/logout': () => json(403, { code: 'CSRF_REJECTED' }) } });
  page.$('logout').onclick();
  await settle();
  assert.equal(page.state.reloads, 0);
  assert.match(page.$('notice').textContent, /rechazó el cierre de sesión: sigue activa/);
});

test('logout answered with 401 is reported as an already ended session', async () => {
  const storage = new MemoryStorage();
  const page = await loadPage({ session: customer, storage, routes: { 'POST /v1/session/logout': () => json(401, { code: 'SESSION_REQUIRED' }) } });
  page.$('logout').onclick();
  await settle();
  assert.equal(page.state.reloads, 1);
  const after = await loadPage({ storage });
  assert.match(after.$('notice').textContent, /ya había terminado/);
});

test('an employee logout does not call customer link cleanup', async () => {
  const page = await loadPage({ session: admin });
  page.$('logout').onclick();
  await settle();
  assert.deepEqual(page.calls.filter(c => c.method === 'POST').map(c => c.path), ['/v1/session/logout']);
  assert.equal(page.state.reloads, 1);
});

// Property timezone

test('observed and expiry times are rendered in America/Santo_Domingo, not the browser zone', async () => {
  const page = await loadPage({ session: customer });
  const expected = new Date(observedAt).toLocaleString('es', { timeZone: TZ });
  assert.notEqual(expected, new Date(observedAt).toLocaleString('es')); // Guards the check itself: TZ=UTC differs from the property zone.
  assert.ok(page.$('observed').textContent.includes(expected), page.$('observed').textContent);
  page.$('booking').onsubmit({ preventDefault() {} });
  await settle();
  assert.ok(page.$('offer-detail').textContent.includes(new Date('2026-10-27T03:35:00Z').toLocaleString('es', { timeZone: TZ })), page.$('offer-detail').textContent);
});

// Web chat states (server turns are faked; the server contract is covered by ResortChatChecks)

const turn = (code, text, extra = {}) => ({ code, text, mode: 'deterministic', interpreted: null, missing: [], assumptions: [], ...extra });
const chatCalls = page => page.calls.filter(c => c.path === '/v2/resort/chat');
async function say(page, text) { page.$('chat-input').value = text; page.$('chat-form').onsubmit({ preventDefault() {} }); await settle(); }
const bubbles = page => page.$('chat-log').children.slice(1);

test('a chat turn renders the server text, mode and code, and the technical panel shows no token', async () => {
  const page = await loadPage({ routes: { 'POST /v2/resort/chat': () => json(200, turn('RESORT_CATALOG', 'Suite · 280.00 USD', { interpreted: { action: 'catalog', type: 'suite' }, catalog: [] })) } });
  await say(page, '¿Qué incluye la Suite?');
  const [mine, reply] = bubbles(page);
  assert.equal(mine.textContent, '¿Qué incluye la Suite?');
  assert.match(reply.textContent, /Suite · 280\.00 USD.*determinista · RESORT_CATALOG/s);
  assert.equal(chatCalls(page)[0].body.language, 'es');
  assert.match(chatCalls(page)[0].headers['Idempotency-Key'], /^[0-9a-f-]{36}$/);
  assert.match(page.$('tech-detail').textContent, /RESORT_CATALOG.*"action":"catalog"/s);
  assert.doesNotMatch(page.$('tech-detail').textContent + page.$('tech-calls').textContent, /csrf-token/);
  assert.equal(page.$('chat-input').value, '');
});

test('the mode badge names deterministic mode, or says unknown when the profile is unavailable', async () => {
  assert.equal((await loadPage()).$('mode').textContent, 'Modo determinista');
  const page = await loadPage({ routes: { 'GET /v2/resort/chat/profile': () => json(503, { code: 'X' }) } });
  assert.equal(page.$('mode').textContent, 'Modo desconocido');
  assert.equal(page.$('catalog').children.length, 3);
});

test('a chat proposal fills the summary and only the Confirm button executes it, once', async () => {
  const proposal = offerFor({ action: 'create', type: 'suite', arrival: '2026-11-20', departure: '2026-11-22', guests: 3 });
  const receipt = { commandId: proposal.id, status: 'Completed', reasonCode: 'CREATE_COMPLETED', stay: { id: 'STAY-CCCC0003' } };
  const page = await loadPage({ session: customer, routes: { 'POST /v2/resort/chat': () => json(200, turn('RESORT_OFFER', 'Propuesta', { offer: proposal })), ['POST /v2/resort/offers/' + proposal.id + '/confirm']: () => json(200, receipt) } });
  await say(page, 'Reservar Suite 2026-11-20 a 2026-11-22 para 3 personas');
  assert.equal(page.$('offer').hidden, false);
  assert.match(page.$('offer-detail').textContent, /Hab suite.*2026-11-20 → 2026-11-22 · 2 noches.*3.*560\.00 USD.*Todavía no se ejecutó/s);
  assert.equal(page.calls.filter(c => c.path.endsWith('/confirm')).length, 0);
  page.$('confirm').onclick(); await settle();
  assert.match(page.$('operation').textContent, /Acción confirmada · STAY-CCCC0003/);
  page.$('confirm').onclick(); await settle();
  assert.match(page.$('operation').textContent, /Mismo recibo que antes/);
  assert.deepEqual(page.calls.filter(c => c.path.endsWith('/confirm')).map(c => c.body), [{ decision: 'confirm' }, { decision: 'confirm' }]);
});

test('a clarification offers its example for editing instead of sending it', async () => {
  const page = await loadPage({ session: customer, routes: { 'POST /v2/resort/chat': () => json(200, turn('RESORT_DETAILS_REQUIRED', 'Para preparar la propuesta me falta el número de huéspedes. Ejemplo: Reservar suite 2026-11-20 a 2026-11-22 para 2 personas', { missing: ['guests'] })) } });
  await say(page, 'Reservar Suite 2026-11-20 a 2026-11-22');
  findButton(page.$('chat-log'), 'Usar ejemplo').onclick();
  assert.equal(page.$('chat-input').value, 'Reservar suite 2026-11-20 a 2026-11-22 para 2 personas');
  assert.equal(page.doc.activeElement, page.$('chat-input'));
  assert.equal(chatCalls(page).length, 1);
  assert.equal(page.$('offer').hidden, true);
});

test('a failed chat request offers a retry that reuses the same Idempotency-Key', async () => {
  let n = 0;
  const page = await loadPage({ routes: { 'POST /v2/resort/chat': () => (++n === 1 ? json(503, { code: 'OPERATIONAL_STORE_UNAVAILABLE' }) : json(200, turn('RESORT_POLICY', 'Política'))) } });
  await say(page, 'Política');
  assert.match(bubbles(page)[1].className, /error/);
  findButton(page.$('chat-log'), 'Reintentar').onclick(); await settle();
  const [first, second] = chatCalls(page);
  assert.equal(second.headers['Idempotency-Key'], first.headers['Idempotency-Key']);
  assert.match(page.$('chat-log').textContent, /Política.*RESORT_POLICY/s);
});

test('a sign-in answer keeps the typed message across the login round trip', async () => {
  const storage = new MemoryStorage();
  const page = await loadPage({ storage, routes: { 'POST /v2/resort/chat': () => json(200, turn('SIGN_IN_REQUIRED', 'Inicia sesión')) } });
  let submitted = 0; page.$('login').submit = () => submitted++;
  await say(page, 'Mis reservas');
  findButton(page.$('chat-log'), 'Iniciar sesión').onclick();
  assert.equal(submitted, 1);
  const back = await loadPage({ session: customer, storage });
  assert.equal(back.$('chat-input').value, 'Mis reservas');
  assert.equal(chatCalls(back).length, 0);
});

test('a chat proposal that arrives after another draft started does not replace the summary', async () => {
  const slow = deferred();
  const page = await loadPage({ session: customer, routes: { 'POST /v2/resort/chat': () => slow.promise } });
  await say(page, 'Reservar Suite 2026-11-20 a 2026-11-22 para 3 personas');
  findButton(stayCard(page, 'STAY-AAAA0001'), 'Cambiar fechas').onclick();
  slow.resolve(json(200, turn('RESORT_OFFER', 'Propuesta', { offer: offerFor({ action: 'create', type: 'suite', arrival: '2026-11-20', departure: '2026-11-22', guests: 3 }) })));
  await settle();
  assert.equal(page.$('offer').hidden, true);
  assert.match(page.$('chat-log').textContent, /Propuesta no mostrada/);
});

test('lab fault: a discarded confirmation response is recovered from the same receipt', async () => {
  const proposal = offerFor({ action: 'create', type: 'standard', arrival: '2026-11-26', departure: '2026-11-28', guests: 2 });
  const receipt = { commandId: proposal.id, status: 'Completed', reasonCode: 'CREATE_COMPLETED', stay: { id: 'STAY-DDDD0004' } };
  let lookups = 0;
  const page = await loadPage({ session: customer, routes: {
    'POST /v2/resort/offers': () => json(200, proposal),
    ['POST /v2/resort/offers/' + proposal.id + '/confirm']: () => json(200, receipt),
    ['GET /v2/resort/operations/' + proposal.id]: () => (++lookups === 1 ? json(200, null) : json(200, receipt)) } });
  page.$('booking').onsubmit({ preventDefault() {} }); await settle();
  page.$('lab-drop').checked = true; page.$('lab-drop').onchange();
  page.$('confirm').onclick(); await settle();
  assert.match(page.$('operation').textContent, /Resultado sin verificar.*laboratorio/s);
  assert.equal(page.$('receipt-check').hidden, false);
  assert.equal(page.$('lab-drop').checked, false);
  page.$('receipt-check').onclick(); await settle();
  assert.match(page.$('operation').textContent, /Todavía no hay recibo/);
  page.$('receipt-check').onclick(); await settle();
  assert.match(page.$('operation').textContent, /Acción confirmada · STAY-DDDD0004 · Recuperado del recibo/);
  assert.equal(page.calls.filter(c => c.path.endsWith('/confirm')).length, 1);
});

test('example chips send directly while scenario buttons only prefill dates relative to today', async () => {
  const page = await loadPage({ routes: { 'POST /v2/resort/chat': () => json(200, turn('RESORT_MY_STAYS', 'ok')) } });
  findButton(page.$('chat-examples'), 'Mis reservas').onclick(); await settle();
  assert.equal(chatCalls(page).length, 1);
  const tryButtons = page.$('scenario-list').findAll(e => e.tagName === 'BUTTON');
  assert.equal(page.$('scenario-list').children.length, 6);
  tryButtons[0].onclick();
  assert.equal(page.$('chat-input').value, 'Reservar Deluxe 2026-11-17 a 2026-11-19 para 2 personas');
  assert.equal(chatCalls(page).length, 1);
});
