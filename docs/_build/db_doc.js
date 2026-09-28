// 2. Документ «Структура базы данных» — описание таблиц строится из information_schema (schema.json)
const fs = require('fs');
const path = require('path');
const { Doc, AlignmentType } = require('./lib/gost');
const { academicTitle } = require('./lib/common');
const ref = require('../../tools/refdata');

const schema = JSON.parse(fs.readFileSync(path.join(__dirname, 'schema.json'), 'utf8'));
const img = f => path.join(__dirname, 'img', f);
const sqlText = fs.readFileSync(path.join(__dirname, '..', '..', 'db', '01_schema.sql'), 'utf8');

const ORDER = ['roles', 'users', 'device_types', 'devices', 'order_statuses', 'orders', 'completeness_items', 'order_completeness',
  'order_status_history', 'services', 'order_works', 'parts', 'part_reservations', 'part_movements', 'inventories',
  'inventory_items', 'payments', 'documents'];
const table = n => schema.tables.find(t => t.name === n);

const PURPOSE = {
  roles: 'Справочник ролей пользователей. Роль определяет набор доступных функций и проверяется сервером при каждом запросе.',
  users: 'Единая таблица учётных записей клиентов и сотрудников. Логином служит номер телефона или e-mail. Клиент, которого оформил приёмщик, создаётся без пароля и может позже зарегистрироваться в личном кабинете по тому же номеру телефона.',
  device_types: 'Справочник типов принимаемой техники.',
  devices: 'Устройства клиентов. Одно устройство может поступать в ремонт повторно, поэтому оно вынесено в отдельную таблицу и связано с несколькими заявками.',
  order_statuses: 'Справочник статусов жизненного цикла заявки с цветом отображения по руководству по стилю.',
  orders: 'Центральная таблица системы — заявки на ремонт. Номер заявки формируется автоинкрементом начиная с 10000.',
  completeness_items: 'Справочник пунктов чек-листа комплектности и внешнего вида при приёме устройства.',
  order_completeness: 'Связующая таблица «многие ко многим» между заявками и пунктами комплектности.',
  order_status_history: 'Журнал смены статусов. Обеспечивает прослеживаемость заявки: фиксирует, кто, когда и с каким комментарием изменил статус.',
  services: 'Прайс-лист типовых работ сервисного центра.',
  order_works: 'Журнал работ по заявке. Стоимость работы копируется из прайс-листа и может быть изменена мастером.',
  parts: 'Номенклатура запчастей с текущими остатками. Физический и зарезервированный остатки хранятся раздельно.',
  part_reservations: 'Запросы мастеров и резервы запчастей под заявки. Цена фиксируется на момент резервирования.',
  part_movements: 'Журнал всех операций со складом: приход, резерв, снятие резерва, выдача (списание), корректировка при инвентаризации.',
  inventories: 'Заголовки инвентаризационных ведомостей.',
  inventory_items: 'Строки инвентаризации: учётный и фактический остаток, расхождение вычисляется СУБД автоматически.',
  payments: 'Оплаты по заявкам. Допускается несколько частичных оплат по одной заявке.',
  documents: 'Реестр сформированных документов. На каждую заявку создаётся не более одного документа каждого типа.',
};

const fkSet = new Set(schema.fks.map(f => `${f.table}.${f.column}`));
function keyOf(t, c) {
  const k = [];
  if (c.key === 'PRI') k.push('PK');
  if (fkSet.has(`${t}.${c.name}`)) k.push('FK');
  if (c.key === 'UNI') k.push('UQ');
  if (c.extra.includes('auto_increment')) k.push('AI');
  return k.join(', ') || '';
}
function typeOf(c) {
  return c.type.toUpperCase().replace(/','/g, "', '");
}
function defOf(c) {
  if (c.extra.includes('GENERATED') && !c.extra.includes('DEFAULT_GENERATED')) return 'вычисляемое';
  if (c.default === 'NONE') return '—';
  if (c.extra.includes('on update')) return 'CURRENT_TIMESTAMP, при изменении';
  return c.default;
}

