// Диаграмма вариантов использования и схема архитектуры (ручная SVG-вёрстка)
const fs = require('fs');
const path = require('path');
const { htmlToPng, trim } = require('./lib/png');

const OUT = path.join(__dirname, 'img');
fs.mkdirSync(OUT, { recursive: true });

const C = {
  navy: '#1B2A41', blue: '#2D6CDF', text: '#212121', muted: '#7A8794', bg: '#F4F6F8', line: '#5B6875',
  purple: '#7E57C2', orange: '#E08A00', green: '#1E8449', gray: '#C9D2DC',
};
const esc = s => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

function save(name, svg, w, h) {
  const html = `<!doctype html><html><head><meta charset="utf-8"><style>
    html,body{margin:0;background:#fff} svg{display:block;font-family:"Segoe UI",Arial,sans-serif}
  </style></head><body>${svg}</body></html>`;
  const htmlPath = path.join(OUT, name + '.html');
  fs.writeFileSync(htmlPath, html, 'utf8');
  const png = path.join(OUT, name + '.png');
  htmlToPng(htmlPath, png, w + 20, h + 200, 2);
  const size = trim(png, 20);
  fs.unlinkSync(htmlPath);
  console.log(name, size.w, 'x', size.h);
}

// ---------- Варианты использования ----------
function actor(x, y, label) {
  return `<g stroke="${C.navy}" stroke-width="3" fill="none" stroke-linecap="round">
    <circle cx="${x}" cy="${y - 52}" r="16" fill="#fff"/>
    <line x1="${x}" y1="${y - 36}" x2="${x}" y2="${y + 8}"/>
    <line x1="${x - 28}" y1="${y - 20}" x2="${x + 28}" y2="${y - 20}"/>
    <line x1="${x}" y1="${y + 8}" x2="${x - 22}" y2="${y + 44}"/>
    <line x1="${x}" y1="${y + 8}" x2="${x + 22}" y2="${y + 44}"/>
  </g>
  <text x="${x}" y="${y + 74}" text-anchor="middle" font-size="19" font-weight="600" fill="${C.navy}">${esc(label)}</text>`;
}

function useCase(cx, cy, text, color) {
  const rx = 205, ry = 29;
  return `<ellipse cx="${cx}" cy="${cy}" rx="${rx}" ry="${ry}" fill="#fff" stroke="${color}" stroke-width="2"/>
  <text x="${cx}" y="${cy + 6}" text-anchor="middle" font-size="16" fill="${C.text}">${esc(text)}</text>`;
}

