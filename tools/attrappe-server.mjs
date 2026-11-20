// SPDX-License-Identifier: AGPL-3.0-or-later
// Attrappe des Twitch Companion JS server für Spieltests der Mod (Spec des Servers „Chat-Brücke“, Entwurf).
// Spricht nie mit Twitch und nie mit dem echten Server. Start: node tools/attrappe-server.mjs [port]
// Steuerung (nur für Tests, nicht Teil des Servers):
//   POST /attrappe/chat     {login, text, userId?, name?, badges?, private?}  → Ereignis chat an alle Verbindungen
//   POST /attrappe/privacy  {userId, login, private}                          → Ereignis privacy
//   POST /attrappe/channel  {category, live, categoryChangedAt?}              → Antwort von /api/twitch/channel
//   POST /attrappe/active   {chatters:[{id, login, name, lastChatAt?, ...}]}  → Antwort von /api/chat/active
//   POST /attrappe/drop                                                       → alle Ereignisströme trennen
//   GET  /attrappe/sent                                                       → gesendete Nachrichten
//   GET  /attrappe/status                                                     → Verbindungen, Anmeldungen
import http from 'node:http';
import { randomUUID } from 'node:crypto';

const port = Number(process.argv[2] || 3999);
if (port === 3000) {
  console.error('Port 3000 gehört dem echten Server. Bitte einen anderen Port wählen.');
  process.exit(1);
}

const streams = new Set();
const registrations = [];
const sent = [];
let lastSendAt = 0;
let channel = { category: 'RimWorld', categoryChangedAt: new Date(Date.now() - 3600_000).toISOString(), live: true, updatedAt: new Date().toISOString() };
let active = [];

function json(res, status, body) {
  const text = JSON.stringify(body);
  res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8', 'Content-Length': Buffer.byteLength(text) });
  res.end(text);
}

function readBody(req) {
  return new Promise((resolve) => {
    let data = '';
    req.on('data', (c) => { data += c; });
    req.on('end', () => {
      try { resolve(data ? JSON.parse(data) : {}); } catch { resolve(null); }
    });
  });
}

function broadcast(event, payload) {
  const frame = `event: ${event}\ndata: ${JSON.stringify(payload)}\n\n`;
  for (const s of streams) s.res.write(frame);
  return streams.size;
}

function matches(reg, text) {
  const t = text.trimStart();
  if (reg.prefixes.some((p) => t.startsWith(p))) return true;
  const first = t.split(' ')[0].toLowerCase();
  return reg.commands.includes(first);
}

