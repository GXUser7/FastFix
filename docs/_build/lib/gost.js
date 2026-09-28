// Мини-DSL для документов в оформлении по ГОСТ 7.32 / 2.105 (docx-js)
const fs = require('fs');
const path = require('path');
const {
  Document, Packer, Paragraph, TextRun, ImageRun, Table, TableRow, TableCell, WidthType, BorderStyle,
  AlignmentType, HeadingLevel, LevelFormat, PageNumber, Footer, Header, ShadingType, VerticalAlign,
  TableOfContents, PageBreak, PageOrientation, TabStopType, LineRuleType, TableLayoutType,
} = require('docx');

const MM = 56.6929; // DXA в 1 мм
const FONT = 'Times New Roman';
const PAGE = { w: 11906, h: 16838 };
const MARGIN = { left: Math.round(30 * MM), right: Math.round(15 * MM), top: Math.round(20 * MM), bottom: Math.round(20 * MM) };
const CONTENT_W = PAGE.w - MARGIN.left - MARGIN.right; // ≈ 9354 DXA = 165 мм
const INDENT = Math.round(12.5 * MM);

// ---- inline-разметка: **жирный**, *курсив*, `код` ----
function runs(text, base = {}) {
  const out = [];
  const re = /(\*\*[^*]+\*\*|\*[^*]+\*|`[^`]+`)/g;
  let last = 0, m;
  const push = (t, o) => { if (t) out.push(new TextRun({ text: t, font: FONT, ...base, ...o })); };
  while ((m = re.exec(text))) {
    push(text.slice(last, m.index), {});
    const tok = m[0];
    if (tok.startsWith('**')) push(tok.slice(2, -2), { bold: true });
    else if (tok.startsWith('`')) push(tok.slice(1, -1), { font: 'Consolas', size: (base.size || 28) - 4 });
    else push(tok.slice(1, -1), { italics: true });
    last = m.index + tok.length;
  }
  push(text.slice(last), {});
  return out;
}

class Doc {
  constructor({ title, subject }) {
    this.title = title;
    this.subject = subject;
    this.fig = 0;
    this.tab = 0;
    this.sections = [];
    this.body = [];
    this.appendixMode = null;
  }

  // ---------- блоки ----------
  p(text, opts = {}) {
    this.body.push(new Paragraph({
      children: runs(text, opts.run || {}),
      alignment: opts.align || AlignmentType.JUSTIFIED,
      indent: opts.noIndent ? undefined : { firstLine: INDENT },
      spacing: { line: 360, before: 0, after: 0, ...(opts.spacing || {}) },
      keepNext: opts.keepNext,
    }));
    return this;
  }
  h1(text, { pageBreak = true } = {}) {
    this.body.push(new Paragraph({
      heading: HeadingLevel.HEADING_1, children: runs(text), pageBreakBefore: pageBreak,
      indent: { firstLine: INDENT }, spacing: { before: 0, after: 240, line: 360 }, keepNext: true,
    }));
    return this;
  }
  // структурный элемент (СОДЕРЖАНИЕ, ВВЕДЕНИЕ, ПРИЛОЖЕНИЕ) — по центру прописными
  hc(text, { pageBreak = true, toc = true } = {}) {
    this.body.push(new Paragraph({
      heading: toc ? HeadingLevel.HEADING_1 : undefined, children: runs(text.toUpperCase(), { bold: true }),
      alignment: AlignmentType.CENTER, pageBreakBefore: pageBreak, spacing: { before: 0, after: 240, line: 360 }, keepNext: true,
    }));
    return this;
  }
  h2(text) {
    this.body.push(new Paragraph({
      heading: HeadingLevel.HEADING_2, children: runs(text),
      indent: { firstLine: INDENT }, spacing: { before: 240, after: 120, line: 360 }, keepNext: true,
    }));
    return this;
  }
  h3(text) {
    this.body.push(new Paragraph({
      heading: HeadingLevel.HEADING_3, children: runs(text),
      indent: { firstLine: INDENT }, spacing: { before: 120, after: 60, line: 360 }, keepNext: true,
    }));
    return this;
  }
  list(items, { numbered = false } = {}) {
    for (const it of items) {
      this.body.push(new Paragraph({
        children: runs(it),
        numbering: { reference: numbered ? 'num' : 'dash', level: 0 },
        alignment: AlignmentType.JUSTIFIED, spacing: { line: 360 },
      }));
    }
    return this;
  }
  // numbered with restart: создаём отдельный экземпляр нумерации
  olist(items) {
    this._olist = (this._olist || 0) + 1;
    const inst = this._olist;
    for (const it of items) {
      this.body.push(new Paragraph({
        children: runs(it), numbering: { reference: 'num', level: 0, instance: inst },
        alignment: AlignmentType.JUSTIFIED, spacing: { line: 360 },
      }));
    }
    return this;
  }
  code(lines, { size = 18 } = {}) {
    const arr = Array.isArray(lines) ? lines : lines.split('\n');
    arr.forEach((l, i) => this.body.push(new Paragraph({
      children: [new TextRun({ text: l.length ? l : ' ', font: 'Consolas', size })],
      spacing: { line: 240, before: i === 0 ? 60 : 0, after: i === arr.length - 1 ? 120 : 0 },
      shading: { type: ShadingType.CLEAR, color: 'auto', fill: 'F4F6F8' },
      indent: { left: 120, right: 120 },
    })));
    return this;
  }
  pageBreak() { this.body.push(new Paragraph({ children: [new PageBreak()] })); return this; }
  spacer(n = 1) { for (let i = 0; i < n; i++) this.body.push(new Paragraph({ children: [], spacing: { line: 360 } })); return this; }

