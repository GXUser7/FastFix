// Генератор db/02_seed.sql: справочники + согласованные демо-данные
// (итоги смет, остатки склада и журнал движения рассчитываются здесь, а не вручную)
// Запуск: node tools/generate_seed.js
const fs = require('fs');
const path = require('path');
const bcrypt = require('bcryptjs');
const ref = require('./refdata');

const q = v => v === null || v === undefined ? 'NULL' : typeof v === 'number' ? String(v) : `'${String(v).replace(/\\/g, '\\\\').replace(/'/g, "''")}'`;
const out = [];
const insert = (table, cols, rows) => {
  if (!rows.length) return;
  out.push(`INSERT INTO ${table} (${cols.join(', ')}) VALUES\n` + rows.map(r => '    (' + r.map(q).join(', ') + ')').join(',\n') + ';\n');
};
const hash = p => bcrypt.hashSync(p, 11);
const DEMO = { receptionist: 'Priem2026', master: 'Master2026', storekeeper: 'Sklad2026', client: 'Client2026' };

// ---------------- справочники ----------------
insert('roles', ['id', 'code', 'name'], ref.roles);
insert('order_statuses', ['id', 'code', 'name', 'color', 'sort_order', 'is_final'], ref.statuses);
insert('device_types', ['id', 'name'], ref.deviceTypes.map((n, i) => [i + 1, n]));
insert('completeness_items', ['id', 'name', 'kind'], ref.completeness.map((c, i) => [i + 1, c[0], c[1]]));
insert('services', ['id', 'name', 'price'], ref.services.map((s, i) => [i + 1, s[0], s[1]]));
const svc = name => { const i = ref.services.findIndex(s => s[0] === name); if (i < 0) throw new Error(name); return { id: i + 1, price: ref.services[i][1] }; };
const dtype = name => ref.deviceTypes.indexOf(name) + 1;
const comp = name => ref.completeness.findIndex(c => c[0] === name) + 1;
const status = code => ref.statuses.find(s => s[1] === code)[0];

// ---------------- пользователи ----------------
const users = [
  // id, role, last, first, middle, phone, email, password, created
  [1, 2, 'Соколова', 'Анна', 'Викторовна', '+79000000001', 'priem@servicedesk.ru', DEMO.receptionist, '2026-01-10 09:00:00'],
  [2, 2, 'Никитин', 'Павел', 'Олегович', '+79000000002', 'priem2@servicedesk.ru', DEMO.receptionist, '2026-01-10 09:05:00'],
  [3, 3, 'Орлов', 'Сергей', 'Викторович', '+79000000003', 'orlov@servicedesk.ru', DEMO.master, '2026-01-10 09:10:00'],
  [4, 3, 'Лебедев', 'Андрей', 'Игоревич', '+79000000004', 'lebedev@servicedesk.ru', DEMO.master, '2026-01-10 09:15:00'],
  [5, 3, 'Захаров', 'Илья', 'Романович', '+79000000005', 'zaharov@servicedesk.ru', DEMO.master, '2026-02-01 09:00:00'],
  [6, 4, 'Морозова', 'Елена', 'Сергеевна', '+79000000006', 'sklad@servicedesk.ru', DEMO.storekeeper, '2026-01-10 09:20:00'],
  [10, 1, 'Иванов', 'Кирилл', null, '+79001234567', 'ivanov.k@mail.ru', DEMO.client, '2026-05-12 11:20:00'],
  [11, 1, 'Смирнов', 'Алексей', 'Петрович', '+79005551122', 'smirnov.ap@yandex.ru', DEMO.client, '2026-09-02 10:05:00'],
  [12, 1, 'Петрова', 'Ольга', 'Ивановна', '+79161234001', 'petrova.oi@mail.ru', DEMO.client, '2026-09-01 12:40:00'],
  [13, 1, 'Кузнецов', 'Роман', 'Сергеевич', '+79161234002', 'kuznetsov.rs@gmail.com', DEMO.client, '2026-08-30 15:10:00'],
  [14, 1, 'Волкова', 'Екатерина', 'Михайловна', '+79161234003', 'volkova.em@mail.ru', DEMO.client, '2026-08-25 10:30:00'],
  [15, 1, 'Егоров', 'Тимур', 'Николаевич', '+79161234004', null, DEMO.client, '2026-08-20 16:00:00'],
  [16, 1, 'Фёдорова', 'Мария', 'Андреевна', '+79161234005', null, null, '2026-09-24 13:15:00'],
];
console.log('bcrypt…');
const hashes = {};
for (const p of Object.values(DEMO)) hashes[p] = hash(p);
insert('users', ['id', 'role_id', 'last_name', 'first_name', 'middle_name', 'phone', 'email', 'password_hash', 'created_at'],
  users.map(u => [u[0], u[1], u[2], u[3], u[4], u[5], u[6], u[7] ? hashes[u[7]] : null, u[8]]));

