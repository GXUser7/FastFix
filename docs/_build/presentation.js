// 8. Презентация проекта (pptxgenjs)
const fs = require('fs');
const path = require('path');
const pptxgen = require('pptxgenjs');

const IMG = f => path.join(__dirname, 'img', f);
const SHOT = f => path.join(__dirname, 'img', 'screens', f);
const unit = require('./unit_results.json');
const e2e = require('./e2e_results.json');

const C = { navy: '1B2A41', navy2: '24344F', blue: '2D6CDF', bg: 'F4F6F8', text: '212121', muted: '7A8794', white: 'FFFFFF', line: 'E3E8EE',
  st: { accepted: '7A8794', diagnostics: '2196F3', approval: 'F39C12', waiting: 'F5C518', work: '7E57C2', ready: '27AE60', issued: '1E8449', cancelled: 'E74C3C' } };
const FONT = 'Segoe UI';
const W = 13.333, H = 7.5;

const pres = new pptxgen();
pres.layout = 'LAYOUT_WIDE';
pres.author = 'Баймуратов Д. А.';
pres.title = 'ИС «СервисДеск» — управление заявками в сервисный центр';

// размеры PNG для вписывания с сохранением пропорций
function pngSize(file) {
  const b = fs.readFileSync(file);
  return { w: b.readUInt32BE(16), h: b.readUInt32BE(20) };
}
function fit(file, x, y, maxW, maxH, { align = 'center', valign = 'middle' } = {}) {
  const s = pngSize(file);
  let w = maxW, h = maxW * s.h / s.w;
  if (h > maxH) { h = maxH; w = maxH * s.w / s.h; }
  const dx = align === 'center' ? (maxW - w) / 2 : align === 'right' ? maxW - w : 0;
  const dy = valign === 'middle' ? (maxH - h) / 2 : valign === 'bottom' ? maxH - h : 0;
  return { path: file, x: x + dx, y: y + dy, w, h };
}
const shadow = () => ({ type: 'outer', color: '1B2A41', opacity: 0.18, blur: 10, offset: 3, angle: 90 });

function text(slide, t, o) {
  slide.addText(t, { fontFace: FONT, color: C.text, isTextBox: true, margin: 0, valign: 'top', ...o });
}
function wordmark(slide, x, y, size = 20, onDark = true) {
  text(slide, [{ text: 'Сервис', options: { color: onDark ? C.white : C.navy, bold: true } }, { text: 'Деск', options: { color: C.blue, bold: true } }],
    { x, y, w: 3, h: size / 60 + 0.1, fontSize: size });
}
// заголовок светлого слайда
function head(slide, title, sub, n) {
  slide.background = { color: C.bg };
  text(slide, title, { x: 0.6, y: 0.45, w: 10.5, h: 0.7, fontSize: 30, bold: true, color: C.navy });
  if (sub) text(slide, sub, { x: 0.6, y: 1.12, w: 11, h: 0.4, fontSize: 15, color: C.muted });
  wordmark(slide, W - 2.25, 0.52, 16, false);
  text(slide, String(n), { x: W - 0.9, y: H - 0.5, w: 0.4, h: 0.3, fontSize: 11, color: C.muted, align: 'right' });
}
function card(slide, x, y, w, h, fill = C.white) {
  slide.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y, w, h, fill: { color: fill }, line: { color: C.line, width: 0.75 }, rectRadius: 0.08, shadow: shadow() });
}
function badge(slide, t, color, x, y, w) {
  slide.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y, w, h: 0.34, fill: { color }, line: { color }, rectRadius: 0.17 });
  text(slide, t, { x, y, w, h: 0.34, fontSize: 11, bold: true, color: C.white, align: 'center', valign: 'middle' });
}
function circleNum(slide, n, x, y, color = C.blue, d = 0.5) {
  slide.addShape(pres.shapes.OVAL, { x, y, w: d, h: d, fill: { color }, line: { color } });
  text(slide, String(n), { x, y, w: d, h: d, fontSize: 16, bold: true, color: C.white, align: 'center', valign: 'middle' });
}
function screenshot(slide, file, x, y, maxW, maxH, opts) {
  const f = fit(file, x, y, maxW, maxH, opts);
  slide.addShape(pres.shapes.ROUNDED_RECTANGLE, { x: f.x - 0.04, y: f.y - 0.04, w: f.w + 0.08, h: f.h + 0.08, fill: { color: C.white }, line: { color: 'D5DCE4', width: 0.75 }, rectRadius: 0.05, shadow: shadow() });
  slide.addImage(f);
  return f;
}

