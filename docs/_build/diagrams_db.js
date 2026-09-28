// ER-диаграммы и диаграмма жизненного цикла заявки (Graphviz → SVG → PNG)
const fs = require('fs');
const path = require('path');
const { svgToPng } = require('./lib/png');

const schema = JSON.parse(fs.readFileSync(path.join(__dirname, 'schema.json'), 'utf8'));
const OUT = path.join(__dirname, 'img');
fs.mkdirSync(OUT, { recursive: true });

const GROUPS = [
  { key: 'acc', title: 'Учётные записи', color: '#1B2A41', tables: ['roles', 'users'] },
  { key: 'intake', title: 'Приём заявок', color: '#2D6CDF', tables: ['device_types', 'devices', 'order_statuses', 'orders', 'completeness_items', 'order_completeness'] },
  { key: 'repair', title: 'Управление ремонтом', color: '#7E57C2', tables: ['order_status_history', 'services', 'order_works'] },
  { key: 'stock', title: 'Склад запчастей', color: '#E08A00', tables: ['parts', 'part_reservations', 'part_movements', 'inventories', 'inventory_items'] },
  { key: 'pay', title: 'Расчёты и документы', color: '#1E8449', tables: ['payments', 'documents'] },
];
const groupOf = t => GROUPS.find(g => g.tables.includes(t));
const esc = s => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
const shortType = t => t.replace(/^enum\(.*\)$/, 'enum').replace('tinyint(1)', 'bool');

function tableNode(t, compact) {
  const g = groupOf(t.name);
  const fkCols = new Set(schema.fks.filter(f => f.table === t.name).map(f => f.column));
  let rows = `<TR><TD COLSPAN="3" BGCOLOR="${g.color}" CELLPADDING="6" ALIGN="LEFT"><FONT COLOR="white" POINT-SIZE="13"><B>${t.name}</B></FONT></TD></TR>`;
  for (const c of t.columns) {
    const isPk = c.key === 'PRI';
    const isFk = fkCols.has(c.name);
    if (compact && !isPk && !isFk) continue;
    const badge = isPk && isFk ? 'PK FK' : isPk ? 'PK' : isFk ? 'FK' : '';
    const badgeColor = isPk ? '#C27C00' : '#2D6CDF';
    const name = isPk ? `<B>${esc(c.name)}</B>` : esc(c.name);
    rows += `<TR>`
      + `<TD ALIGN="LEFT" WIDTH="34" PORT="${c.name}_l">${badge ? `<FONT POINT-SIZE="9" COLOR="${badgeColor}"><B>${badge}</B></FONT>` : ' '}</TD>`
      + `<TD ALIGN="LEFT">${name}${c.nullable === 'YES' ? '<FONT COLOR="#7A8794"> ?</FONT>' : ''}</TD>`
      + `<TD ALIGN="LEFT" PORT="${c.name}_r"><FONT COLOR="#7A8794" POINT-SIZE="10">${esc(shortType(c.type))}  </FONT></TD>`
      + `</TR>`;
  }
  if (compact) rows += `<TR><TD COLSPAN="3" ALIGN="LEFT"><FONT POINT-SIZE="9" COLOR="#7A8794">… ещё полей: ${t.columns.filter(c => c.key !== 'PRI' && !fkCols.has(c.name)).length}</FONT></TD></TR>`;
  return `"${t.name}" [label=<<TABLE BORDER="1" COLOR="#C9D2DC" CELLBORDER="0" CELLSPACING="0" CELLPADDING="3" BGCOLOR="white">${rows}</TABLE>>];`;
}

function erDot(tableNames, { compact = false, clusters = true, rankdir = 'LR', staffEdges = true, focus = null } = {}) {
  const tables = schema.tables.filter(t => tableNames.includes(t.name));
  // во фрагментах «чужие» таблицы показываем свёрнуто — только ключи
  const isCompact = t => compact || (focus && !GROUPS.find(g => g.key === focus).tables.includes(t.name));
  let dot = `digraph ER {
    graph [rankdir=${rankdir}, splines=spline, nodesep=0.3, ranksep=1.1, pad=0.25, fontname="Arial", bgcolor="white", newrank=true];
    node  [shape=plain, fontname="Arial", fontsize=11, fontcolor="#212121"];
    edge  [dir=both, color="#8A97A6", penwidth=1.2, arrowsize=0.8, fontname="Arial"];\n`;
  for (const g of GROUPS) {
    const inGroup = tables.filter(t => g.tables.includes(t.name));
    if (!inGroup.length) continue;
    if (clusters) {
      dot += `subgraph cluster_${g.key} { label=<<FONT POINT-SIZE="14" COLOR="${g.color}"><B>${g.title}</B></FONT>>; labeljust=l; style="rounded,filled"; fillcolor="${g.color}0F"; color="${g.color}55"; penwidth=1.4; margin=14;\n`;
      inGroup.forEach(t => { dot += tableNode(t, isCompact(t)) + '\n'; });
      dot += '}\n';
    } else inGroup.forEach(t => { dot += tableNode(t, isCompact(t)) + '\n'; });
  }
  for (const f of schema.fks) {
    if (!tableNames.includes(f.table) || !tableNames.includes(f.ref_table)) continue;
    const col = schema.tables.find(t => t.name === f.table).columns.find(c => c.name === f.column);
    const optional = col.nullable === 'YES';
    const isUserRef = f.ref_table === 'users' && !['client_id'].includes(f.column);
    if (isUserRef && !staffEdges) continue;
    dot += `"${f.ref_table}":"${f.ref_column}_r":e -> "${f.table}":"${f.column}_l":w [arrowtail=${optional ? 'teeodot' : 'teetee'}, arrowhead=crowodot${isUserRef ? ', style=dashed, color="#A9B4BF"' : ''}];\n`;
  }
  return dot + '}\n';
}