// ---------------- склад ----------------
// sku, name, price, initial qty, min, location
const partsDef = [
  ['PWR-ASUS-65W', 'Блок питания ASUS 65W 19V', 2000, 5, 2, 'A-01'],
  ['BAT-IPAD9', 'Аккумулятор iPad 9 (A2197)', 2400, 3, 1, 'A-02'],
  ['LCD-SGA54', 'Дисплей Samsung Galaxy A54 OLED', 5400, 2, 1, 'A-03'],
  ['LED-SAM55', 'Подсветка LED Samsung 55" (комплект)', 3800, 0, 1, 'B-01'],
  ['BRG-6205', 'Подшипник 6205-2RS', 450, 10, 4, 'C-01'],
  ['SEAL-3562', 'Сальник 35×62×10', 300, 8, 3, 'C-02'],
  ['TEN-1950', 'ТЭН 1950 Вт для стиральной машины', 1600, 2, 1, 'C-03'],
  ['KBD-X515', 'Клавиатура ASUS X515', 1900, 2, 1, 'A-04'],
  ['DCJ-ASUS', 'Разъём питания DC ASUS 4.5×3.0', 350, 12, 5, 'A-05'],
  ['PST-MX4', 'Термопаста Arctic MX-4, 4 г', 600, 6, 2, 'D-01'],
  ['SSD-512', 'SSD Kingston NV2 512 ГБ', 3900, 4, 2, 'D-02'],
  ['RAM-8D4', 'Модуль памяти DDR4 8 ГБ SO-DIMM', 2100, 5, 2, 'D-03'],
  ['BAT-RN12', 'Аккумулятор Xiaomi BN5E (Redmi Note 12)', 1500, 3, 1, 'A-06'],
  ['USBC-BRD', 'Плата зарядки с разъёмом USB Type-C', 700, 6, 2, 'A-07'],
  ['FAN-X515', 'Вентилятор охлаждения ASUS X515', 1300, 1, 2, 'A-08'],
  ['HDMI-PS5', 'Разъём HDMI для PlayStation 5', 900, 4, 1, 'B-02'],
  ['GLS-UNI', 'Защитное стекло универсальное', 300, 21, 5, 'D-04'],
  ['CAP-KIT', 'Набор электролитических конденсаторов', 400, 10, 3, 'D-05'],
  ['THP-1MM', 'Термопрокладки 1 мм (лист)', 350, 0, 3, 'D-06'],
];
const parts = partsDef.map((p, i) => ({ id: i + 1, sku: p[0], name: p[1], price: p[2], onHand: 0, reserved: 0, min: p[4], loc: p[5], init: p[3] }));
const part = sku => { const p = parts.find(x => x.sku === sku); if (!p) throw new Error(sku); return p; };
const movements = [];
const move = (p, orderId, type, qty, userId, at, comment) => {
  if (type === 'receipt' || type === 'inventory') p.onHand += qty;
  if (type === 'issue') p.onHand += qty; // qty отрицательное
  movements.push([p.id, orderId, type, qty, p.onHand, userId, at, comment]);
};
// начальное оприходование
parts.forEach(p => { if (p.init > 0) move(p, null, 'receipt', p.init, 6, '2026-08-01 10:00:00', 'Накладная № 184 от 01.08.2026, ООО «ЗапчастьТорг»'); });