  // ---------- рисунки ----------
  figure(file, caption, { widthMm = 165, maxHeightMm = 225 } = {}) {
    const buf = fs.readFileSync(file);
    const pw = buf.readUInt32BE(16), ph = buf.readUInt32BE(20);
    let wmm = widthMm, hmm = widthMm * ph / pw;
    if (hmm > maxHeightMm) { hmm = maxHeightMm; wmm = hmm * pw / ph; }
    const px = mm => Math.round(mm / 25.4 * 96);
    this.fig++;
    this.body.push(new Paragraph({
      alignment: AlignmentType.CENTER, spacing: { before: 120, after: 60, line: 240 }, keepNext: true,
      children: [new ImageRun({ type: 'png', data: buf, transformation: { width: px(wmm), height: px(hmm) }, altText: { title: caption, description: caption, name: path.basename(file) } })],
    }));
    this.body.push(new Paragraph({
      alignment: AlignmentType.CENTER, spacing: { before: 60, after: 240, line: 360 },
      children: runs(`Рисунок ${this.figNo()} — ${caption}`),
    }));
    return this;
  }
  figNo() { return this.appendixMode ? `${this.appendixMode}.${this.fig}` : `${this.fig}`; }
  nextFigRef() { return this.appendixMode ? `${this.appendixMode}.${this.fig + 1}` : `${this.fig + 1}`; }
  nextTabRef() { return this.appendixMode ? `${this.appendixMode}.${this.tab + 1}` : `${this.tab + 1}`; }

  // ---------- таблицы ----------
  // cols: [{ title, w (мм), align }]; rows: массив массивов строк
  table(caption, cols, rows, { size = 24, headerFill = 'E3EAF4', fitWidth = true, rowShading = null, bold = [] } = {}) {
    this.tab++;
    const no = this.appendixMode ? `${this.appendixMode}.${this.tab}` : `${this.tab}`;
    this.body.push(new Paragraph({
      children: runs(`Таблица ${no} — ${caption}`), keepNext: true,
      spacing: { before: 120, after: 60, line: 360 },
    }));
    const totalMm = cols.reduce((s, c) => s + c.w, 0);
    const k = fitWidth ? (CONTENT_W / MM) / totalMm : 1;
    const widths = cols.map(c => Math.round(c.w * k * MM));
    const tableW = widths.reduce((a, b) => a + b, 0);
    const border = { style: BorderStyle.SINGLE, size: 4, color: '000000' };
    const borders = { top: border, bottom: border, left: border, right: border };
    const cell = (text, i, header, fill) => new TableCell({
      width: { size: widths[i], type: WidthType.DXA }, borders, verticalAlign: VerticalAlign.CENTER,
      shading: fill ? { type: ShadingType.CLEAR, color: 'auto', fill } : undefined,
      margins: { top: 40, bottom: 40, left: 80, right: 80 },
      children: String(text).split('\n').map(line => new Paragraph({
        alignment: header ? AlignmentType.CENTER : (cols[i].align || AlignmentType.LEFT),
        spacing: { line: 240, before: 0, after: 0 },
        children: runs(line, { size, bold: header || undefined }),
      })),
    });
    const trs = [new TableRow({ tableHeader: true, cantSplit: true, children: cols.map((c, i) => cell(c.title, i, true, headerFill)) })];
    rows.forEach((r, ri) => trs.push(new TableRow({
      cantSplit: true,
      children: r.map((v, i) => cell(v, i, bold.includes(ri), rowShading ? rowShading(ri, i, r) : null)),
    })));
    this.body.push(new Table({ width: { size: tableW, type: WidthType.DXA }, columnWidths: widths, rows: trs, layout: TableLayoutType.FIXED }));
    this.body.push(new Paragraph({ children: [], spacing: { after: 120, line: 240 } }));
    return this;
  }

