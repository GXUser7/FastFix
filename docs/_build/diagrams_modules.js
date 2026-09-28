// Диаграммы для документа «Модульная схема»
const fs = require('fs');
const path = require('path');
const { htmlToPng, trim } = require('./lib/png');

const OUT = path.join(__dirname, 'img');
const C = {
  navy: '#1B2A41', blue: '#2D6CDF', text: '#212121', muted: '#7A8794', bg: '#F4F6F8', line: '#5B6875',
  purple: '#7E57C2', orange: '#E08A00', green: '#1E8449', teal: '#00897B', red: '#C0392B', gray: '#C9D2DC',
};
const esc = s => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
const DEFS = `<defs><marker id="ar" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="8" markerHeight="8" orient="auto-start-reverse"><path d="M0,0 L10,5 L0,10 z" fill="${C.line}"/></marker>
<marker id="ao" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="9" markerHeight="9" orient="auto-start-reverse"><path d="M0,0 L10,5 L0,10" fill="none" stroke="${C.line}" stroke-width="1.5"/></marker></defs>`;

function save(name, body, W, H) {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}">${DEFS}${body}</svg>`;
  const html = `<!doctype html><html><head><meta charset="utf-8"><style>html,body{margin:0;background:#fff} svg{display:block;font-family:"Segoe UI",Arial,sans-serif}</style></head><body>${svg}</body></html>`;
  const hp = path.join(OUT, name + '.html');
  fs.writeFileSync(hp, html, 'utf8');
  const png = path.join(OUT, name + '.png');
  htmlToPng(hp, png, W + 20, H + 200, 2);
  const s = trim(png, 20);
  fs.unlinkSync(hp);
  console.log(name, s.w, 'x', s.h);
}
const text = (x, y, t, { size = 15, weight = 400, color = C.text, anchor = 'start', italic = false } = {}) =>
  `<text x="${x}" y="${y}" font-size="${size}" font-weight="${weight}" fill="${color}" text-anchor="${anchor}"${italic ? ' font-style="italic"' : ''}>${esc(t)}</text>`;
const rect = (x, y, w, h, { fill = '#fff', stroke = C.gray, rx = 12, sw = 1.6, dash = '' } = {}) =>
  `<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${rx}" fill="${fill}" stroke="${stroke}" stroke-width="${sw}"${dash ? ` stroke-dasharray="${dash}"` : ''}/>`;
const line = (x1, y1, x2, y2, { color = C.line, sw = 1.6, dash = '', arrow = true, open = false } = {}) =>
  `<line x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}" stroke="${color}" stroke-width="${sw}"${dash ? ` stroke-dasharray="${dash}"` : ''}${arrow ? ` marker-end="url(#${open ? 'ao' : 'ar'})"` : ''}/>`;