// ---------------- заявки ----------------
const devices = [], orders = [], history = [], works = [], reservations = [], payments = [], docs = [], orderComp = [];
let devId = 0;
function order(o) {
  devId++;
  devices.push([devId, o.client, dtype(o.type), o.brand, o.model, o.serial || null, o.created]);
  const w = (o.works || []).map(x => ({ ...x, svc: x.svc ? svc(x.svc) : null }));
  let total = 0;
  w.forEach(x => { const price = x.price ?? x.svc.price; total += price; works.push([o.id, x.svc ? x.svc.id : null, o.master, x.desc || (x.svc && ref.services[x.svc.id - 1][0]), price, x.at]); });
  (o.parts || []).forEach(x => {
    const p = part(x.sku);
    total += p.price * x.qty;
    reservations.push({ order: o.id, p, qty: x.qty, status: x.status, by: o.master, at: x.at, issuedAt: x.issuedAt || null, issuedBy: x.issuedAt ? 6 : null });
  });
  if (o.warranty) total = 0;
  const last = o.history[o.history.length - 1];
  orders.push([o.id, o.client, devId, o.receptionist, o.master, status(last[0]), o.fault, o.note || null, o.diagnosis || null,
    o.warranty ? 1 : 0, o.warrantyMonths ?? null, o.decision || null, o.decisionAt || null, total.toFixed(2), o.due, o.created,
    o.readyAt || null, o.issuedAt || null, last[1]]);
  o.history.forEach(h => history.push([o.id, status(h[0]), h[2], h[1], h[3] || null]));
  (o.comp || []).forEach(c => orderComp.push([o.id, comp(c)]));
  (o.pay || []).forEach(p => payments.push([o.id, p.amount, p.method, p.by || o.receptionist, p.at]));
  docs.push([o.id, 'receipt', `КВ-${o.id}`, o.receptionist, o.created]);
  (o.docs || []).forEach(dd => docs.push([o.id, dd[0], `${{ estimate: 'СМ', act: 'АВР', issue: 'АВ' }[dd[0]]}-${o.id}`, dd[1], dd[2]]));
  return total;
}

order({ id: 9950, client: 10, type: 'Ноутбук', brand: 'Apple', model: 'MacBook Air M1', serial: 'C02FK1ABQ6L4', receptionist: 1, master: 4,
  fault: 'Повторный перегрев и шум вентилятора после ремонта', created: '2026-05-12 11:25:00', due: '2026-05-19', warranty: true, warrantyMonths: 3,
  diagnosis: 'Высохла термопаста после предыдущего обслуживания. Выполнена чистка и замена термоинтерфейса по гарантии.', comp: ['Зарядное устройство'],
  works: [{ svc: 'Чистка от пыли и замена термопасты', at: '2026-05-13 14:00:00' }],
  history: [['accepted', '2026-05-12 11:25:00', 1], ['diagnostics', '2026-05-12 15:10:00', 4], ['in_work', '2026-05-13 10:00:00', 4, 'Гарантийный случай'],
    ['ready', '2026-05-13 16:30:00', 4], ['issued', '2026-05-15 12:00:00', 1]], readyAt: '2026-05-13 16:30:00', issuedAt: '2026-05-15 12:00:00',
  docs: [['act', 4, '2026-05-13 16:30:00'], ['issue', 1, '2026-05-15 12:00:00']] });

order({ id: 10176, client: 10, type: 'Смартфон', brand: 'Samsung', model: 'Galaxy A54', serial: '356789112233445', receptionist: 2, master: 3,
  fault: 'Не заряжается, телефон не реагирует на кабель', created: '2026-08-11 12:10:00', due: '2026-08-18', warrantyMonths: 6,
  diagnosis: 'Выгорел контроллер питания и повреждена плата зарядки. Требуется пайка и замена платы зарядки.', decision: 'approved', decisionAt: '2026-08-12 09:40:00',
  comp: ['Сумка/чехол', 'SIM-карта'],
  works: [{ svc: 'Ремонт платы (пайка компонентов)', at: '2026-08-13 11:00:00' }],
  parts: [{ sku: 'USBC-BRD', qty: 1, status: 'issued', at: '2026-08-11 17:00:00', issuedAt: '2026-08-12 10:15:00' }],
  history: [['accepted', '2026-08-11 12:10:00', 2], ['diagnostics', '2026-08-11 15:00:00', 3], ['approval', '2026-08-11 17:05:00', 3],
    ['in_work', '2026-08-12 09:40:00', 10, 'Клиент согласовал стоимость'], ['ready', '2026-08-13 17:20:00', 3], ['issued', '2026-08-14 18:05:00', 2]],
  readyAt: '2026-08-13 17:20:00', issuedAt: '2026-08-14 18:05:00', pay: [{ amount: 3200, method: 'cash', at: '2026-08-14 18:00:00' }],
  docs: [['estimate', 3, '2026-08-11 17:05:00'], ['act', 3, '2026-08-13 17:20:00'], ['issue', 2, '2026-08-14 18:05:00']] });