const d = new Doc({ title: 'Структура базы данных — ИС «СервисДеск»', subject: 'Описание структуры базы данных ИС «СервисДеск»' });
d.titlePage(academicTitle('Структура базы данных', 'Описание логической и физической модели данных'));
d.toc();

// 1 ---------------------------------------------------------------
d.h1('1 Общие сведения о базе данных');
d.p('База данных ИС «СервисДеск» хранит сведения о клиентах, сотрудниках, устройствах, заявках на ремонт, выполненных работах, запчастях и их движении, оплатах и сформированных документах. Доступ к данным выполняет только серверная часть (ASP.NET Core Web API) через Entity Framework Core; клиентские приложения к БД напрямую не подключаются.');
d.table('Параметры базы данных', [{ title: 'Параметр', w: 60 }, { title: 'Значение', w: 105 }], [
  ['СУБД', 'MySQL 8.0 (Community Server)'],
  ['Имя базы данных', 'servicedesk'],
  ['Подсистема хранения', 'InnoDB (транзакции, внешние ключи, блокировки на уровне строк)'],
  ['Кодировка и сравнение', 'utf8mb4 / utf8mb4_unicode_ci'],
  ['Количество таблиц', `${schema.tables.length} таблиц и 2 представления`],
  ['Количество внешних ключей', String(schema.fks.length)],
  ['Скрипты', 'db/01_schema.sql — создание структуры; db/02_seed.sql — справочные и демонстрационные данные'],
  ['Учётная запись приложения', 'servicedesk — права только на базу servicedesk'],
  ['Развёртывание', 'контейнер mysql:8.0, скрипты выполняются автоматически при первом запуске (docker-entrypoint-initdb.d)'],
]);
d.p('При проектировании приняты следующие соглашения об именовании:');
d.list([
  'имена таблиц и полей — латиницей в нижнем регистре через подчёркивание (snake_case), таблицы — во множественном числе;',
  'первичный ключ каждой таблицы — поле `id`; внешний ключ — `<сущность>_id` или `<действие>_by` для ссылок на сотрудника;',
  'имена ограничений: `pk_` — первичный ключ, `fk_` — внешний, `uq_` — уникальность, `ck_` — проверка, `ix_` — индекс;',
  'дата и время хранятся в типе DATETIME, денежные суммы — в DECIMAL(10,2), логические признаки — в TINYINT(1);',
  'перечислимые значения с фиксированным набором (способ оплаты, тип документа, тип операции склада) — в типе ENUM.',
]);

// 2 ---------------------------------------------------------------
d.h1('2 Инфологическая модель');
d.h2('2.1 Сущности предметной области');
d.p(`Предметная область разбита на пять групп сущностей, соответствующих подсистемам ИС. Перечень сущностей приведён в таблице ${d.nextTabRef()}.`);
const GROUP = {
  'Учётные записи': ['roles', 'users'],
  'Приём заявок': ['device_types', 'devices', 'order_statuses', 'orders', 'completeness_items', 'order_completeness'],
  'Управление ремонтом': ['order_status_history', 'services', 'order_works'],
  'Склад запчастей': ['parts', 'part_reservations', 'part_movements', 'inventories', 'inventory_items'],
  'Расчёты и документы': ['payments', 'documents'],
};
const rows = [];
for (const [g, ts] of Object.entries(GROUP)) ts.forEach((t, i) => rows.push([i === 0 ? g : '', table(t).comment, t]));
d.table('Сущности и таблицы', [{ title: 'Подсистема', w: 42 }, { title: 'Сущность', w: 70 }, { title: 'Таблица', w: 53 }], rows, { size: 22 });
d.h2('2.2 Связи между сущностями');
d.list([
  'Роль — Пользователь (1:M): каждому пользователю назначена ровно одна роль.',
  'Клиент — Устройство (1:M) и Клиент — Заявка (1:M): клиент может сдавать несколько устройств и обращаться повторно.',
  'Устройство — Заявка (1:M): одно устройство может несколько раз поступать в ремонт.',
  'Сотрудник — Заявка: приёмщик оформляет заявку (1:M), мастер назначается на заявку (0..1:M).',
  'Статус — Заявка (1:M) и Заявка — История статусов (1:M): каждая смена статуса добавляет запись в историю.',
  'Заявка — Пункт комплектности (M:N) через связующую таблицу order_completeness.',
  'Заявка — Работа (1:M), Услуга прайс-листа — Работа (0..1:M).',
  'Заявка — Резерв запчасти (1:M), Запчасть — Резерв (1:M), Запчасть — Движение (1:M).',
  'Инвентаризация — Строка инвентаризации (1:M), Запчасть — Строка инвентаризации (1:M).',
  'Заявка — Оплата (1:M), Заявка — Документ (1:M, не более одного документа каждого типа).',
]);