// ---------- иерархическая модульная схема ----------
function tree() {
  const subs = [
    { t: 'Приём заявок', c: C.blue, m: ['Поиск и регистрация клиента', 'Регистрация устройства', 'Чек-лист комплектности', 'Оформление заявки', 'Назначение мастера и срока', 'Реестр заявок, поиск, фильтр'] },
    { t: 'Управление ремонтом', c: C.purple, m: ['Очередь заявок мастера', 'Диагностика и заключение', 'Журнал работ', 'Жизненный цикл (автомат статусов)', 'История статусов', 'Контроль сроков'] },
    { t: 'Склад запчастей', c: C.orange, m: ['Номенклатура и остатки', 'Оприходование', 'Резервирование под заявку', 'Выдача мастерам (списание)', 'Инвентаризация', 'Журнал движения'] },
    { t: 'Расчёты и документы', c: C.green, m: ['Расчёт сметы', 'Приём оплаты', 'Квитанция о приёме', 'Смета', 'Акт выполненных работ', 'Акт выдачи'] },
    { t: 'Личный кабинет клиента', c: C.teal, m: ['Регистрация и вход', 'Профиль', 'Мои заявки', 'Карточка и шкала этапов', 'Согласование стоимости', 'Уведомления в реальном времени'] },
    { t: 'Администрирование и безопасность', c: C.navy, m: ['Учётные записи и роли', 'Аутентификация JWT', 'Хеширование паролей BCrypt', 'Блокировка после 5 попыток', 'Проверка прав доступа', 'Обработка ошибок API'] },
  ];
  const colW = 250, gap = 22, W = subs.length * colW + (subs.length - 1) * gap + 80, H = 760;
  let b = '';
  const rootW = 520, rootX = (W - rootW) / 2;
  b += rect(rootX, 30, rootW, 70, { fill: C.navy, stroke: C.navy, rx: 18 });
  b += text(W / 2, 62, 'ИС «СервисДеск»', { size: 22, weight: 700, color: '#fff', anchor: 'middle' });
  b += text(W / 2, 86, 'управление заявками в сервисный центр', { size: 14, color: '#C9D6EA', anchor: 'middle' });
  const busY = 140;
  b += line(W / 2, 100, W / 2, busY, { arrow: false, sw: 2 });
  const x0 = 40 + colW / 2, x1 = 40 + (subs.length - 1) * (colW + gap) + colW / 2;
  b += line(x0, busY, x1, busY, { arrow: false, sw: 2 });
  subs.forEach((s, i) => {
    const x = 40 + i * (colW + gap);
    b += line(x + colW / 2, busY, x + colW / 2, 170, { arrow: false, sw: 2 });
    b += rect(x, 170, colW, 64, { fill: s.c, stroke: s.c, rx: 14 });
    const words = s.t.split(' ');
    const lines = s.t.length > 22 ? [words.slice(0, Math.ceil(words.length / 2)).join(' '), words.slice(Math.ceil(words.length / 2)).join(' ')] : [s.t];
    lines.forEach((l, li) => { b += text(x + colW / 2, 170 + (lines.length === 1 ? 39 : 28 + li * 21), l, { size: 16, weight: 700, color: '#fff', anchor: 'middle' }); });
    const lx = x + 18;
    b += line(lx, 234, lx, 234 + 30 + (s.m.length - 1) * 76 + 24, { arrow: false, color: s.c, sw: 2 });
    s.m.forEach((m, j) => {
      const y = 262 + j * 76;
      b += line(lx, y + 24, lx + 16, y + 24, { arrow: false, color: s.c, sw: 2 });
      b += rect(lx + 16, y, colW - 34, 48, { fill: s.c + '12', stroke: s.c + '80', rx: 10 });
      const ws = m.split(' ');
      const two = m.length > 24;
      const l1 = two ? ws.slice(0, Math.ceil(ws.length / 2)).join(' ') : m;
      const l2 = two ? ws.slice(Math.ceil(ws.length / 2)).join(' ') : '';
      b += text(lx + 16 + (colW - 34) / 2, y + (two ? 20 : 29), l1, { size: 13.5, anchor: 'middle' });
      if (two) b += text(lx + 16 + (colW - 34) / 2, y + 38, l2, { size: 13.5, anchor: 'middle' });
    });
  });
  save('modules_tree', b, W, H);
}