function useCaseDiagram() {
  const W = 1640, H = 1200;
  const groups = [
    { title: 'Личный кабинет клиента', actor: 'Клиент', side: 'L', top: true, color: C.blue, items: [
      'Регистрация и ведение учётной записи', 'Просмотр статуса заявки в реальном времени',
      'Согласование или отклонение стоимости', 'Просмотр истории заявок', 'Скачивание квитанции и акта работ'] },
    { title: 'Управление ремонтом', actor: 'Мастер', side: 'R', top: true, color: C.purple, items: [
      'Просмотр назначенных заявок', 'Проведение диагностики и смета', 'Ведение журнала работ и гарантии',
      'Запрос и резервирование запчастей', 'Изменение статуса в пределах этапа'] },
    { title: 'Приём заявок · Расчёты и документы', actor: 'Приёмщик', side: 'L', top: false, color: C.navy, items: [
      'Оформление заявки и регистрация устройства', 'Печать квитанции о приёме',
      'Приём оплаты', 'Выдача устройства и акт выдачи', 'Просмотр всех заявок (без изменения ремонта)'] },
    { title: 'Склад запчастей', actor: 'Кладовщик', side: 'R', top: false, color: C.orange, items: [
      'Учёт остатков и оприходование', 'Выдача деталей мастерам', 'Проведение инвентаризации',
      'Просмотр истории движения запчастей'] },
  ];
  const boxW = 500, gap = 70, sysX = 250, sysW = W - 2 * sysX;
  const colX = { L: sysX + 40, R: sysX + sysW - 40 - boxW };
  const rowY = { true: 110, false: 700 };
  let body = `<rect x="${sysX}" y="30" width="${sysW}" height="${H - 60}" rx="28" fill="${C.bg}" stroke="${C.gray}" stroke-width="2"/>
  <text x="${W / 2}" y="72" text-anchor="middle" font-size="24" font-weight="700" fill="${C.navy}">Сервис<tspan fill="${C.blue}">Деск</tspan></text>`;
  const auth = { cx: W / 2, cy: 620 };
  for (const g of groups) {
    const x = colX[g.side], y = rowY[g.top];
    const h = 64 + g.items.length * 76;
    body += `<rect x="${x}" y="${y}" width="${boxW}" height="${h}" rx="20" fill="#fff" fill-opacity="0.65" stroke="${g.color}" stroke-opacity="0.45" stroke-width="2"/>
    <text x="${x + 22}" y="${y + 36}" font-size="18" font-weight="700" fill="${g.color}">${esc(g.title)}</text>`;
    const ucx = x + boxW / 2;
    const ax = g.side === 'L' ? 115 : W - 115;
    const ay = y + h / 2 + 10;
    g.items.forEach((it, i) => {
      const cy = y + 92 + i * 76;
      const ex = g.side === 'L' ? ucx - 205 : ucx + 205;
      body += `<line x1="${g.side === 'L' ? ax + 30 : ax - 30}" y1="${ay - 20}" x2="${ex}" y2="${cy}" stroke="${C.line}" stroke-width="1.6"/>`;
      body += useCase(ucx, cy, it, g.color);
    });
    body += actor(ax, ay, g.actor);
    // include → авторизация
    const fromY = g.top ? y + h : y;
    const toY = g.top ? auth.cy - 29 : auth.cy + 29;
    const fromX = g.side === 'L' ? x + boxW - 60 : x + 60;
    const toX = g.side === 'L' ? auth.cx - 120 : auth.cx + 120;
    body += `<line x1="${fromX}" y1="${fromY}" x2="${toX}" y2="${toY}" stroke="${C.muted}" stroke-width="1.6" stroke-dasharray="7 5" marker-end="url(#arr)"/>`;
  }
  body += useCase(auth.cx, auth.cy, 'Авторизация (JWT, блокировка после 5 попыток)', C.green);
  body += `<text x="${auth.cx - 250}" y="${auth.cy - 44}" font-size="14" fill="${C.muted}" font-style="italic">«include»</text>
  <text x="${auth.cx + 200}" y="${auth.cy - 44}" font-size="14" fill="${C.muted}" font-style="italic">«include»</text>`;
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}">
  <defs><marker id="arr" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="9" markerHeight="9" orient="auto-start-reverse">
    <path d="M0,0 L10,5 L0,10 z" fill="${C.muted}"/></marker></defs>${body}</svg>`;
  save('use_case', svg, W, H);
}

// ---------- Архитектура ----------
function box(x, y, w, h, { fill = '#fff', stroke = C.gray, rx = 18, sw = 2, dash = '' } = {}) {
  return `<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${rx}" fill="${fill}" stroke="${stroke}" stroke-width="${sw}" ${dash ? `stroke-dasharray="${dash}"` : ''}/>`;
}
function label(x, y, text, { size = 16, weight = 400, color = C.text, anchor = 'start' } = {}) {
  return `<text x="${x}" y="${y}" font-size="${size}" font-weight="${weight}" fill="${color}" text-anchor="${anchor}">${esc(text)}</text>`;
}
function arrow(x1, y1, x2, y2, text, { color = C.line, dy = -10, both = false, dash = '' } = {}) {
  const mx = (x1 + x2) / 2, my = (y1 + y2) / 2;
  return `<line x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}" stroke="${color}" stroke-width="2.2" marker-end="url(#a2)" ${both ? 'marker-start="url(#a2)"' : ''} ${dash ? `stroke-dasharray="${dash}"` : ''}/>`
    + (text ? text.split('\n').map((t, i) => label(mx, my + dy + i * 19, t, { size: 14, color: C.muted, anchor: 'middle' })).join('') : '');
}
function comp(x, y, w, title, lines, color) {
  const h = 54 + lines.length * 24;
  return box(x, y, w, h, { stroke: color, rx: 16 })
    + `<rect x="${x}" y="${y}" width="${w}" height="40" rx="16" fill="${color}"/><rect x="${x}" y="${y + 22}" width="${w}" height="18" fill="${color}"/>`
    + label(x + 16, y + 27, title, { size: 17, weight: 700, color: '#fff' })
    + lines.map((l, i) => label(x + 16, y + 66 + i * 24, l, { size: 15, color: C.text })).join('');
}