order({ id: 10184, client: 15, type: 'Планшет', brand: 'Apple', model: 'iPad 9', serial: 'DMPXK2ABQ1GC', receptionist: 1, master: 4,
  fault: 'Быстро разряжается, выключается на 30 %', created: '2026-08-20 16:05:00', due: '2026-08-27', warrantyMonths: 6,
  diagnosis: 'Износ аккумулятора 38 %. Требуется замена аккумулятора.', decision: 'approved', decisionAt: '2026-08-21 10:00:00', comp: ['Зарядное устройство', 'Сумка/чехол'],
  works: [{ svc: 'Замена аккумулятора', at: '2026-08-22 12:00:00' }],
  parts: [{ sku: 'BAT-IPAD9', qty: 1, status: 'issued', at: '2026-08-21 09:00:00', issuedAt: '2026-08-21 11:00:00' }],
  history: [['accepted', '2026-08-20 16:05:00', 1], ['diagnostics', '2026-08-21 08:30:00', 4], ['approval', '2026-08-21 09:05:00', 4],
    ['in_work', '2026-08-21 10:00:00', 15, 'Клиент согласовал стоимость'], ['ready', '2026-08-22 13:00:00', 4], ['issued', '2026-08-24 11:30:00', 1]],
  readyAt: '2026-08-22 13:00:00', issuedAt: '2026-08-24 11:30:00', pay: [{ amount: 3400, method: 'card', at: '2026-08-24 11:25:00' }],
  docs: [['estimate', 4, '2026-08-21 09:05:00'], ['act', 4, '2026-08-22 13:00:00'], ['issue', 1, '2026-08-24 11:30:00']] });

order({ id: 10198, client: 14, type: 'Смартфон', brand: 'Samsung', model: 'Galaxy A54', serial: '356789998877665', receptionist: 2, master: 3,
  fault: 'Разбит экран после падения, сенсор не работает', created: '2026-08-25 10:35:00', due: '2026-09-01', warrantyMonths: 6,
  note: 'Трещины по всей площади экрана', diagnosis: 'Повреждён дисплейный модуль. Требуется замена дисплея.', decision: 'approved', decisionAt: '2026-08-25 18:20:00',
  comp: ['Сумка/чехол', 'Трещина экрана'],
  works: [{ svc: 'Замена экрана смартфона', at: '2026-09-23 12:00:00' }],
  parts: [{ sku: 'LCD-SGA54', qty: 1, status: 'issued', at: '2026-08-25 16:00:00', issuedAt: '2026-09-22 10:00:00' }],
  history: [['accepted', '2026-08-25 10:35:00', 2], ['diagnostics', '2026-08-25 14:00:00', 3], ['approval', '2026-08-25 16:05:00', 3],
    ['in_work', '2026-08-25 18:20:00', 14, 'Клиент согласовал стоимость'], ['ready', '2026-09-23 15:40:00', 3]],
  readyAt: '2026-09-23 15:40:00', docs: [['estimate', 3, '2026-08-25 16:05:00'], ['act', 3, '2026-09-23 15:40:00']] });

order({ id: 10222, client: 15, type: 'Системный блок', brand: 'DEXP', model: 'Aquilon O286', receptionist: 1, master: 3,
  fault: 'Не загружается Windows, щелчки жёсткого диска', created: '2026-09-03 11:00:00', due: '2026-09-10', warrantyMonths: 3,
  diagnosis: 'Неисправен HDD. Замена накопителя на SSD и установка ОС.', decision: 'approved', decisionAt: '2026-09-04 10:00:00',
  works: [{ svc: 'Установка ОС и драйверов', at: '2026-09-08 15:00:00' }],
  parts: [{ sku: 'SSD-512', qty: 1, status: 'issued', at: '2026-09-03 16:00:00', issuedAt: '2026-09-04 11:00:00' }],
  history: [['accepted', '2026-09-03 11:00:00', 1], ['diagnostics', '2026-09-03 14:00:00', 3], ['approval', '2026-09-03 16:05:00', 3],
    ['in_work', '2026-09-04 10:00:00', 15, 'Клиент согласовал стоимость']], docs: [['estimate', 3, '2026-09-03 16:05:00']] });