// 3 ---------------------------------------------------------------
d.h1('3 Логическая модель (ER-диаграмма)');
d.p(`Общая ER-диаграмма базы данных приведена на рисунке ${d.nextFigRef()}. Таблицы сгруппированы по подсистемам; для компактности показаны только ключевые поля.`);
d.p('Обозначения: PK — первичный ключ, FK — внешний ключ, «?» — поле допускает NULL; связь «один ко многим» изображается линией с поперечными чертами со стороны родительской таблицы и «вороньей лапкой» со стороны дочерней. Ссылки на сотрудников (changed_by, master_id и др.) на общей диаграмме не показаны и приведены на фрагментах.', { spacing: { after: 120 } });
d.figure(img('er_overview.png'), 'ER-диаграмма базы данных (общий вид)', { maxHeightMm: 215 });
d.p('Полный состав полей по подсистемам приведён на рисунках 2–5 (штриховые линии — ссылки на учётную запись сотрудника).');
d.figure(img('er_intake.png'), 'Фрагмент ER-диаграммы: учётные записи и приём заявок');
d.figure(img('er_repair.png'), 'Фрагмент ER-диаграммы: управление ремонтом', { widthMm: 150 });
d.figure(img('er_stock.png'), 'Фрагмент ER-диаграммы: склад запчастей', { maxHeightMm: 215 });
d.figure(img('er_pay.png'), 'Фрагмент ER-диаграммы: расчёты и документы', { widthMm: 150 });

// 4 ---------------------------------------------------------------
d.h1('4 Физическая модель: описание таблиц');
d.p('Для каждой таблицы приведены поля, типы данных, допустимость NULL, ключи (PK — первичный, FK — внешний, UQ — уникальный, AI — автоинкремент), значения по умолчанию и назначение.');
ORDER.forEach((name, i) => {
  const t = table(name);
  d.h2(`4.${i + 1} Таблица ${name} — ${t.comment}`);
  d.p(PURPOSE[name]);
  d.table(`Структура таблицы ${name}`, [
    { title: 'Поле', w: 33 }, { title: 'Тип', w: 30 }, { title: 'NULL', w: 12, align: AlignmentType.CENTER },
    { title: 'Ключ', w: 15, align: AlignmentType.CENTER }, { title: 'По умолч.', w: 22 }, { title: 'Описание', w: 53 },
  ], t.columns.map(c => [c.name, typeOf(c), c.nullable === 'YES' ? 'да' : 'нет', keyOf(name, c), defOf(c), c.comment]), { size: 20 });
});

// 5 ---------------------------------------------------------------
d.h1('5 Связи и ссылочная целостность');
d.p(`Все связи реализованы внешними ключами InnoDB (таблица ${d.nextTabRef()}). Правило RESTRICT запрещает удалять запись, на которую есть ссылки (например, клиента с заявками); CASCADE удаляет зависимые строки вместе с заявкой; SET NULL сохраняет запись журнала движения при удалении заявки.`);
const onDel = r => r === 'CASCADE' ? 'CASCADE' : r === 'SET NULL' ? 'SET NULL' : 'RESTRICT';
d.table('Внешние ключи', [
  { title: 'Ограничение', w: 38 }, { title: 'Дочерняя таблица.поле', w: 52 }, { title: 'Родительская таблица', w: 38 },
  { title: 'Тип', w: 17, align: AlignmentType.CENTER }, { title: 'ON DELETE', w: 20, align: AlignmentType.CENTER },
], schema.fks.map(f => {
  const col = table(f.table).columns.find(c => c.name === f.column);
  return [f.name, `${f.table}.${f.column}`, `${f.ref_table}.${f.ref_column}`, col.nullable === 'YES' ? '0..1:M' : '1:M', onDel(f.on_delete)];
}), { size: 20 });