let n = 0;

// 1. Титул ------------------------------------------------------------
{
  const s = pres.addSlide(); n++;
  s.background = { color: C.navy };
  wordmark(s, 0.8, 0.7, 26);
  text(s, 'Информационная система управления заявками в сервисный центр', { x: 0.8, y: 2.2, w: 7.6, h: 1.9, fontSize: 36, bold: true, color: C.white });
  text(s, 'От приёма устройства до выдачи — прослеживаемость каждой заявки', { x: 0.8, y: 4.15, w: 7.4, h: 0.6, fontSize: 17, color: '9FB0C8' });
  text(s, [{ text: 'Выполнил: ', options: { color: '9FB0C8' } }, { text: 'Баймуратов Данил Азатович, группа 23П-1', options: { color: C.white } }],
    { x: 0.8, y: 5.9, w: 7, h: 0.35, fontSize: 14 });
  text(s, 'Fullstack-разработчик · 2026', { x: 0.8, y: 6.3, w: 7, h: 0.35, fontSize: 13, color: '9FB0C8' });
  const f = fit(SHOT('w_order_approval.png'), 8.9, 0.6, 3.9, 6.3);
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x: f.x - 0.06, y: f.y - 0.06, w: f.w + 0.12, h: f.h + 0.12, fill: { color: C.navy2 }, line: { color: C.navy2 }, rectRadius: 0.08 });
  s.addImage(f);
  s.addNotes('Тема проекта — информационная система «СервисДеск» для сервисного центра по ремонту бытовой и цифровой техники.');
}

// 2. Проблема и цель --------------------------------------------------
{
  const s = pres.addSlide(); n++;
  head(s, 'Проблема и цель', 'Сейчас учёт ведётся в бумажном журнале и таблицах', n);
  const problems = [
    ['Потеря сведений', 'о неисправности и комплектности при передаче устройства от приёмщика мастеру'],
    ['Звонки клиентов', 'нет актуальной информации о ходе ремонта'],
    ['Расхождения склада', 'неучтённое использование запчастей'],
    ['Ошибки в расчётах', 'нет письменного согласования цены с клиентом'],
  ];
  problems.forEach(([t, d], i) => {
    const y = 1.85 + i * 1.2;
    card(s, 0.6, y, 6.4, 1.0);
    circleNum(s, i + 1, 0.85, y + 0.25, C.st.cancelled);
    text(s, t, { x: 1.55, y: y + 0.16, w: 5.3, h: 0.35, fontSize: 16, bold: true });
    text(s, d, { x: 1.55, y: y + 0.52, w: 5.3, h: 0.4, fontSize: 13, color: C.muted });
  });
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x: 7.5, y: 1.85, w: 5.2, h: 4.6, fill: { color: C.navy }, line: { color: C.navy }, rectRadius: 0.1 });
  text(s, 'Цель', { x: 7.9, y: 2.15, w: 4.5, h: 0.5, fontSize: 22, bold: true, color: C.blue });
  text(s, 'Автоматизировать работу сервисного центра на всём пути устройства и исключить потерю информации между сотрудниками', { x: 7.9, y: 2.75, w: 4.5, h: 1.6, fontSize: 17, color: C.white });
  const goals = ['оформление заявки ≤ 3 мин', 'поиск заявки ≤ 2 с', 'статус у клиента ≤ 5 с', '100 % смен статусов в истории'];
  text(s, goals.map((g, i) => ({ text: g, options: { bullet: true, breakLine: i < goals.length - 1 } })), { x: 7.9, y: 4.45, w: 4.5, h: 1.8, fontSize: 14, color: 'C9D6EA', paraSpaceAfter: 6 });
}