order({ id: 10225, client: 13, type: 'Телевизор', brand: 'Samsung', model: 'UE55AU7100', serial: '0B3K3HCR600123', receptionist: 2, master: 5,
  fault: 'Есть звук, нет изображения', created: '2026-08-30 15:15:00', due: '2026-09-30', note: 'Без пульта, царапина на рамке', comp: ['Царапины на корпусе'],
  diagnosis: 'Вышла из строя LED-подсветка матрицы. Требуется замена комплекта подсветки.', decision: 'approved', decisionAt: '2026-08-31 12:00:00', warrantyMonths: 6,
  works: [{ svc: 'Замена подсветки телевизора', at: '2026-08-31 10:00:00' }],
  parts: [{ sku: 'LED-SAM55', qty: 1, status: 'requested', at: '2026-08-31 10:30:00' }],
  history: [['accepted', '2026-08-30 15:15:00', 2], ['diagnostics', '2026-08-31 09:00:00', 5], ['approval', '2026-08-31 10:35:00', 5],
    ['waiting_parts', '2026-08-31 12:00:00', 13, 'Клиент согласовал; подсветка заказана у поставщика']], docs: [['estimate', 5, '2026-08-31 10:35:00']] });

order({ id: 10229, client: 12, type: 'Стиральная машина', brand: 'LG', model: 'F2J3NS1W', serial: '309KWSB1A234', receptionist: 1, master: 4,
  fault: 'Сильный гул при отжиме, течёт вода снизу', created: '2026-09-01 12:45:00', due: '2026-09-29', warrantyMonths: 12,
  diagnosis: 'Износ подшипников барабана и сальника. Требуется замена подшипников и сальника.', decision: 'approved', decisionAt: '2026-09-02 11:00:00',
  works: [{ svc: 'Замена подшипника барабана', at: '2026-09-24 12:00:00' }],
  parts: [{ sku: 'BRG-6205', qty: 2, status: 'issued', at: '2026-09-02 09:00:00', issuedAt: '2026-09-24 10:00:00' },
    { sku: 'SEAL-3562', qty: 1, status: 'reserved', at: '2026-09-02 09:05:00' }],
  history: [['accepted', '2026-09-01 12:45:00', 1], ['diagnostics', '2026-09-01 16:00:00', 4], ['approval', '2026-09-02 09:10:00', 4],
    ['in_work', '2026-09-02 11:00:00', 12, 'Клиент согласовал стоимость']], docs: [['estimate', 4, '2026-09-02 09:10:00']] });

order({ id: 10231, client: 11, type: 'Ноутбук', brand: 'ASUS', model: 'X515', serial: 'M3NRCV12A345', receptionist: 1, master: 3,
  fault: 'Не включается, индикатор питания не горит', created: '2026-09-02 10:10:00', due: '2026-10-02', comp: ['Зарядное устройство', 'Царапины на крышке'],
  history: [['accepted', '2026-09-02 10:10:00', 1], ['diagnostics', '2026-09-24 10:00:00', 3]] });

order({ id: 10233, client: 16, type: 'Смартфон', brand: 'Xiaomi', model: 'Redmi Note 12', serial: '861234050112233', receptionist: 2, master: 3,
  fault: 'Не держит заряд, вздулся аккумулятор', created: '2026-09-24 13:20:00', due: '2026-10-01', comp: ['Сумка/чехол'],
  history: [['accepted', '2026-09-24 13:20:00', 2]] });

order({ id: 10236, client: 10, type: 'Ноутбук', brand: 'ASUS', model: 'VivoBook 15 X515', serial: 'M3NRCV55B777', receptionist: 1, master: 3,
  fault: 'Не включается, не реагирует на кнопку питания', created: '2026-09-22 11:40:00', due: '2026-09-30', warrantyMonths: 6, comp: ['Зарядное устройство', 'Сумка/чехол'],
  diagnosis: 'Неисправен блок питания, требуется замена. Гарантия 6 мес.',
  works: [{ svc: 'Замена блока питания', at: '2026-09-23 12:00:00' }],
  parts: [{ sku: 'PWR-ASUS-65W', qty: 1, status: 'reserved', at: '2026-09-23 12:05:00' }],
  history: [['accepted', '2026-09-22 11:40:00', 1], ['diagnostics', '2026-09-23 09:30:00', 3], ['approval', '2026-09-23 12:10:00', 3, 'Смета отправлена клиенту']],
  docs: [['estimate', 3, '2026-09-23 12:10:00']] });

order({ id: 10237, client: 13, type: 'Игровая приставка', brand: 'Sony', model: 'PlayStation 5', serial: 'E23456789A', receptionist: 2, master: 5,
  fault: 'Нет изображения на телевизоре, разъём HDMI шатается', created: '2026-09-25 10:20:00', due: '2026-10-02', comp: ['Кабель USB'],
  history: [['accepted', '2026-09-25 10:20:00', 2]] });

