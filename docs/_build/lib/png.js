// Рендер HTML/SVG в PNG через Edge headless (в системе нет LibreOffice/Chrome-driver)
const { execFileSync } = require('child_process');
const fs = require('fs');
const path = require('path');

const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';

function htmlToPng(htmlPath, pngPath, width, height, scale = 2) {
  const abs = path.resolve(htmlPath).replace(/\\/g, '/');
  const out = path.resolve(pngPath);
  if (fs.existsSync(out)) fs.unlinkSync(out);
  execFileSync(EDGE, [
    '--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run',
    '--user-data-dir=' + path.resolve(__dirname, '..', '.edge-profile'),
    `--force-device-scale-factor=${scale}`,
    `--window-size=${Math.ceil(width)},${Math.ceil(height)}`,
    '--virtual-time-budget=4000',
    `--screenshot=${out}`,
    'file:///' + abs,
  ], { stdio: 'ignore', timeout: 60000 });
  if (!fs.existsSync(out)) throw new Error('screenshot failed: ' + pngPath);
  return out;
}

// SVG-строку оборачиваем в HTML нужного размера и снимаем
function svgToPng(svg, pngPath, scale = 2) {
  const m = svg.match(/<svg[^>]*width="([\d.]+)pt"[^>]*height="([\d.]+)pt"/);
  let w = 1200, h = 800;
  if (m) { w = parseFloat(m[1]) * 96 / 72; h = parseFloat(m[2]) * 96 / 72; }
  const fixed = svg.replace(/width="[\d.]+pt" height="[\d.]+pt"/, `width="${w}px" height="${h}px"`);
  const html = `<!doctype html><html><head><meta charset="utf-8"><style>html,body{margin:0;padding:0;background:#fff}svg{display:block}</style></head><body>${fixed}</body></html>`;
  const tmp = pngPath.replace(/\.png$/, '.tmp.html');
  fs.writeFileSync(tmp, html, 'utf8');
  htmlToPng(tmp, pngPath, w + 40, h + 200, scale);
  fs.unlinkSync(tmp);
  return trim(pngPath);
}

// Обрезка белых полей (окно headless-браузера берём с запасом)
function trim(pngPath, pad = 24) {
  const out = execFileSync('python', [path.join(__dirname, 'trim.py'), pngPath, String(pad)]).toString().trim().split(' ');
  return { w: +out[0], h: +out[1] };
}

module.exports = { htmlToPng, svgToPng, trim };