// ---------- слоистая схема компонентов ----------
function layered(name, title, layers, { W = 1500, note = '' } = {}) {
  let b = text(40, 44, title, { size: 22, weight: 700, color: C.navy });
  let y = 70;
  const chipH = 40, pad = 18;
  layers.forEach((L, li) => {
    // раскладка «чипов» по строкам
    const maxW = W - 80 - 2 * pad - 270;
    const rows = [[]]; let rw = 0;
    const widths = L.items.map(it => Math.max(120, it.length * 8.2 + 34));
    L.items.forEach((it, i) => {
      if (rw + widths[i] > maxW && rows[rows.length - 1].length) { rows.push([]); rw = 0; }
      rows[rows.length - 1].push(i); rw += widths[i] + 12;
    });
    const h = pad * 2 + rows.length * chipH + (rows.length - 1) * 12;
    b += rect(40, y, W - 80, h, { fill: L.c + '0D', stroke: L.c + '66', rx: 18 });
    b += text(60, y + 32, L.t, { size: 17, weight: 700, color: L.c });
    if (L.sub) b += text(60, y + 54, L.sub, { size: 13, color: C.muted });
    rows.forEach((r, ri) => {
      let x = 40 + 270;
      r.forEach(i => {
        const cy = y + pad + ri * (chipH + 12);
        b += rect(x, cy, widths[i], chipH, { fill: '#fff', stroke: L.c + 'AA', rx: 10 });
        b += text(x + widths[i] / 2, cy + 26, L.items[i], { size: 14.5, anchor: 'middle' });
        x += widths[i] + 12;
      });
    });
    y += h;
    if (li < layers.length - 1) {
      const lbl = layers[li].down || '';
      b += line(W / 2, y + 2, W / 2, y + 40, { sw: 2.2 });
      if (lbl) b += text(W / 2 + 14, y + 26, lbl, { size: 13.5, color: C.muted });
      y += 44;
    }
  });
  if (note) { b += text(40, y + 32, note, { size: 14, color: C.muted }); y += 40; }
  save(name, b, W, y + 20);
}

// ---------- диаграмма последовательности ----------
function sequence(name, title, parts, msgs) {
  const colW = 230, W = 60 + parts.length * colW, top = 70, headH = 56;
  const stepH = 56;
  const H = top + headH + 30 + msgs.length * stepH + 50;
  let b = text(30, 40, title, { size: 20, weight: 700, color: C.navy });
  const cx = i => 30 + colW / 2 + i * colW;
  parts.forEach((p, i) => {
    b += rect(cx(i) - 95, top, 190, headH, { fill: p.c, stroke: p.c, rx: 12 });
    const ls = p.t.split('\n');
    ls.forEach((l, li) => { b += text(cx(i), top + (ls.length === 1 ? 34 : 24 + li * 20), l, { size: 14.5, weight: 700, color: '#fff', anchor: 'middle' }); });
    b += line(cx(i), top + headH, cx(i), H - 30, { arrow: false, dash: '6 6', color: C.gray, sw: 1.6 });
  });
  msgs.forEach((m, k) => {
    const y = top + headH + 40 + k * stepH;
    const a = parts.findIndex(p => p.id === m[0]), z = parts.findIndex(p => p.id === m[1]);
    const ret = m[3] === 'ret';
    if (a === z) {
      b += `<path d="M${cx(a)},${y - 8} h46 v22 h-40" fill="none" stroke="${C.line}" stroke-width="1.6" marker-end="url(#ar)"/>`;
      b += text(cx(a) + 54, y + 2, m[2], { size: 13.5 });
    } else {
      const dir = z > a ? 1 : -1;
      b += line(cx(a) + dir * 6, y, cx(z) - dir * 8, y, { dash: ret ? '7 5' : '', open: ret });
      b += text((cx(a) + cx(z)) / 2, y - 9, m[2], { size: 13.5, anchor: 'middle', color: ret ? C.muted : C.text });
    }
    b += text(cx(0) - 110 + 0, y + 5, String(k + 1), { size: 12, color: C.muted });
  });
  save(name, b, W, H);
}