function architecture() {
  const W = 1640, H = 860;
  let b = '';
  // клиенты
  b += label(40, 50, 'Клиентский уровень', { size: 20, weight: 700, color: C.navy });
  b += comp(40, 80, 400, 'Настольное приложение', ['C# · WPF · .NET 8', 'Приёмщик, мастер, кладовщик', 'HttpClient + SignalR Client', 'Windows 10/11, 1366×768+'], C.navy);
  b += comp(40, 430, 400, 'Веб-кабинет клиента', ['Vue 3 · Vite · Vue Router', 'SPA в браузере клиента', 'fetch + @microsoft/signalr', 'Адаптив: 1200 / 768 / 480 px'], C.blue);

  // docker
  b += box(540, 30, 1070, 800, { fill: '#F2F7FF', stroke: '#8FB3F0', rx: 28, dash: '10 7' });
  b += label(570, 70, 'Сервер · Docker Compose', { size: 20, weight: 700, color: C.blue });

  b += comp(580, 430, 330, 'web · nginx', ['Статика собранного Vue', 'Прокси /api и /hubs → api', 'порт 8088 → 80'], '#2E7D32');

  // API
  const ax = 990, ay = 100;
  b += box(ax, ay, 580, 470, { stroke: C.purple, rx: 20 });
  b += `<rect x="${ax}" y="${ay}" width="580" height="44" rx="20" fill="${C.purple}"/><rect x="${ax}" y="${ay + 24}" width="580" height="20" fill="${C.purple}"/>`;
  b += label(ax + 18, ay + 30, 'api · ASP.NET Core 8 Web API', { size: 18, weight: 700, color: '#fff' });
  const layer = (y, title, text, color) => box(ax + 24, y, 532, 76, { fill: color + '14', stroke: color + '66', rx: 14 })
    + label(ax + 44, y + 30, title, { size: 16, weight: 700, color }) + label(ax + 44, y + 56, text, { size: 14, color: C.text });
  b += layer(ay + 64, 'Controllers (REST, JWT, роли)', 'Auth · Profile · Orders · Clients · Parts · Inventory · Documents', C.purple);
  b += layer(ay + 156, 'Services (бизнес-логика)', 'OrderWorkflow · CostCalculator · Warehouse · Payments · Pdf', C.purple);
  b += layer(ay + 248, 'Realtime · Documents', 'SignalR OrdersHub · QuestPDF (квитанция, смета, акты)', C.purple);
  b += layer(ay + 340, 'Data Access', 'EF Core 8 + Pomelo MySQL · AppDbContext · транзакции', C.purple);
  for (const y of [ay + 140, ay + 232, ay + 324]) b += arrow(ax + 290, y, ax + 290, y + 16, '', { color: C.purple });

  // DB
  b += comp(990, 640, 580, 'db · MySQL 8.0', ['БД servicedesk · 18 таблиц · InnoDB · utf8mb4', 'Том db_data · init-скрипты 01_schema.sql, 02_seed.sql'], C.orange);

  // стрелки
  b += arrow(440, 160, 990, 160, 'REST/JSON · JWT Bearer · порт 5080', { dy: -12 });
  b += arrow(440, 230, 990, 330, '', { dash: '8 6' });
  b += label(730, 248, 'WebSocket (SignalR) — обновление статусов', { size: 14, color: C.muted, anchor: 'middle' });
  b += arrow(440, 500, 580, 500, 'HTTP', { dy: -12 });
  b += arrow(910, 470, 990, 420, '', {});
  b += label(905, 420, '/api, /hubs', { size: 14, color: C.muted, anchor: 'end' });
  b += arrow(1280, 570, 1280, 640, '');
  b += label(1294, 612, 'TCP 3306 · MySqlConnector', { size: 14, color: C.muted });
  b += label(40, 760, 'Документы PDF формируются на сервере и скачиваются', { size: 15, color: C.muted });
  b += label(40, 784, 'клиентом и сотрудниками через REST API.', { size: 15, color: C.muted });

  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}">
  <defs><marker id="a2" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="8" markerHeight="8" orient="auto-start-reverse">
    <path d="M0,0 L10,5 L0,10 z" fill="${C.line}"/></marker></defs>${b}</svg>`;
  save('architecture', svg, W, H);
}

useCaseDiagram();
architecture();