// 3. Пользователи и подсистемы ----------------------------------------
{
  const s = pres.addSlide(); n++;
  head(s, 'Пользователи и подсистемы', 'Четыре роли, четыре функциональные подсистемы, личный кабинет и администрирование', n);
  const roles = [
    ['Клиент', 'Веб-кабинет', C.blue, ['статус заявки в реальном времени', 'согласование стоимости', 'квитанция и акт в PDF']],
    ['Приёмщик', 'Настольное приложение', C.st.diagnostics, ['оформление заявки, квитанция', 'приём оплаты, выдача', 'все заявки без права ремонта']],
    ['Мастер', 'Настольное приложение', C.st.work, ['диагностика и журнал работ', 'запрос и резерв запчастей', 'статусы своего этапа']],
    ['Кладовщик', 'Настольное приложение', C.st.approval, ['остатки и оприходование', 'выдача деталей мастерам', 'инвентаризация, журнал']],
  ];
  roles.forEach(([t, app, color, items], i) => {
    const x = 0.6 + i * 3.08, y = 1.85;
    card(s, x, y, 2.85, 3.3);
    s.addShape(pres.shapes.OVAL, { x: x + 0.3, y: y + 0.3, w: 0.62, h: 0.62, fill: { color }, line: { color } });
    text(s, t[0], { x: x + 0.3, y: y + 0.3, w: 0.62, h: 0.62, fontSize: 20, bold: true, color: C.white, align: 'center', valign: 'middle' });
    text(s, t, { x: x + 0.3, y: y + 1.08, w: 2.4, h: 0.4, fontSize: 18, bold: true });
    text(s, app, { x: x + 0.3, y: y + 1.48, w: 2.4, h: 0.3, fontSize: 12, color: C.muted });
    text(s, items.map((it, k) => ({ text: it, options: { bullet: true, breakLine: k < items.length - 1 } })), { x: x + 0.3, y: y + 1.9, w: 2.4, h: 1.3, fontSize: 12.5, paraSpaceAfter: 4 });
  });
  const subs = [['Приём заявок', C.blue], ['Управление ремонтом', C.st.work], ['Склад запчастей', 'E08A00'], ['Расчёты и документы', C.st.issued]];
  subs.forEach(([t, c], i) => {
    const x = 0.6 + i * 3.08;
    s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y: 5.5, w: 2.85, h: 0.75, fill: { color: c }, line: { color: c }, rectRadius: 0.1 });
    text(s, t, { x, y: 5.5, w: 2.85, h: 0.75, fontSize: 15, bold: true, color: C.white, align: 'center', valign: 'middle' });
  });
}

// 4. Архитектура ------------------------------------------------------
{
  const s = pres.addSlide(); n++;
  head(s, 'Архитектура', 'Трёхзвенная клиент-серверная система: клиенты работают с данными только через API', n);
  const f = fit(IMG('architecture.png'), 0.6, 1.7, 8.1, 5.2);
  card(s, f.x - 0.15, f.y - 0.15, f.w + 0.3, f.h + 0.3);
  s.addImage(f);
  const stack = [
    ['СУБД', 'MySQL 8.0 · 18 таблиц, 2 представления'],
    ['Сервер', 'ASP.NET Core 8, EF Core, JWT, SignalR, QuestPDF, Swagger'],
    ['Сотрудники', 'WPF, .NET 8'],
    ['Клиенты', 'Vue 3, Vite, адаптивная вёрстка'],
    ['Запуск', 'Docker Compose: db, api, web (nginx), backup'],
  ];
  stack.forEach(([t, d], i) => {
    const y = 1.75 + i * 1.02;
    text(s, t, { x: 9.2, y, w: 3.6, h: 0.32, fontSize: 15, bold: true, color: C.blue });
    text(s, d, { x: 9.2, y: y + 0.34, w: 3.6, h: 0.6, fontSize: 13 });
  });
}

// 5. База данных ------------------------------------------------------
{
  const s = pres.addSlide(); n++;
  head(s, 'База данных', 'Ссылочная целостность — внешние ключи; корректность — ограничения CHECK и UNIQUE', n);
  const f = fit(IMG('er_overview.png'), 0.6, 1.7, 8.6, 5.3);
  card(s, f.x - 0.15, f.y - 0.15, f.w + 0.3, f.h + 0.3);
  s.addImage(f);
  const stats = [['18', 'таблиц'], ['2', 'представления'], ['5 лет', 'хранение истории']];
  stats.forEach(([v, l], i) => {
    const y = 1.7 + i * 1.35;
    text(s, v, { x: 9.7, y, w: 3.1, h: 0.85, fontSize: 48, bold: true, color: C.navy });
    text(s, l, { x: 9.7, y: y + 0.85, w: 3.1, h: 0.35, fontSize: 14, color: C.muted });
  });
  text(s, 'Складские и платёжные операции выполняются в транзакциях, строка запчасти блокируется при резервировании (SELECT … FOR UPDATE)', { x: 9.7, y: 5.85, w: 3.1, h: 0.9, fontSize: 11.5, color: C.muted });
}