// ---------- схема развёртывания ----------
function deployment() {
  const W = 1500, H = 760;
  let b = '';
  b += rect(360, 30, 1110, 700, { fill: '#F2F7FF', stroke: '#8FB3F0', rx: 24, dash: '10 7' });
  b += text(386, 66, 'Сервер (Linux / Windows) · Docker Engine · docker compose, проект servicedesk', { size: 18, weight: 700, color: C.blue });
  b += rect(390, 90, 1050, 460, { fill: '#fff', stroke: C.gray, rx: 18 });
  b += text(412, 535, 'Сеть servicedesk_default (bridge)', { size: 15, weight: 600, color: C.muted });
  const cont = (x, y, w, t, lines, c) => {
    let s = rect(x, y, w, 60 + lines.length * 24, { fill: c + '10', stroke: c, rx: 16, sw: 2 });
    s += text(x + 18, y + 32, t, { size: 17, weight: 700, color: c });
    lines.forEach((l, i) => { s += text(x + 18, y + 60 + i * 24, l, { size: 14.5 }); });
    return s;
  };
  b += cont(420, 150, 310, 'web', ['образ: nginx:1.27-alpine', 'статика Vue (npm run build)', 'прокси /api, /hubs → api:8080', 'порт хоста 8088 → 80'], '#2E7D32');
  b += cont(770, 150, 320, 'api', ['образ: aspnet:8.0', 'ServiceDesk.Api.dll', 'порт хоста 5080 → 8080', 'depends_on: db (healthy)'], C.purple);
  b += cont(1130, 150, 290, 'db', ['образ: mysql:8.0', 'БД servicedesk', 'порт хоста 3307 → 3306', 'healthcheck: mysqladmin ping'], C.orange);
  b += cont(1130, 380, 290, 'backup', ['образ: mysql:8.0', 'mysqldump раз в сутки', 'хранение 30 дней'], C.navy);
  b += line(730, 230, 770, 230, { sw: 2.2 });
  b += line(1090, 230, 1130, 230, { sw: 2.2 });
  b += line(1275, 380, 1275, 322, { sw: 2.2 });
  // тома
  b += rect(420, 590, 480, 110, { fill: '#fff', stroke: C.gray, rx: 16 });
  b += text(440, 622, 'Тома и каталоги', { size: 16, weight: 700, color: C.navy });
  b += text(440, 650, 'db_data → /var/lib/mysql (данные СУБД)', { size: 14.5 });
  b += text(440, 676, './db → /docker-entrypoint-initdb.d (скрипты)', { size: 14.5 });
  b += rect(940, 590, 500, 110, { fill: '#fff', stroke: C.gray, rx: 16 });
  b += text(960, 622, 'Переменные окружения (.env)', { size: 16, weight: 700, color: C.navy });
  b += text(960, 650, 'MYSQL_ROOT_PASSWORD, DB_PASSWORD, JWT_KEY', { size: 14.5 });
  b += text(960, 676, 'ConnectionStrings__Default, Jwt__Key', { size: 14.5 });
  b += line(1275, 550, 1275, 590, { arrow: false, dash: '5 5' });
  // клиенты
  b += rect(30, 150, 280, 150, { fill: '#fff', stroke: C.navy, rx: 16, sw: 2 });
  b += text(50, 182, 'Рабочее место сотрудника', { size: 16, weight: 700, color: C.navy });
  b += text(50, 210, 'Windows 10/11', { size: 14.5 });
  b += text(50, 234, 'ServiceDesk.Desktop.exe', { size: 14.5 });
  b += text(50, 258, 'appsettings.json: ApiUrl', { size: 14.5 });
  b += rect(30, 400, 280, 130, { fill: '#fff', stroke: C.blue, rx: 16, sw: 2 });
  b += text(50, 432, 'Устройство клиента', { size: 16, weight: 700, color: C.blue });
  b += text(50, 460, 'Браузер (ПК / смартфон)', { size: 14.5 });
  b += text(50, 484, 'http://сервер:8088', { size: 14.5 });
  b += `<path d="M310,180 H350 V122 H930 V146" fill="none" stroke="${C.line}" stroke-width="2.2" marker-end="url(#ar)"/>`;
  b += text(560, 114, 'HTTP :5080 (REST + WebSocket)', { size: 14, color: C.muted });
  b += line(310, 460, 470, 318, { sw: 2.2 });
  b += text(196, 368, 'HTTP :8088', { size: 14, color: C.muted });
  save('deployment', b, W, H);
}