  toc() {
    this.body.push(new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 240, line: 360 }, children: runs('СОДЕРЖАНИЕ', { bold: true }) }));
    this.body.push(new TableOfContents('Содержание', { hyperlink: true, headingStyleRange: '1-2' }));
    return this;
  }

  appendix(letter, title) {
    this.appendixMode = letter;
    this.fig = 0; this.tab = 0;
    this.body.push(new Paragraph({
      heading: HeadingLevel.HEADING_1, pageBreakBefore: true, alignment: AlignmentType.CENTER,
      spacing: { after: 0, line: 360 }, keepNext: true,
      children: runs(`ПРИЛОЖЕНИЕ ${letter}`, { bold: true }),
    }));
    this.body.push(new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 240, line: 360 }, keepNext: true, children: runs(title, { bold: true }) }));
    return this;
  }

  // ---------- титульный лист ----------
  titlePage(children) { this.titleChildren = children; return this; }

  // Параграф титула
  static tp(text, { align = AlignmentType.CENTER, bold = false, size = 28, before = 0, after = 0, caps = false } = {}) {
    return new Paragraph({
      alignment: align, spacing: { before, after, line: 276 },
      children: runs(caps ? text.toUpperCase() : text, { bold, size }),
    });
  }

  // таблица подписей без рамок (титул, «Согласовано»)
  static signTable(left, right, { size = 28 } = {}) {
    const none = { style: BorderStyle.NONE, size: 0, color: 'FFFFFF' };
    const borders = { top: none, bottom: none, left: none, right: none };
    const half = Math.round(CONTENT_W / 2);
    const col = (lines, align) => new TableCell({
      width: { size: half, type: WidthType.DXA }, borders,
      children: lines.map(l => new Paragraph({ alignment: align, spacing: { line: 276, after: 60 }, children: runs(l, { size }) })),
    });
    return new Table({
      width: { size: half * 2, type: WidthType.DXA }, columnWidths: [half, half], layout: TableLayoutType.FIXED,
      borders: { top: none, bottom: none, left: none, right: none, insideHorizontal: none, insideVertical: none },
      rows: [new TableRow({ children: [col(left, AlignmentType.LEFT), col(right, AlignmentType.LEFT)] })],
    });
  }
  raw(el) { this.body.push(el); return this; }

  async save(file) {
    const footer = new Footer({ children: [new Paragraph({ alignment: AlignmentType.CENTER, children: [new TextRun({ children: [PageNumber.CURRENT], font: FONT, size: 24 })] })] });
    const page = { size: { width: PAGE.w, height: PAGE.h }, margin: MARGIN };
    const sections = [];
    if (this.titleChildren) {
      sections.push({ properties: { page, titlePage: true }, footers: { first: new Footer({ children: [] }), default: footer }, children: this.titleChildren });
    }
    sections.push({ properties: { page }, footers: { default: footer }, children: this.body });
    const doc = new Document({
      creator: 'Баймуратов Д.А.', title: this.title, subject: this.subject, description: this.subject,
      features: { updateFields: true },
      styles: {
        // переопределяем встроенные стили docx-js (у них синие заголовки 2E74B5) — все заголовки чёрные
        default: {
          document: { run: { font: FONT, size: 28, color: '000000' }, paragraph: { spacing: { line: 360 } } },
          title: { run: { font: FONT, size: 28, bold: true, color: '000000' } },
          heading1: { run: { font: FONT, size: 28, bold: true, color: '000000' }, paragraph: { spacing: { before: 0, after: 240 } } },
          heading2: { run: { font: FONT, size: 28, bold: true, color: '000000' }, paragraph: { spacing: { before: 240, after: 120 } } },
          heading3: { run: { font: FONT, size: 28, bold: true, italics: true, color: '000000' }, paragraph: { spacing: { before: 120, after: 60 } } },
          heading4: { run: { font: FONT, size: 28, bold: true, color: '000000' } },
          heading5: { run: { font: FONT, size: 28, color: '000000' } },
          heading6: { run: { font: FONT, size: 28, color: '000000' } },
          hyperlink: { run: { color: '000000', underline: {} } },
        },
      },
      numbering: {
        config: [
          { reference: 'dash', levels: [{ level: 0, format: LevelFormat.BULLET, text: '–', alignment: AlignmentType.LEFT,
            style: { paragraph: { indent: { left: INDENT + 360, hanging: 360 } }, run: { font: FONT } } }] },
          { reference: 'num', levels: [{ level: 0, format: LevelFormat.DECIMAL, text: '%1)', alignment: AlignmentType.LEFT,
            style: { paragraph: { indent: { left: INDENT + 420, hanging: 420 } }, run: { font: FONT } } }] },
        ],
      },
      sections,
    });
    const buf = await Packer.toBuffer(doc);
    fs.writeFileSync(file, buf);
    console.log('saved', file, Math.round(buf.length / 1024) + ' KB', `рис.: ${this.fig}, табл.: ${this.tab}`);
  }
}

module.exports = { Doc, runs, MM, CONTENT_W, INDENT, FONT, AlignmentType, Paragraph, TextRun, Table, TableRow, TableCell, WidthType, BorderStyle, TabStopType, PageBreak };