// 6 ---------------------------------------------------------------
d.h1('6 Ограничения целостности и индексы');
d.h2('6.1 Ограничения CHECK');
d.p('Ограничения CHECK проверяются СУБД при каждой вставке и изменении и не позволяют записать некорректные значения даже при ошибке в прикладном коде.');
const CHECK_DESC = {
  ck_users_failed: 'Счётчик неудачных входов неотрицателен',
  ck_orders_total: 'Стоимость заявки неотрицательна',
  ck_orders_warranty: 'Гарантийный срок от 0 до 36 месяцев',
  ck_services_price: 'Цена услуги неотрицательна',
  ck_ow_price: 'Стоимость работы неотрицательна',
  ck_parts_qty: 'Остаток на складе не может быть отрицательным',
  ck_parts_reserved: 'Резерв неотрицателен и не превышает физический остаток',
  ck_parts_price: 'Цена запчасти неотрицательна',
  ck_pr_qty: 'Количество в резерве больше нуля',
  ck_ii_actual: 'Фактический остаток неотрицателен',
  ck_pay_amount: 'Сумма оплаты больше нуля',
};
d.table('Ограничения CHECK', [{ title: 'Ограничение', w: 36 }, { title: 'Таблица', w: 30 }, { title: 'Условие', w: 52 }, { title: 'Смысл', w: 47 }],
  schema.checks.map(c => [c.name, c.table, c.clause.replace(/`/g, ''), CHECK_DESC[c.name] || '']), { size: 20 });
d.h2('6.2 Ограничения уникальности и индексы');
d.p('Помимо первичных ключей созданы уникальные ограничения и индексы для ускорения типовых запросов: поиска клиента по телефону, отбора заявок по статусу и дате, получения истории заявки и журнала движения запчасти. Для каждого внешнего ключа InnoDB автоматически создаёт индекс.');
const idx = schema.indexes.filter(i => i.name !== 'PRIMARY' && (i.name.startsWith('uq_') || i.name.startsWith('ix_')));
d.table('Уникальные ограничения и индексы', [{ title: 'Таблица', w: 40 }, { title: 'Индекс', w: 50 }, { title: 'Уникальный', w: 25, align: AlignmentType.CENTER }, { title: 'Поля', w: 50 }],
  idx.map(i => [i.table, i.name, i.unique, i.columns]), { size: 20 });

// 7 ---------------------------------------------------------------
d.h1('7 Представления');
d.p('Для отчётов и быстрого просмотра созданы два представления.');
d.h2('7.1 v_orders_overview — сводка по заявкам');
d.p('Объединяет заявку с клиентом, устройством, статусом и мастером, рассчитывает сумму оплат и признак просрочки. Используется для отчётов и контроля сроков.');
const viewSql = name => {
  const m = sqlText.match(new RegExp(`CREATE VIEW ${name} AS[\\s\\S]*?;`));
  return m ? m[0].split('\n') : [];
};
d.code(viewSql('v_orders_overview'));
d.h2('7.2 v_parts_stock — остатки запчастей');
d.p('Показывает физический, зарезервированный и доступный остаток по каждой активной позиции и признак снижения ниже неснижаемого остатка.');
d.code(viewSql('v_parts_stock'));

// 8 ---------------------------------------------------------------
d.h1('8 Нормализация');
d.p('Схема базы данных приведена к третьей нормальной форме (3НФ):');
d.list([
  '**1НФ** — все поля атомарны: ФИО разделено на фамилию, имя и отчество; комплектность при приёме хранится не списком в строке, а отдельными записями в таблице order_completeness; повторяющиеся группы (работы, запчасти, оплаты, смены статусов) вынесены в отдельные таблицы;',
  '**2НФ** — в таблицах с составным ключом (order_completeness) нет неключевых атрибутов, зависящих от части ключа; у остальных таблиц первичный ключ простой;',
  '**3НФ** — отсутствуют транзитивные зависимости: название и цвет статуса хранятся в справочнике order_statuses, тип устройства — в device_types, данные клиента — в users, а не дублируются в заявке.',
]);
d.p('Осознанно допущены следующие отступления, обоснованные требованиями к производительности и хранению истории:');
d.list([
  '`orders.total_cost` — итог сметы хранится в заявке, чтобы список заявок и отчёты не пересчитывали суммы по журналу работ и резервам. Значение пересчитывает сервер при каждом изменении работ или запчастей;',
  '`parts.quantity_on_hand` и `parts.quantity_reserved` — текущие остатки хранятся в карточке запчасти для мгновенного контроля доступности; полная история изменений ведётся в part_movements, где поле balance_after позволяет проверить корректность остатка;',
  '`part_reservations.price` и `order_works.price` — цена фиксируется на момент операции, чтобы последующее изменение прайс-листа не меняло стоимость уже согласованных заявок. Это историческое значение, а не дублирование;',
  '`inventory_items.difference` — вычисляемое (GENERATED STORED) поле, рассчитывается самой СУБД и не может разойтись с исходными данными.',
]);

// 9 ---------------------------------------------------------------
d.h1('9 Справочные данные');
d.p('При развёртывании скрипт 02_seed.sql заполняет справочники начальными значениями.');
d.table('Роли пользователей (roles)', [{ title: 'id', w: 15, align: AlignmentType.CENTER }, { title: 'code', w: 55 }, { title: 'name', w: 95 }], ref.roles.map(r => r.map(String)));
d.table('Статусы заявок (order_statuses)', [
  { title: 'id', w: 10, align: AlignmentType.CENTER }, { title: 'code', w: 35 }, { title: 'name', w: 55 },
  { title: 'color', w: 25, align: AlignmentType.CENTER }, { title: 'sort_order', w: 20, align: AlignmentType.CENTER }, { title: 'is_final', w: 20, align: AlignmentType.CENTER },
], ref.statuses.map(r => r.map(String)), { rowShading: (ri, ci, r) => (ci === 3 ? r[3].slice(1) : null) });
d.table('Типы устройств (device_types)', [{ title: '№', w: 15, align: AlignmentType.CENTER }, { title: 'name', w: 150 }], ref.deviceTypes.map((n, i) => [String(i + 1), n]), { size: 22 });
d.table('Пункты комплектности (completeness_items)', [{ title: '№', w: 15, align: AlignmentType.CENTER }, { title: 'name', w: 90 }, { title: 'kind', w: 60 }],
  ref.completeness.map((c, i) => [String(i + 1), c[0], c[1] === 'accessory' ? 'accessory — комплектующее' : 'appearance — внешний вид']), { size: 22 });
d.table('Прайс-лист работ (services)', [{ title: '№', w: 15, align: AlignmentType.CENTER }, { title: 'name', w: 110 }, { title: 'price, руб.', w: 40, align: AlignmentType.RIGHT }],
  ref.services.map((s, i) => [String(i + 1), s[0], s[1].toLocaleString('ru-RU') + ',00']), { size: 22 });

// 10 --------------------------------------------------------------
d.h1('10 Типовые запросы');
d.p('Ниже приведены примеры запросов, которые выполняет серверная часть (в приложении они формируются средствами Entity Framework Core).');
d.h3('Активные заявки мастера с признаком просрочки');
d.code(`SELECT o.id, CONCAT(dt.name, ' ', d.brand, ' ', d.model) AS device,
       s.name AS status, o.due_date, o.due_date < CURRENT_DATE AS overdue
FROM orders o
  JOIN devices d        ON d.id = o.device_id
  JOIN device_types dt  ON dt.id = d.device_type_id
  JOIN order_statuses s ON s.id = o.status_id
WHERE o.master_id = @master_id AND s.is_final = 0 AND s.code <> 'cancelled'
ORDER BY o.due_date;`);
d.h3('Полная история заявки');
d.code(`SELECT h.changed_at, s.name AS status,
       CONCAT(u.last_name, ' ', u.first_name) AS changed_by, h.comment
FROM order_status_history h
  JOIN order_statuses s ON s.id = h.status_id
  JOIN users u          ON u.id = h.changed_by
WHERE h.order_id = @order_id
ORDER BY h.changed_at;`);
d.h3('Резервирование запчасти под заявку (транзакция)');
d.code(`START TRANSACTION;
SELECT quantity_on_hand - quantity_reserved INTO @available
FROM parts WHERE id = @part_id FOR UPDATE;          -- блокировка строки
-- при @available >= @qty:
UPDATE parts SET quantity_reserved = quantity_reserved + @qty WHERE id = @part_id;
INSERT INTO part_reservations (order_id, part_id, quantity, price, status, requested_by)
SELECT @order_id, id, @qty, price, 'reserved', @master_id FROM parts WHERE id = @part_id;
INSERT INTO part_movements (part_id, order_id, movement_type, quantity, balance_after, user_id)
SELECT id, @order_id, 'reserve', 0, quantity_on_hand, @master_id FROM parts WHERE id = @part_id;
COMMIT;`);
d.h3('Запчасти ниже неснижаемого остатка');
d.code(`SELECT sku, name, quantity_available, min_quantity
FROM v_parts_stock
WHERE below_minimum = 1
ORDER BY name;`);
d.h3('Выручка по способам оплаты за месяц');
d.code(`SELECT method, COUNT(*) AS payments, SUM(amount) AS total
FROM payments
WHERE paid_at >= '2026-10-01' AND paid_at < '2026-11-01'
GROUP BY method;`);

// 11 --------------------------------------------------------------
d.h1('11 Оценка объёма данных');
d.p('Оценка выполнена для объекта автоматизации из технического задания (до 60 новых заявок в день, около 300 рабочих дней в году).');
d.table('Прогноз количества записей', [{ title: 'Таблица', w: 55 }, { title: 'Записей в год', w: 40, align: AlignmentType.RIGHT }, { title: 'За 5 лет', w: 40, align: AlignmentType.RIGHT }, { title: 'Основание', w: 30 }], [
  ['orders, devices', '18 000', '90 000', '60 заявок/день'],
  ['order_status_history', '108 000', '540 000', '≈6 смен статуса'],
  ['order_works', '36 000', '180 000', '≈2 работы'],
  ['part_reservations', '27 000', '135 000', '≈1,5 детали'],
  ['part_movements', '60 000', '300 000', 'резерв, выдача, приход'],
  ['payments, documents', '90 000', '450 000', '1–2 оплаты, 3–4 документа'],
  ['users (клиенты)', '10 000', '50 000', 'новые клиенты'],
], { size: 22 });
d.p('Суммарный объём данных за 5 лет не превышает 1 ГБ, что позволяет хранить полную историю заявок в соответствии с требованием ТЗ без архивирования.');

// 12 --------------------------------------------------------------
d.h1('12 Резервное копирование и восстановление');
d.p('В составе docker-compose работает служба backup, которая ежедневно выгружает базу данных утилитой mysqldump в каталог backups и удаляет копии старше 30 дней. Резервную копию можно создать и вручную:');
d.code(`docker compose exec db sh -c "mysqldump -uroot -p\\"$MYSQL_ROOT_PASSWORD\\" --single-transaction --routines servicedesk" > backup.sql`);
d.p('Восстановление из копии:');
d.code(`docker compose exec -T db sh -c "mysql -uroot -p\\"$MYSQL_ROOT_PASSWORD\\" servicedesk" < backup.sql`);
d.p('Параметр --single-transaction создаёт согласованный снимок таблиц InnoDB без блокировки работы пользователей.');

// Приложение ------------------------------------------------------
d.appendix('А', 'SQL-скрипт создания базы данных (db/01_schema.sql)');
d.code(sqlText.replace(/\r/g, '').trim().split('\n'), { size: 15 });

d.save(path.join(__dirname, '..', '02_Структура_базы_данных.docx'));