tree();
layered('api_modules', 'Серверная часть ServiceDesk.Api — модули и слои', [
  { t: 'Контроллеры', sub: 'Controllers/ · REST, роли', c: C.purple, down: 'вызов сервисов', items: ['AuthController', 'ProfileController', 'DictionariesController', 'ClientsController', 'OrdersController', 'DocumentsController', 'PartsController', 'InventoriesController'] },
  { t: 'Сервисы бизнес-логики', sub: 'Services/', c: C.blue, down: 'EF Core LINQ, транзакции', items: ['AuthService', 'TokenService', 'OrderService', 'OrderWorkflow', 'CostCalculator', 'WarehouseService', 'PaymentService', 'DocumentService (QuestPDF)', 'NotificationService'] },
  { t: 'Доступ к данным', sub: 'Data/ · Pomelo MySQL', c: C.orange, down: 'MySqlConnector, TCP 3306', items: ['AppDbContext', 'Entities: User, Role, Device, Order, …', 'Конфигурация связей (Fluent API)'] },
  { t: 'СУБД MySQL 8.0', sub: 'БД servicedesk', c: C.navy, items: ['18 таблиц', '2 представления', 'FK, CHECK, UNIQUE'] },
], { note: 'Сквозные модули: Infrastructure (обработка ошибок, CurrentUser, нормализация телефона), Hubs/OrdersHub (SignalR), Swagger.' });
layered('desktop_modules', 'Настольное приложение ServiceDesk.Desktop (WPF) — модули', [
  { t: 'Окна и страницы', sub: 'Views/ · XAML + code-behind', c: C.navy, down: 'вызовы сервисов', items: ['LoginWindow', 'MainWindow (меню по роли)', 'OrdersPage', 'NewOrderPage', 'OrderCardPage', 'PartsPage', 'PartRequestsPage', 'InventoryPage', 'MovementsPage', 'Диалоги'] },
  { t: 'Оформление', sub: 'Themes/, Controls/, Converters/', c: C.blue, down: '', items: ['Theme.xaml (палитра, шрифты, кнопки)', 'StatusBadge', 'StatusStepper', 'StatusColorConverter'] },
  { t: 'Сервисы клиента', sub: 'Services/', c: C.purple, down: 'HTTP/JSON + JWT, WebSocket', items: ['ApiClient (HttpClient)', 'Session (токен, роль)', 'RealtimeClient (SignalR)', 'DocumentOpener (PDF)'] },
  { t: 'ServiceDesk.Api', sub: 'REST API сервера', c: C.orange, items: ['/api/*', '/hubs/orders'] },
], { note: 'Общие DTO берутся из библиотеки ServiceDesk.Contracts — одни и те же классы используются сервером и настольным приложением.' });
layered('web_modules', 'Веб-кабинет клиента (Vue 3) — модули', [
  { t: 'Маршрутизация', sub: 'router.js · защита маршрутов', c: C.teal, down: '', items: ['/login', '/register', '/ (мои заявки)', '/orders/:id', '/profile', '/profile/edit'] },
  { t: 'Представления', sub: 'views/', c: C.blue, down: '', items: ['LoginView', 'RegisterView', 'OrdersView', 'OrderView', 'ProfileView', 'ProfileEditView'] },
  { t: 'Компоненты', sub: 'components/', c: C.purple, down: 'обращение к API', items: ['AppHeader', 'StatusBadge', 'StatusStepper', 'OrderCard', 'ToastHost'] },
  { t: 'Данные и связь', sub: 'api/, stores/', c: C.navy, down: 'fetch + JWT, SignalR', items: ['http.js (fetch, ошибки)', 'realtime.js (SignalR)', 'session.js (токен в localStorage)', 'format.js'] },
  { t: 'ServiceDesk.Api', sub: 'через nginx /api, /hubs', c: C.orange, items: ['REST', 'WebSocket'] },
]);
sequence('seq_create_order', 'Сценарий 1. Оформление заявки приёмщиком', [
  { id: 'u', t: 'Приёмщик', c: C.navy }, { id: 'w', t: 'Desktop\nNewOrderPage', c: C.blue }, { id: 'a', t: 'API\nOrdersController', c: C.purple },
  { id: 's', t: 'OrderService', c: C.purple }, { id: 'd', t: 'MySQL', c: C.orange }, { id: 'h', t: 'OrdersHub\n(SignalR)', c: C.teal },
], [
  ['u', 'w', 'ввод телефона клиента'], ['w', 'a', 'GET /api/clients?phone='], ['a', 'd', 'SELECT users'], ['a', 'w', 'клиент / 404', 'ret'],
  ['u', 'w', 'устройство, неисправность, комплектность'], ['w', 'a', 'POST /api/orders'], ['a', 's', 'CreateAsync(dto)'],
  ['s', 'd', 'BEGIN; INSERT users?, devices, orders, …'], ['s', 'd', 'INSERT order_status_history; COMMIT'],
  ['s', 'h', 'OrderUpdated (staff, client)'], ['a', 'w', '201 Created {id: 10241}', 'ret'],
  ['w', 'a', 'GET /api/orders/10241/documents/receipt'], ['a', 'w', 'application/pdf — квитанция', 'ret'], ['w', 'u', 'печать квитанции', 'ret'],
]);
sequence('seq_approval', 'Сценарий 2. Смета и согласование стоимости клиентом', [
  { id: 'm', t: 'Мастер\n(Desktop)', c: C.navy }, { id: 'a', t: 'API', c: C.purple }, { id: 's', t: 'OrderWorkflow\nCostCalculator', c: C.purple },
  { id: 'd', t: 'MySQL', c: C.orange }, { id: 'h', t: 'OrdersHub', c: C.teal }, { id: 'c', t: 'Клиент\n(Vue)', c: C.blue },
], [
  ['m', 'a', 'POST /orders/{id}/works, /parts'], ['a', 's', 'пересчёт стоимости S'], ['s', 'd', 'UPDATE orders.total_cost'],
  ['m', 'a', 'POST /orders/{id}/status → approval'], ['a', 's', 'CanChange(diagnostics → approval, master)'],
  ['s', 'd', 'UPDATE orders; INSERT history'], ['a', 'h', 'OrderUpdated'], ['h', 'c', 'событие по WebSocket', 'ret'],
  ['c', 'a', 'GET /api/orders/{id}'], ['a', 'c', 'смета, заключение, S', 'ret'], ['c', 'a', 'POST /orders/{id}/decision {approve}'],
  ['a', 's', 'approval → in_work / waiting_parts'], ['s', 'd', 'UPDATE orders; INSERT history'], ['a', 'h', 'OrderUpdated'], ['h', 'm', 'обновление списка', 'ret'],
]);
sequence('seq_parts', 'Сценарий 3. Резервирование и выдача запчасти', [
  { id: 'm', t: 'Мастер', c: C.navy }, { id: 'a', t: 'API', c: C.purple }, { id: 'w', t: 'WarehouseService', c: C.purple },
  { id: 'd', t: 'MySQL', c: C.orange }, { id: 'k', t: 'Кладовщик', c: C.orange },
], [
  ['m', 'a', 'POST /orders/{id}/parts {partId, qty}'], ['a', 'w', 'ReserveAsync'], ['w', 'd', 'SELECT … FOR UPDATE (остаток)'],
  ['w', 'd', 'резерв: reserved += qty, reserve'], ['w', 'w', 'нет остатка → requested'], ['a', 'm', '201 резерв / ожидание', 'ret'],
  ['k', 'a', 'GET /api/parts/requests'], ['a', 'k', 'список запросов мастеров', 'ret'], ['k', 'a', 'POST /parts/requests/{id}/issue'],
  ['a', 'w', 'IssueAsync'], ['w', 'd', 'on_hand −= qty; reserved −= qty'], ['w', 'd', 'INSERT part_movements (issue)'], ['a', 'k', '200 выдано', 'ret'],
]);
deployment();