// движения по резервам — в хронологическом порядке
const events = [];
reservations.forEach(r => {
  if (r.status !== 'requested') events.push({ at: r.at, kind: 'reserve', r });
  if (r.status === 'issued') events.push({ at: r.issuedAt, kind: 'issue', r });
});
events.sort((a, b) => a.at.localeCompare(b.at));
// оприходование подсветки после запроса — ещё не пришла, запрос остаётся «requested»
for (const e of events) {
  const { r } = e;
  if (e.kind === 'reserve') { r.p.reserved += r.qty; move(r.p, r.order, 'reserve', 0, r.by, r.at, `Резерв ${r.qty} шт. под заявку № ${r.order}`); }
  else { r.p.reserved -= r.qty; move(r.p, r.order, 'issue', -r.qty, 6, r.issuedAt, `Выдано мастеру под заявку № ${r.order}`); }
}
// инвентаризация 20.09.2026: недостача 1 защитного стекла
const glass = part('GLS-UNI');
const invItems = [[1, glass.id, glass.onHand, glass.onHand - 1], [1, part('CAP-KIT').id, part('CAP-KIT').onHand, part('CAP-KIT').onHand], [1, part('DCJ-ASUS').id, part('DCJ-ASUS').onHand, part('DCJ-ASUS').onHand]];
move(glass, null, 'inventory', -1, 6, '2026-09-20 18:00:00', 'Инвентаризация № 1: недостача');

// проверка инвариантов
parts.forEach(p => { if (p.onHand < 0 || p.reserved < 0 || p.reserved > p.onHand) throw new Error('bad stock ' + p.sku); });

insert('parts', ['id', 'sku', 'name', 'price', 'quantity_on_hand', 'quantity_reserved', 'min_quantity', 'location', 'created_at'],
  parts.map(p => [p.id, p.sku, p.name, p.price, p.onHand, p.reserved, p.min, p.loc, '2026-08-01 09:00:00']));
insert('devices', ['id', 'client_id', 'device_type_id', 'brand', 'model', 'serial_number', 'created_at'], devices);
insert('orders', ['id', 'client_id', 'device_id', 'receptionist_id', 'master_id', 'status_id', 'declared_fault', 'appearance_note', 'diagnosis',
  'is_warranty', 'warranty_months', 'client_decision', 'decision_at', 'total_cost', 'due_date', 'created_at', 'ready_at', 'issued_at', 'updated_at'], orders);
insert('order_completeness', ['order_id', 'item_id'], orderComp);
insert('order_status_history', ['order_id', 'status_id', 'changed_by', 'changed_at', 'comment'], history);
insert('order_works', ['order_id', 'service_id', 'master_id', 'description', 'price', 'created_at'], works);
insert('part_reservations', ['order_id', 'part_id', 'quantity', 'price', 'status', 'requested_by', 'issued_by', 'created_at', 'issued_at'],
  reservations.map(r => [r.order, r.p.id, r.qty, r.p.price, r.status, r.by, r.issuedBy, r.at, r.issuedAt]));
movements.sort((a, b) => a[6].localeCompare(b[6]));
insert('part_movements', ['part_id', 'order_id', 'movement_type', 'quantity', 'balance_after', 'user_id', 'created_at', 'comment'], movements);
insert('inventories', ['id', 'created_by', 'created_at', 'comment', 'discrepancies'], [[1, 6, '2026-09-20 18:00:00', 'Плановая инвентаризация стеллажей A–D (выборочно)', 1]]);
insert('inventory_items', ['inventory_id', 'part_id', 'expected_qty', 'actual_qty'], invItems);
insert('payments', ['order_id', 'amount', 'method', 'received_by', 'paid_at'], payments);
insert('documents', ['order_id', 'doc_type', 'number', 'created_by', 'created_at'], docs);

const header = `-- =====================================================================
--  ИС «СервисДеск» — справочные и демонстрационные данные
--  Сгенерировано tools/generate_seed.js (не редактировать вручную)
--  Демо-пароли: приёмщик ${DEMO.receptionist}, мастер ${DEMO.master}, кладовщик ${DEMO.storekeeper}, клиент ${DEMO.client}
-- =====================================================================

USE servicedesk;
SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

`;
const file = path.join(__dirname, '..', 'db', '02_seed.sql');
fs.writeFileSync(file, header + out.join('\n') + '\nSET FOREIGN_KEY_CHECKS = 1;\n', 'utf8');
console.log('written', file, 'orders:', orders.length, 'movements:', movements.length);
orders.forEach(o => console.log(o[0], 'status', o[5], 'total', o[13]));