function lifecycleDot() {
  const S = {
    accepted: ['Принята', '#7A8794'], diagnostics: ['Диагностика', '#2196F3'], approval: ['Согласование\\nс клиентом', '#F39C12'],
    waiting_parts: ['Ожидание\\nзапчастей', '#F5C518'], in_work: ['В работе', '#7E57C2'], ready: ['Готова\\nк выдаче', '#27AE60'],
    issued: ['Выдана', '#1E8449'], cancelled: ['Отменена', '#E74C3C'],
  };
  const node = (k) => `${k} [label="${S[k][0]}", fillcolor="${S[k][1]}", fontcolor="${k === 'waiting_parts' ? '#212121' : 'white'}"];`;
  const e = (a, b, who, extra = '') => `${a} -> ${b} [label=<<FONT POINT-SIZE="10" COLOR="#5B6875">${who}</FONT>>${extra}];`;
  return `digraph L {
    graph [rankdir=LR, pad=0.3, nodesep=0.5, ranksep=0.55, fontname="Arial", bgcolor="white"];
    node [shape=box, style="rounded,filled", fontname="Arial", fontsize=13, penwidth=0, margin="0.22,0.12", height=0.6];
    edge [color="#5B6875", penwidth=1.4, arrowsize=0.8, fontname="Arial"];
    start [shape=circle, label="", width=0.22, fillcolor="#1B2A41"];
    finish [shape=doublecircle, label="", width=0.18, fillcolor="#1B2A41"];
    ${Object.keys(S).map(node).join('\n')}
    { rank=same; approval; cancelled; }
    ${e('start', 'accepted', 'Приёмщик:<BR/>оформление')}
    ${e('accepted', 'diagnostics', 'Мастер')}
    ${e('diagnostics', 'approval', 'Мастер:<BR/>смета')}
    ${e('approval', 'in_work', 'Клиент:<BR/>согласовал')}
    ${e('approval', 'waiting_parts', 'Клиент: согласовал,<BR/>нет запчастей')}
    ${e('waiting_parts', 'in_work', 'Мастер:<BR/>детали выданы')}
    ${e('in_work', 'waiting_parts', 'Мастер', ', style=dashed')}
    ${e('in_work', 'ready', 'Мастер:<BR/>работы завершены')}
    ${e('ready', 'issued', 'Приёмщик:<BR/>оплата и выдача')}
    ${e('approval', 'cancelled', 'Клиент:<BR/>отказ')}
    ${e('cancelled', 'issued', 'Приёмщик:<BR/>возврат устройства', ', style=dashed')}
    ${e('issued', 'finish', '')}
  }`;
}

(async () => {
  const viz = await require('@viz-js/viz').instance();
  const render = (dot, name) => {
    const svg = viz.renderString(dot, { format: 'svg' });
    fs.writeFileSync(path.join(OUT, name + '.svg'), svg, 'utf8');
    const size = svgToPng(svg, path.join(OUT, name + '.png'));
    console.log(name, Math.round(size.w), 'x', Math.round(size.h));
  };
  const all = schema.tables.map(t => t.name);
  render(erDot(all, { compact: true, staffEdges: false }), 'er_overview');
  render(erDot(['roles', 'users', 'device_types', 'devices', 'order_statuses', 'orders', 'completeness_items', 'order_completeness'], { focus: 'intake' }), 'er_intake');
  render(erDot(['orders', 'order_statuses', 'order_status_history', 'services', 'order_works', 'users'], { focus: 'repair' }), 'er_repair');
  render(erDot(['orders', 'parts', 'part_reservations', 'part_movements', 'inventories', 'inventory_items', 'users'], { focus: 'stock' }), 'er_stock');
  render(erDot(['orders', 'payments', 'documents', 'users'], { focus: 'pay' }), 'er_pay');
  render(lifecycleDot(), 'lifecycle');
})();