const handler = async (req, res) => {
  const url = new URL(req.url, `http://${req.headers.host}`);
  const path = url.pathname;
  if (!path.startsWith('/attrappe/')) console.log(`[attrappe] ${req.method} ${path}`);

  if (req.method === 'GET' && path === '/api/chat/stream') {
    const client = url.searchParams.get('client') || '';
    const prefixes = (url.searchParams.get('prefixes') || '').split(',').filter(Boolean);
    const commands = (url.searchParams.get('commands') || '').split(',').filter(Boolean).map((c) => c.toLowerCase());
    if (!/^[a-z0-9_-]{1,32}$/.test(client) || (prefixes.length === 0 && commands.length === 0)) {
      return json(res, 400, { error: 'client und prefixes oder commands nötig' });
    }
    res.writeHead(200, { 'Content-Type': 'text/event-stream', 'Cache-Control': 'no-cache', Connection: 'keep-alive' });
    res.write('retry: 5000\n\n');
    const entry = { res, client, prefixes, commands, since: new Date().toISOString() };
    streams.add(entry);
    registrations.push({ client, prefixes, commands, at: entry.since });
    res.write(`event: hello\ndata: ${JSON.stringify({ server: 'twitch-companion-js-server', version: 'attrappe', client, commands, prefixes, botLogin: 'derbdu' })}\n\n`);
    const ping = setInterval(() => res.write(': ping\n\n'), 30_000);
    console.log(`[attrappe] verbunden: ${client} prefixes=${prefixes.join(' ')}`);
    req.on('close', () => { clearInterval(ping); streams.delete(entry); console.log(`[attrappe] getrennt: ${client}`); });
    return;
  }

  if (req.method === 'POST' && path === '/api/chat/send') {
    if (!(req.headers['content-type'] || '').includes('application/json')) return json(res, 415, { ok: false, sent: false, error: 'json nötig' });
    const body = await readBody(req);
    if (!body || body.feature !== 'rimworld' && body.feature !== 'coinflip') return json(res, 400, { ok: false, sent: false, error: 'feature unbekannt' });
    const message = String(body.message || '').trim();
    if (!message) return json(res, 400, { ok: false, sent: false, error: 'message leer' });
    const now = Date.now();
    if (now - lastSendAt < 1000) return json(res, 429, { ok: false, sent: false, error: 'zu früh', retryAfterMs: 1000 - (now - lastSendAt) });
    lastSendAt = now;
    const entry = { at: new Date(now).toISOString(), feature: body.feature, message: message.slice(0, 500), length: message.length };
    sent.push(entry);
    console.log(`[attrappe] Chat ← ${entry.message}`);
    return json(res, 200, { ok: true, sent: true, sender: 'bot' });
  }

  if (req.method === 'GET' && path === '/api/twitch/channel') return json(res, 200, channel);
  if (req.method === 'GET' && path === '/api/chat/active') {
    return json(res, 200, { updatedAt: active[0]?.lastChatAt ?? null, windowMinutes: 60, chatters: active });
  }

  // ── Steuerung ──
  if (req.method === 'POST' && path === '/attrappe/chat') {
    const b = await readBody(req);
    if (!b || !b.login || !b.text) return json(res, 400, { error: 'login und text nötig' });
    const payload = {
      messageId: randomUUID(),
      userId: String(b.userId || `attrappe-${b.login.toLowerCase()}`),
      userLogin: b.login.toLowerCase(),
      userName: b.name || b.login,
      text: b.text,
      at: new Date().toISOString(),
      badges: { broadcaster: false, moderator: false, vip: false, subscriber: false, ...(b.badges || {}) },
      private: !!b.private,
    };
    let delivered = 0;
    for (const s of streams) {
      if (!matches(s, payload.text)) continue;
      s.res.write(`event: chat\ndata: ${JSON.stringify(payload)}\n\n`);
      delivered++;
    }
    // Wie der Server: jede Nachricht zählt für aktive Chatter (außer !privat).
    if (!payload.private) {
      active = active.filter((c) => c.id !== payload.userId);
      active.unshift({ id: payload.userId, login: payload.userLogin, name: payload.userName, lastChatAt: payload.at,
        moderator: payload.badges.moderator, vip: payload.badges.vip, subscriber: payload.badges.subscriber });
    }
    return json(res, 200, { delivered, messageId: payload.messageId });
  }
  if (req.method === 'POST' && path === '/attrappe/privacy') {
    const b = (await readBody(req)) || {};
    const n = broadcast('privacy', { userId: String(b.userId), userLogin: b.login, private: !!b.private, at: new Date().toISOString() });
    return json(res, 200, { delivered: n });
  }
  if (req.method === 'POST' && path === '/attrappe/channel') {
    const b = (await readBody(req)) || {};
    if (b.category !== channel.category) channel.categoryChangedAt = b.categoryChangedAt || new Date().toISOString();
    channel = { ...channel, category: b.category ?? null, live: b.live ?? channel.live, updatedAt: new Date().toISOString() };
    return json(res, 200, channel);
  }
  if (req.method === 'POST' && path === '/attrappe/active') {
    const b = (await readBody(req)) || {};
    active = (b.chatters || []).map((c) => ({ lastChatAt: new Date().toISOString(), moderator: false, vip: false, subscriber: false, ...c }));
    return json(res, 200, { count: active.length });
  }
  if (req.method === 'POST' && path === '/attrappe/drop') {
    const n = streams.size;
    for (const s of streams) s.res.destroy();
    return json(res, 200, { dropped: n });
  }
  if (req.method === 'GET' && path === '/attrappe/sent') return json(res, 200, sent);
  if (req.method === 'GET' && path === '/attrappe/status') {
    return json(res, 200, { streams: [...streams].map((s) => ({ client: s.client, prefixes: s.prefixes, since: s.since })), registrations, channel, active: active.length, sent: sent.length });
  }

  json(res, 404, { error: 'not found' });
};

// Nur lokal, IPv4 und IPv6 („localhost“ kann auf beides zeigen).
http.createServer(handler).listen(port, '127.0.0.1', () => console.log(`[attrappe] läuft auf http://localhost:${port}`));
http.createServer(handler).listen(port, '::1').on('error', () => { /* ohne IPv6 */ });