// 6. Жизненный цикл ---------------------------------------------------
{
  const s = pres.addSlide(); n++;
  head(s, 'Жизненный цикл заявки', 'Статус меняется только по допустимым переходам и только в пределах этапа своей роли', n);
  const f = fit(IMG('lifecycle.png'), 0.6, 1.7, 8.4, 5.3);
  card(s, f.x - 0.15, f.y - 0.15, f.w + 0.3, f.h + 0.3);
  s.addImage(f);
  const st = [['Принята', C.st.accepted], ['Диагностика', C.st.diagnostics], ['Согласование с клиентом', C.st.approval], ['Ожидание запчастей', C.st.waiting],
    ['В работе', C.st.work], ['Готова к выдаче', C.st.ready], ['Выдана', C.st.issued], ['Отменена', C.st.cancelled]];
  st.forEach(([t, c], i) => badge(s, t, c, 9.6, 1.75 + i * 0.52, 3.1));
  text(s, 'Недопустимый переход сервер отклоняет с кодом 409', { x: 9.6, y: 6.05, w: 3.2, h: 0.6, fontSize: 12, color: C.muted });
}

// 7. Настольное приложение: приёмщик ---------------------------------
{
  const s = pres.addSlide(); n++;
  head(s, 'Рабочее место приёмщика', 'Поиск по номеру и телефону, фильтр по статусу, выделение просрочки', n);
  screenshot(s, SHOT('d_recept_orders.png'), 0.6, 1.75, 8.3, 5.2);
  const pts = [
    ['Новая заявка', 'поиск клиента по телефону, быстрая регистрация, чек-лист комплектности'],
    ['Квитанция', 'номер заявки и PDF для печати сразу после оформления'],
    ['Оплата', 'наличные, карта, СБП; частичная оплата с контролем остатка'],
    ['Выдача', 'только после полной оплаты, акт выдачи'],
  ];
  pts.forEach(([t, d], i) => {
    const y = 1.8 + i * 1.28;
    circleNum(s, i + 1, 9.3, y, C.blue, 0.46);
    text(s, t, { x: 9.95, y: y - 0.02, w: 2.9, h: 0.34, fontSize: 15, bold: true });
    text(s, d, { x: 9.95, y: y + 0.33, w: 2.9, h: 0.8, fontSize: 12, color: C.muted });
  });
}

// 8. Мастер -----------------------------------------------------------
{
  const s = pres.addSlide(); n++;
  head(s, 'Рабочее место мастера', 'Действия в карточке зависят от роли и текущего статуса заявки', n);
  const pts = [
    ['Диагностика', 'заключение и гарантийный срок 0–36 мес.'],
    ['Смета', 'работы из прайс-листа или произвольные, запчасти со склада'],
    ['Расчёт', 'S = Σ работ + Σ цена × кол-во запчастей; гарантия — 0 ₽'],
    ['Нехватка детали', 'запрос «ожидает поступления», заявка → «Ожидание запчастей»'],
  ];
  pts.forEach(([t, d], i) => {
    const y = 1.8 + i * 1.28;
    circleNum(s, i + 1, 0.6, y, C.st.work, 0.46);
    text(s, t, { x: 1.25, y: y - 0.02, w: 3.0, h: 0.34, fontSize: 15, bold: true });
    text(s, d, { x: 1.25, y: y + 0.33, w: 3.0, h: 0.8, fontSize: 12, color: C.muted });
  });
  screenshot(s, SHOT('d_master_card.png'), 4.5, 1.75, 8.25, 5.2);
}

// 9. Склад ------------------------------------------------------------
{
  const s = pres.addSlide(); n++;
  head(s, 'Склад запчастей', 'Доступный остаток = на складе − в резерве; ниже минимального — подсветка', n);
  screenshot(s, SHOT('d_sk_parts.png'), 0.6, 1.75, 6.1, 4.0);
  screenshot(s, SHOT('d_sk_inventory.png'), 6.95, 1.75, 5.8, 4.0);
  const pts = ['Резерв под заявку по запросу мастера', 'Оприходование закрывает очередь запросов автоматически', 'Инвентаризация: Δ = факт − учёт, корректировка и журнал'];
  pts.forEach((p, i) => {
    const x = 0.6 + i * 4.1;
    card(s, x, 6.0, 3.9, 0.85);
    text(s, p, { x: x + 0.2, y: 6.0, w: 3.5, h: 0.85, fontSize: 13, valign: 'middle' });
  });
}

// 10. Веб-кабинет -----------------------------------------------------
{
  const s = pres.addSlide(); n++;
  head(s, 'Личный кабинет клиента', 'Изменения статуса приходят через WebSocket (SignalR) без перезагрузки страницы', n);
  screenshot(s, SHOT('w_orders.png'), 0.6, 1.75, 6.6, 4.3);
  screenshot(s, SHOT('w_order_approval.png'), 7.6, 1.75, 2.75, 5.2);
  const pts = ['Шкала этапов ремонта', 'Согласование или отказ с фиксацией даты', 'Квитанция, смета и акт в PDF', 'Адаптивность: 1200 / 768 / 480 px'];
  text(s, pts.map((p, i) => ({ text: p, options: { bullet: true, breakLine: i < pts.length - 1 } })), { x: 10.65, y: 1.8, w: 2.2, h: 4.5, fontSize: 13.5, paraSpaceAfter: 10 });
  text(s, 'Регистрация по тому же телефону, что назван приёмщику, — заявки появляются в кабинете автоматически', { x: 0.6, y: 6.3, w: 6.6, h: 0.6, fontSize: 12, color: C.muted });
}

// 11. Документы -------------------------------------------------------
{
  const s = pres.addSlide(); n++;
  head(s, 'Расчёты и документы', 'Каждый документ регистрируется в реестре с уникальным номером', n);
  const docs = [['КВ', 'Квитанция о приёме', 'при оформлении'], ['СМ', 'Смета', 'после диагностики'], ['АВР', 'Акт выполненных работ', 'при готовности'], ['АВ', 'Акт выдачи', 'после выдачи']];
  docs.forEach(([p, t, w], i) => {
    const y = 1.8 + i * 1.15;
    card(s, 0.6, y, 5.6, 0.95);
    s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x: 0.8, y: y + 0.2, w: 0.9, h: 0.55, fill: { color: C.st.cancelled }, line: { color: C.st.cancelled }, rectRadius: 0.06 });
    text(s, p, { x: 0.8, y: y + 0.2, w: 0.9, h: 0.55, fontSize: 14, bold: true, color: C.white, align: 'center', valign: 'middle' });
    text(s, t, { x: 1.95, y: y + 0.15, w: 4.1, h: 0.35, fontSize: 15, bold: true });
    text(s, w, { x: 1.95, y: y + 0.5, w: 4.1, h: 0.3, fontSize: 12, color: C.muted });
  });
  text(s, 'Выдача — только после полной оплаты (кроме гарантийных и бесплатных заявок)', { x: 0.6, y: 6.5, w: 5.6, h: 0.5, fontSize: 12, color: C.muted });
  screenshot(s, SHOT('pdf_act.png'), 6.7, 1.75, 6.0, 5.25);
}

// 12. Тестирование ----------------------------------------------------
{
  const s = pres.addSlide(); n++;
  head(s, 'Тестирование', 'Модульные, функциональные и интерфейсные проверки — все пройдены', n);
  const stats = [[String(unit.length), 'модульных тестов xUnit', C.navy], [String(e2e.length), 'функциональных тест-кейсов через API', C.blue], ['16', 'проверок интерфейса', C.st.work], ['16 мс', 'среднее время ответа API (норма ≤ 1 с)', C.st.issued]];
  stats.forEach(([v, l, c], i) => {
    const x = 0.6 + i * 3.08;
    card(s, x, 1.85, 2.85, 2.3);
    text(s, v, { x: x + 0.3, y: 2.05, w: 2.4, h: 1.1, fontSize: 54, bold: true, color: c });
    text(s, l, { x: x + 0.3, y: 3.2, w: 2.4, h: 0.8, fontSize: 13, color: C.muted });
  });
  card(s, 0.6, 4.5, 12.1, 2.35);
  text(s, 'Найдено и исправлено 6 дефектов', { x: 0.9, y: 4.7, w: 11.5, h: 0.4, fontSize: 17, bold: true });
  const defs = ['двойной учёт стоимости запчасти в смете', 'клавиша Enter не открывала карточку в списке', 'столбцы таблиц схлопывались при масштабе 125 %', 'перекрытие заголовка карточки, узкие поля форм'];
  text(s, defs.map((d, i) => ({ text: d, options: { bullet: true, breakLine: i < defs.length - 1 } })), { x: 0.9, y: 5.2, w: 11.5, h: 1.5, fontSize: 13.5, paraSpaceAfter: 4 });
}

// 13. Развёртывание ---------------------------------------------------
{
  const s = pres.addSlide(); n++;
  head(s, 'Развёртывание и безопасность', 'Серверная часть запускается одной командой docker compose up', n);
  const f = fit(IMG('deployment.png'), 0.6, 1.7, 7.9, 5.2);
  card(s, f.x - 0.15, f.y - 0.15, f.w + 0.3, f.h + 0.3);
  s.addImage(f);
  const sec = ['JWT на 12 часов, пароли — хеш BCrypt', 'Блокировка на 15 минут после 5 неудачных попыток', 'Права проверяются на сервере при каждом запросе', 'Только параметризованные запросы ORM', 'Ежедневный mysqldump, хранение 30 дней', 'Автоперезапуск контейнеров, данные в томе'];
  sec.forEach((t, i) => {
    const y = 1.8 + i * 0.83;
    s.addShape(pres.shapes.OVAL, { x: 9.0, y: y + 0.07, w: 0.26, h: 0.26, fill: { color: C.st.ready }, line: { color: C.st.ready } });
    text(s, t, { x: 9.45, y, w: 3.4, h: 0.7, fontSize: 13 });
  });
}

// 14. Итоги -----------------------------------------------------------
{
  const s = pres.addSlide(); n++;
  s.background = { color: C.navy };
  wordmark(s, 0.8, 0.6, 20);
  text(s, 'Итоги', { x: 0.8, y: 1.4, w: 6, h: 0.8, fontSize: 36, bold: true, color: C.white });
  const done = ['REST API на ASP.NET Core с разграничением прав по ролям', 'Настольное приложение WPF для приёмщика, мастера и кладовщика', 'Веб-кабинет клиента на Vue с уведомлениями в реальном времени', 'Документы PDF, учёт склада, оплата и выдача', 'Docker Compose, резервное копирование', 'Комплект документации по разделу 9 ТЗ'];
  done.forEach((t, i) => {
    const y = 2.45 + i * 0.68;
    s.addShape(pres.shapes.OVAL, { x: 0.8, y: y + 0.06, w: 0.3, h: 0.3, fill: { color: C.blue }, line: { color: C.blue } });
    text(s, t, { x: 1.3, y, w: 6.4, h: 0.5, fontSize: 15, color: C.white });
  });
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x: 8.3, y: 1.5, w: 4.3, h: 4.9, fill: { color: C.navy2 }, line: { color: C.navy2 }, rectRadius: 0.1 });
  text(s, 'Развитие', { x: 8.7, y: 1.8, w: 3.6, h: 0.5, fontSize: 20, bold: true, color: C.blue });
  const next = ['SMS и e-mail уведомления клиенту', 'Онлайн-оплата в личном кабинете', 'Отчёты по выручке и загрузке мастеров', 'Мобильное приложение для мастеров'];
  text(s, next.map((t, i) => ({ text: t, options: { bullet: true, breakLine: i < next.length - 1 } })), { x: 8.7, y: 2.45, w: 3.6, h: 3.6, fontSize: 14, color: 'C9D6EA', paraSpaceAfter: 10 });
  text(s, 'Спасибо за внимание!', { x: 0.8, y: 6.6, w: 7, h: 0.5, fontSize: 18, bold: true, color: C.white });
}

pres.writeFile({ fileName: path.join(__dirname, '..', '08_Презентация.pptx') }).then(f => console.log('saved', f, n, 'слайдов'));
