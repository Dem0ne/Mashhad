// Shared building blocks for the Mashhad UI concept mockups: tokens, icons, illustrated
// "media" (procedural SVG scenes standing in for photos and video frames), and chrome.
const fs = require('fs');
const path = require('path');

const ROOT = path.resolve(__dirname, '../../../..');
const LOGO = fs.readFileSync(path.join(ROOT, 'assets/brand/mashhad-logo.svg'), 'utf8')
  .replace(/<title>.*?<\/title>/, '')
  .replace('width="512" height="512"', 'width="100%" height="100%"');

let uid = 0;
const nextId = (p) => `${p}${++uid}`;

function rng(seed) {
  let s = (seed * 2654435761) >>> 0 || 1;
  return () => {
    s ^= s << 13; s >>>= 0; s ^= s >>> 17; s ^= s << 5; s >>>= 0;
    return (s % 1000003) / 1000003;
  };
}

// ---------------------------------------------------------------------------------------------
// Design tokens (Fluent dark + brand)
// ---------------------------------------------------------------------------------------------
const BRAND = 'linear-gradient(135deg,#2FD8B8 0%,#16A3D9 50%,#2F5BE3 100%)';

const BASE_CSS = `
*{box-sizing:border-box;margin:0;padding:0}
html,body{width:1600px;height:1000px;overflow:hidden}
body{background:#121417;color:#fff;font-family:Inter,"Noto Sans Arabic","Noto Sans",sans-serif;font-size:14px;
  -webkit-font-smoothing:antialiased;letter-spacing:-0.005em;position:relative}
html[dir=rtl] body{font-family:"Noto Sans Arabic","Noto Kufi Arabic",Inter,sans-serif;letter-spacing:0}
.num,.tc{font-variant-numeric:tabular-nums;font-family:Inter,sans-serif;letter-spacing:0;direction:ltr;unicode-bidi:isolate}
.ltr{direction:ltr;unicode-bidi:isolate}
.ic{width:20px;height:20px;flex:none;display:block}
.ic.s16{width:16px;height:16px}.ic.s14{width:14px;height:14px}.ic.s18{width:18px;height:18px}.ic.s24{width:24px;height:24px}
.t2{color:rgba(255,255,255,.72)}.t3{color:rgba(255,255,255,.5)}
.brand-text{background:${BRAND};-webkit-background-clip:text;background-clip:text;color:transparent}
.logo{display:block;flex:none}
.logo svg{display:block;width:100%;height:100%}
.caps{display:flex;height:100%;align-items:stretch}
.caps i{width:46px;display:flex;align-items:center;justify-content:center;color:rgba(255,255,255,.85)}
.kbd{display:inline-flex;align-items:center;justify-content:center;min-width:22px;height:22px;padding:0 6px;border-radius:5px;
  background:rgba(255,255,255,.08);border:1px solid rgba(255,255,255,.12);border-bottom-width:2px;font-size:11.5px;font-weight:600;
  color:rgba(255,255,255,.85);font-family:Inter,sans-serif}
.btn{display:inline-flex;align-items:center;gap:8px;height:32px;padding:0 12px;border-radius:6px;font-size:13.5px;font-weight:500;
  background:rgba(255,255,255,.06);border:1px solid rgba(255,255,255,.08);color:#fff;white-space:nowrap}
.btn.subtle{background:transparent;border-color:transparent}
.btn.accent{background:${BRAND};border:0;color:#fff;font-weight:600;box-shadow:0 4px 18px rgba(22,163,217,.35),inset 0 1px 0 rgba(255,255,255,.25)}
.btn.icon{width:36px;padding:0;justify-content:center}
.toggle{width:40px;height:20px;border-radius:10px;position:relative;flex:none;border:1px solid rgba(255,255,255,.55)}
.toggle::after{content:"";position:absolute;top:3px;left:3px;width:12px;height:12px;border-radius:50%;background:rgba(255,255,255,.8)}
.toggle.on{background:${BRAND};border:0}
.toggle.on::after{left:23px;top:4px;background:#fff}
html[dir=rtl] .toggle::after{left:auto;right:3px}
html[dir=rtl] .toggle.on::after{left:auto;right:23px}
.badge{display:inline-flex;align-items:center;gap:4px;height:20px;padding:0 7px;border-radius:5px;font-size:11px;font-weight:600;
  background:rgba(0,0,0,.55);backdrop-filter:blur(6px);color:#fff;font-family:Inter,sans-serif}
.thumb{position:relative;overflow:hidden;background:#222}
.thumb>svg{position:absolute;inset:0;width:100%;height:100%;display:block}
.sep{width:1px;align-self:stretch;background:rgba(255,255,255,.09);margin:4px 4px}
.chk{width:20px;height:20px;border-radius:50%;border:1.5px solid rgba(255,255,255,.9);background:rgba(0,0,0,.25)}
.chk.on{background:${BRAND};border:0;display:flex;align-items:center;justify-content:center}
`;

// ---------------------------------------------------------------------------------------------
// Icons (Fluent-like outline, 24 grid)
// ---------------------------------------------------------------------------------------------
const P = {
  play: '<path d="M8 5.2v13.6a.8.8 0 0 0 1.2.7l10.6-6.8a.8.8 0 0 0 0-1.4L9.2 4.5A.8.8 0 0 0 8 5.2z" fill="currentColor" stroke="none"/>',
  pause: '<rect x="6.5" y="5" width="4" height="14" rx="1.2" fill="currentColor" stroke="none"/><rect x="13.5" y="5" width="4" height="14" rx="1.2" fill="currentColor" stroke="none"/>',
  prev: '<path d="M6 5v14"/><path d="M18.5 5.6v12.8a.6.6 0 0 1-.9.5L9 12.5a.6.6 0 0 1 0-1l8.6-6.4a.6.6 0 0 1 .9.5z" fill="currentColor" stroke="none"/>',
  next: '<path d="M18 5v14"/><path d="M5.5 5.6v12.8a.6.6 0 0 0 .9.5L15 12.5a.6.6 0 0 0 0-1L6.4 5.1a.6.6 0 0 0-.9.5z" fill="currentColor" stroke="none"/>',
  back10: '<path d="M4.5 12a7.5 7.5 0 1 0 2.2-5.3"/><path d="M4.5 4v4h4"/><text x="12.3" y="15.3" font-size="7" font-family="Inter" font-weight="700" text-anchor="middle" fill="currentColor" stroke="none">10</text>',
  fwd10: '<path d="M19.5 12a7.5 7.5 0 1 1-2.2-5.3"/><path d="M19.5 4v4h-4"/><text x="11.7" y="15.3" font-size="7" font-family="Inter" font-weight="700" text-anchor="middle" fill="currentColor" stroke="none">10</text>',
  volume: '<path d="M4 9.5v5h3.5L12 18.5v-13L7.5 9.5z"/><path d="M15.5 9a4 4 0 0 1 0 6"/><path d="M18 6.5a7.5 7.5 0 0 1 0 11"/>',
  cc: '<rect x="3" y="5.5" width="18" height="13" rx="2.5"/><path d="M10.5 10.2a2.2 2.2 0 1 0 0 3.6"/><path d="M17 10.2a2.2 2.2 0 1 0 0 3.6"/>',
  audio: '<path d="M4 14v-2a8 8 0 0 1 16 0v2"/><rect x="3.5" y="13.5" width="4" height="6" rx="1.5"/><rect x="16.5" y="13.5" width="4" height="6" rx="1.5"/>',
  speed: '<path d="M4.6 17.5a8.5 8.5 0 1 1 14.8 0"/><path d="M12 13.5l4.5-4.5"/><circle cx="12" cy="13.5" r="1.3" fill="currentColor"/>',
  fullscreen: '<path d="M4 9V5a1 1 0 0 1 1-1h4"/><path d="M15 4h4a1 1 0 0 1 1 1v4"/><path d="M20 15v4a1 1 0 0 1-1 1h-4"/><path d="M9 20H5a1 1 0 0 1-1-1v-4"/>',
  more: '<circle cx="5.5" cy="12" r="1.5" fill="currentColor" stroke="none"/><circle cx="12" cy="12" r="1.5" fill="currentColor" stroke="none"/><circle cx="18.5" cy="12" r="1.5" fill="currentColor" stroke="none"/>',
  folder: '<path d="M3 7.5A2.5 2.5 0 0 1 5.5 5h3.6l2.2 2.2h7.2A2.5 2.5 0 0 1 21 9.7v7.8a2.5 2.5 0 0 1-2.5 2.5h-13A2.5 2.5 0 0 1 3 17.5z"/>',
  image: '<rect x="3" y="4.5" width="18" height="15" rx="2.5"/><circle cx="8.5" cy="9.5" r="1.6"/><path d="M3.5 17l5-5 4 4 3-3 5 5"/>',
  video: '<rect x="2.5" y="6" width="13" height="12" rx="2.5"/><path d="M15.5 10.5l5-3v9l-5-3"/>',
  grid: '<rect x="4" y="4" width="7" height="7" rx="1.5"/><rect x="13" y="4" width="7" height="7" rx="1.5"/><rect x="4" y="13" width="7" height="7" rx="1.5"/><rect x="13" y="13" width="7" height="7" rx="1.5"/>',
  list: '<path d="M9 6h11M9 12h11M9 18h11"/><circle cx="5" cy="6" r="1" fill="currentColor"/><circle cx="5" cy="12" r="1" fill="currentColor"/><circle cx="5" cy="18" r="1" fill="currentColor"/>',
  info: '<circle cx="12" cy="12" r="8.5"/><path d="M12 11v5.5"/><circle cx="12" cy="7.8" r="1" fill="currentColor" stroke="none"/>',
  scissors: '<circle cx="6.5" cy="7" r="2.5"/><circle cx="6.5" cy="17" r="2.5"/><path d="M8.6 8.4L20 17.5"/><path d="M8.6 15.6L20 6.5"/>',
  plus: '<path d="M12 5v14M5 12h14"/>',
  split: '<path d="M12 3.5v17"/><path d="M8 8.5L4.5 12 8 15.5"/><path d="M16 8.5l3.5 3.5-3.5 3.5"/>',
  trash: '<path d="M4.5 7h15"/><path d="M9.5 7V5a1 1 0 0 1 1-1h3a1 1 0 0 1 1 1v2"/><path d="M6.5 7l.9 11.6a1.5 1.5 0 0 0 1.5 1.4h6.2a1.5 1.5 0 0 0 1.5-1.4L17.5 7"/>',
  export: '<path d="M12 15V3.5"/><path d="M7.5 8L12 3.5 16.5 8"/><path d="M5 13v5.5A1.5 1.5 0 0 0 6.5 20h11a1.5 1.5 0 0 0 1.5-1.5V13"/>',
  rotate: '<path d="M19.5 12a7.5 7.5 0 1 1-2.2-5.3"/><path d="M19.5 4v4h-4"/>',
  crop: '<path d="M7 3v13a1 1 0 0 0 1 1h13"/><path d="M3 7h13a1 1 0 0 1 1 1v13"/>',
  zoomin: '<circle cx="10.5" cy="10.5" r="6.5"/><path d="M15.5 15.5L20.5 20.5"/><path d="M10.5 8v5M8 10.5h5"/>',
  zoomout: '<circle cx="10.5" cy="10.5" r="6.5"/><path d="M15.5 15.5L20.5 20.5"/><path d="M8 10.5h5"/>',
  search: '<circle cx="10.5" cy="10.5" r="6.5"/><path d="M15.5 15.5L20.5 20.5"/>',
  star: '<path d="M12 3.8l2.5 5.1 5.6.8-4 4 .9 5.6-5-2.7-5 2.7.9-5.6-4-4 5.6-.8z"/>',
  clock: '<circle cx="12" cy="12" r="8.5"/><path d="M12 7.5V12l3 2"/>',
  home: '<path d="M4 10.5L12 4l8 6.5V19a1 1 0 0 1-1 1h-4.5v-5.5h-5V20H5a1 1 0 0 1-1-1z"/>',
  filter: '<path d="M4 5.5h16l-6.2 7.3V19l-3.6-1.8v-4.4z"/>',
  chevR: '<path d="M9.5 6l6 6-6 6"/>',
  chevL: '<path d="M14.5 6l-6 6 6 6"/>',
  chevD: '<path d="M6 9.5l6 6 6-6"/>',
  back: '<path d="M19.5 12h-15"/><path d="M10.5 6l-6 6 6 6"/>',
  fwd: '<path d="M4.5 12h15"/><path d="M13.5 6l6 6-6 6"/>',
  sort: '<path d="M7.5 4.5v15"/><path d="M4 16l3.5 3.5L11 16"/><path d="M16.5 19.5v-15"/><path d="M13 8l3.5-3.5L20 8"/>',
  slideshow: '<rect x="3" y="4.5" width="18" height="12.5" rx="2"/><path d="M10.5 8.3v4.9l4-2.45z" fill="currentColor"/><path d="M8 20.5h8"/>',
  loop: '<path d="M17 3l3 3-3 3"/><path d="M4 11.5V10a4 4 0 0 1 4-4h12"/><path d="M7 21l-3-3 3-3"/><path d="M20 12.5V14a4 4 0 0 1-4 4H4"/>',
  camera: '<path d="M4 8.5A1.5 1.5 0 0 1 5.5 7h2.3l1.4-2h5.6l1.4 2h2.3A1.5 1.5 0 0 1 20 8.5v9a1.5 1.5 0 0 1-1.5 1.5h-13A1.5 1.5 0 0 1 4 17.5z"/><circle cx="12" cy="12.8" r="3.4"/>',
  merge: '<path d="M6 3.5V8a6 6 0 0 0 6 6 6 6 0 0 0 6-6V3.5"/><path d="M12 14v6.5"/>',
  check: '<path d="M5 12.5l4.5 4.5L19 7.5"/>',
  close: '<path d="M6 6l12 12M18 6L6 18"/>',
  key: '<path d="M12 4.5l7.5 7.5-7.5 7.5L4.5 12z"/>',
  keyPrev: '<path d="M9 7l-5 5 5 5 5-5z"/><path d="M15.5 7v10"/><path d="M19.5 7v10"/>',
  keyNext: '<path d="M15 7l5 5-5 5-5-5z"/><path d="M8.5 7v10"/><path d="M4.5 7v10"/>',
  frameBack: '<path d="M14.5 6l-6 6 6 6"/><path d="M18.5 6v12"/>',
  frameFwd: '<path d="M9.5 6l6 6-6 6"/><path d="M5.5 6v12"/>',
  settings: '<circle cx="12" cy="12" r="3"/><path d="M12 2.8v2.4M12 18.8v2.4M2.8 12h2.4M18.8 12h2.4M5.5 5.5l1.7 1.7M16.8 16.8l1.7 1.7M5.5 18.5l1.7-1.7M16.8 7.2l1.7-1.7"/>',
  heart: '<path d="M12 19.5s-7.5-4.4-7.5-9.7A4.3 4.3 0 0 1 12 7.2a4.3 4.3 0 0 1 7.5 2.6c0 5.3-7.5 9.7-7.5 9.7z"/>',
  share: '<path d="M4.5 12.5V19a1 1 0 0 0 1 1h13a1 1 0 0 0 1-1v-6.5"/><path d="M12 3.5v11"/><path d="M8 7.5l4-4 4 4"/>',
  copy: '<rect x="8" y="8" width="12" height="12" rx="2"/><path d="M16 8V5.5A1.5 1.5 0 0 0 14.5 4h-9A1.5 1.5 0 0 0 4 5.5v9A1.5 1.5 0 0 0 5.5 16H8"/>',
  magnet: '<path d="M6 4v7.5a6 6 0 0 0 12 0V4"/><path d="M6 8.5h3.5M14.5 8.5H18"/><path d="M9.5 4v7.5a2.5 2.5 0 0 0 5 0V4"/>',
  pin: '<path d="M12 21s-6.5-5.6-6.5-11a6.5 6.5 0 0 1 13 0c0 5.4-6.5 11-6.5 11z"/><circle cx="12" cy="10" r="2.3"/>',
  layers: '<path d="M12 4l8.5 4.5L12 13 3.5 8.5z"/><path d="M3.5 12.5L12 17l8.5-4.5"/><path d="M3.5 16.5L12 21l8.5-4.5"/>',
  edit: '<path d="M4.5 19.5l1-4L15.8 5.2a2 2 0 0 1 2.9 0l.1.1a2 2 0 0 1 0 2.9L8.5 18.5z"/><path d="M14 7l3 3"/>',
  pictures: '<rect x="6.5" y="3.5" width="14" height="12" rx="2"/><path d="M3.5 7.5v10a2 2 0 0 0 2 2h11"/><path d="M8 13l3.2-3.2 2.6 2.6 1.7-1.7L18.8 14"/>',
  device: '<rect x="3" y="4.5" width="18" height="12" rx="1.5"/><path d="M9 20h6M12 16.5V20"/>',
  cloud: '<path d="M7 18.5a4.5 4.5 0 0 1-.6-9 6 6 0 0 1 11.5 1.6A3.8 3.8 0 0 1 17.5 18.5z"/>',
  wave: '<path d="M3 12h2M7 8v8M11 5v14M15 8.5v7M19 10.5v3M21 12h0"/>',
  subtitle: '<rect x="3" y="5.5" width="18" height="13" rx="2.5"/><path d="M7 14.5h4M13 14.5h4M7 11h10"/>',
  sidebar: '<rect x="3" y="4.5" width="18" height="15" rx="2.5"/><path d="M9 4.5v15"/>',
  histogram: '<path d="M4 20V10M8 20V6M12 20v-8M16 20V8M20 20v-5"/>',
  open: '<path d="M14 4.5h5.5V10"/><path d="M19.5 4.5L11 13"/><path d="M18 14v4.5a1.5 1.5 0 0 1-1.5 1.5h-11A1.5 1.5 0 0 1 4 18.5v-11A1.5 1.5 0 0 1 5.5 6H10"/>',
  dot: '<circle cx="12" cy="12" r="4" fill="currentColor" stroke="none"/>',
};
function icon(name, cls = '', extra = '') {
  return `<svg class="ic ${cls}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" ${extra}>${P[name]}</svg>`;
}

function logo(size) { return `<span class="logo" style="width:${size}px;height:${size}px">${LOGO}</span>`; }

function captionButtons(h = 40) {
  const s = 'width="10" height="10" viewBox="0 0 10 10" fill="none" stroke="currentColor" stroke-width="1"';
  return `<div class="caps" style="height:${h}px">
    <i><svg ${s}><path d="M0 5.5h10"/></svg></i>
    <i><svg ${s}><rect x=".5" y=".5" width="9" height="9" rx="1.2"/></svg></i>
    <i><svg ${s}><path d="M.5.5l9 9M9.5.5l-9 9"/></svg></i></div>`;
}

// ---------------------------------------------------------------------------------------------
// Illustrated media: procedural scenes instead of photos (deterministic per seed)
// ---------------------------------------------------------------------------------------------
function smooth(pts) {
  let d = `M${pts[0][0].toFixed(2)},${pts[0][1].toFixed(2)}`;
  for (let i = 0; i < pts.length - 1; i++) {
    const p0 = pts[i - 1] || pts[i], p1 = pts[i], p2 = pts[i + 1], p3 = pts[i + 2] || p2;
    const c1 = [p1[0] + (p2[0] - p0[0]) / 6, p1[1] + (p2[1] - p0[1]) / 6];
    const c2 = [p2[0] - (p3[0] - p1[0]) / 6, p2[1] - (p3[1] - p1[1]) / 6];
    d += `C${c1[0].toFixed(2)},${c1[1].toFixed(2)} ${c2[0].toFixed(2)},${c2[1].toFixed(2)} ${p2[0].toFixed(2)},${p2[1].toFixed(2)}`;
  }
  return d;
}

function ridge(r, base, amp, freq, phase, step = 5, jag = 0) {
  const pts = [];
  for (let x = -10; x <= 170; x += step) {
    const y = base + amp * (Math.sin(x * freq + phase) * 0.65 + Math.sin(x * freq * 2.31 + phase * 1.7) * 0.25
      + Math.sin(x * freq * 5.1 + phase * 0.3) * 0.1) + (r() - 0.5) * jag;
    pts.push([x, y]);
  }
  return smooth(pts) + 'L170,110L-10,110Z';
}

function jaggedRidge(r, base, amp, step = 2.2) {
  let d = `M-10,${base}`;
  let y = base;
  for (let x = -10; x <= 170; x += step) {
    y += (r() - 0.5) * amp; y = Math.max(base - amp * 3, Math.min(base + amp * 3, y));
    const tip = y - (r() * amp * 1.4);
    d += `L${x.toFixed(1)},${y.toFixed(1)}L${(x + step / 2).toFixed(1)},${tip.toFixed(1)}`;
  }
  return d + 'L170,110L-10,110Z';
}

function peaks(r, base, height, n) {
  const step = 180 / n;
  let d = `M-10,${base + 4}`;
  for (let i = 0; i < n; i++) {
    const x = -10 + i * step;
    const top = base - height * (0.35 + r() * 0.65), valley = base - height * 0.12 * r();
    d += `L${(x + step * (0.4 + r() * 0.2)).toFixed(1)},${top.toFixed(1)}L${(x + step).toFixed(1)},${valley.toFixed(1)}`;
  }
  return d + 'L170,110L-10,110Z';
}

function mesas(r, base, height) {
  let d = `M-10,${base}`, x = -10;
  while (x < 170) {
    const w = 10 + r() * 28, h = r() < 0.65 ? height * (0.55 + r() * 0.45) : height * 0.12;
    d += `L${(x + 2.5).toFixed(1)},${(base - h).toFixed(1)}L${(x + w - 2.5).toFixed(1)},${(base - h).toFixed(1)}L${(x + w).toFixed(1)},${base}`;
    x += w + r() * 8;
    d += `L${x.toFixed(1)},${base}`;
  }
  return d + 'L170,110L-10,110Z';
}

const SKIES = {
  dunes: ['#2a1a4c', '#8a3f72', '#e3765c', '#f6b26b', '#ffe0a3'],
  mountains: ['#0d2742', '#2f5d8c', '#7fb0d3', '#d6e7ef'],
  city: ['#04060e', '#0c1736', '#26357a', '#7b4b8e', '#e07a7a'],
  sea: ['#16224f', '#5b4a9a', '#d9787a', '#ffc58a'],
  forest: ['#b8d4d0', '#dfeae2', '#f1f4ea'],
  aurora: ['#01040c', '#041427', '#08283a'],
  oldcity: ['#3a1d4f', '#b04f6b', '#f08a5d', '#ffd08a'],
  canyon: ['#5c86b8', '#a9c4dc', '#f2d6b3'],
  bokeh: ['#1a0f16', '#2c1520'],
  palms: ['#0f3550', '#2f7fa0', '#8fd0d8', '#f5e6c4'],
};

function stops(colors) {
  return colors.map((c, i) => `<stop offset="${(i / (colors.length - 1)).toFixed(3)}" stop-color="${c}"/>`).join('');
}

/** An illustration standing in for a photo or video frame. `t` animates it (for filmstrips). */
function scene(kind, seed = 1, t = 0) {
  const r = rng(seed * 7919 + 13);
  const id = nextId('g');
  let defs = `<linearGradient id="${id}s" x1="0" y1="0" x2="0" y2="1">${stops(SKIES[kind] || SKIES.mountains)}</linearGradient>`;
  let b = `<rect x="-10" y="-10" width="180" height="120" fill="url(#${id}s)"/>`;
  const ph = r() * 6 + t * 2.2;
  const glow = (cx, cy, rad, col, op = .55) => {
    defs += `<radialGradient id="${id}g${cx | 0}"><stop offset="0" stop-color="${col}" stop-opacity="${op}"/><stop offset="1" stop-color="${col}" stop-opacity="0"/></radialGradient>`;
    return `<circle cx="${cx}" cy="${cy}" r="${rad}" fill="url(#${id}g${cx | 0})"/>`;
  };
  switch (kind) {
    case 'dunes': {
      const sx = 30 + r() * 90 + t * 10, sy = 50 + r() * 8;
      b += glow(sx, sy, 50, '#fff1c2', .6) + `<circle cx="${sx}" cy="${sy}" r="6.5" fill="#fff6d8"/>`;
      const cols = [['#e69a5c', '#b9633a'], ['#cf7c45', '#94492a'], ['#a95a33', '#6c311c'], ['#7a3a20', '#3e1a0f']];
      cols.forEach((c, i) => {
        const gid = `${id}d${i}`;
        defs += `<linearGradient id="${gid}" x1="0" y1="0" x2="1" y2=".3"><stop offset="0" stop-color="${c[0]}"/><stop offset="1" stop-color="${c[1]}"/></linearGradient>`;
        b += `<path d="${ridge(r, 60 + i * 11, 5 + i * 1.5, 0.035 + i * 0.006, ph + i * 1.3)}" fill="url(#${gid})"/>`;
      });
      break;
    }
    case 'mountains': {
      b += glow(120 + t * 6, 30, 45, '#ffffff', .35);
      ['#8fb0cb', '#6589aa', '#42658a', '#24405f'].forEach((c, i) => {
        b += `<path d="${peaks(r, 58 + i * 10, 30 - i * 6, 4 + i * 2)}" fill="${c}" stroke="${c}" stroke-width=".6" stroke-linejoin="round"/>`;
        if (i === 0) b += `<rect x="-10" y="56" width="180" height="8" fill="#e8f0f4" opacity=".18"/>`;
      });
      b += `<rect x="-10" y="88" width="180" height="22" fill="#163049" opacity=".85"/>`;
      break;
    }
    case 'city': {
      b += `<circle cx="${120 + t * 4}" cy="20" r="5" fill="#f4f1e6"/>` + glow(120 + t * 4, 20, 22, '#f4f1e6', .35);
      for (let i = 0; i < 60; i++) b += `<circle cx="${(r() * 160).toFixed(1)}" cy="${(r() * 45).toFixed(1)}" r="${(r() * .35 + .1).toFixed(2)}" fill="#fff" opacity="${(r() * .7 + .2).toFixed(2)}"/>`;
      let x = -4 - t * 8;
      while (x < 165) {
        const w = 5 + r() * 9, h = 18 + r() * 42, tone = ['#0a0e1d', '#0d1226', '#111832'][Math.floor(r() * 3)];
        b += `<rect x="${x.toFixed(1)}" y="${(100 - h).toFixed(1)}" width="${w.toFixed(1)}" height="${h.toFixed(1)}" fill="${tone}"/>`;
        for (let wy = 100 - h + 2; wy < 98; wy += 2.6) for (let wx = x + 1; wx < x + w - 1; wx += 1.8)
          if (r() < 0.16) b += `<rect x="${wx.toFixed(1)}" y="${wy.toFixed(1)}" width=".9" height="1.1" fill="${r() < .8 ? '#ffd27a' : '#9fd9ff'}" opacity=".85"/>`;
        x += w + r() * 1.5;
      }
      break;
    }
    case 'sea': {
      const sx = 80 + (r() - .5) * 40 + t * 6;
      b += glow(sx, 60, 55, '#ffd7a0', .7) + `<circle cx="${sx}" cy="60" r="9" fill="#ffe7b8"/>`;
      defs += `<linearGradient id="${id}w" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#3b4c8f"/><stop offset="1" stop-color="#0b1233"/></linearGradient>`;
      b += `<rect x="-10" y="60" width="180" height="50" fill="url(#${id}w)"/>`;
      for (let i = 0; i < 26; i++) {
        const y = 62 + i * 1.5 + r(), w = 4 + r() * 18 * (1 - i / 30);
        b += `<rect x="${(sx - w / 2 + (r() - .5) * 6).toFixed(1)}" y="${y.toFixed(1)}" width="${w.toFixed(1)}" height=".5" rx=".25" fill="#ffd9a6" opacity="${(0.75 - i * 0.025).toFixed(2)}"/>`;
      }
      break;
    }
    case 'forest': {
      ['#a7c3b6', '#7ea694', '#557f6c', '#2f5a48', '#173a2d'].forEach((c, i) => {
        b += `<path d="${jaggedRidge(r, 45 + i * 12, 2.4)}" fill="${c}"/>`;
        if (i < 3) b += `<rect x="-10" y="${48 + i * 12}" width="180" height="10" fill="#eef3ec" opacity=".18"/>`;
      });
      break;
    }
    case 'aurora': {
      for (let i = 0; i < 90; i++) b += `<circle cx="${(r() * 160).toFixed(1)}" cy="${(r() * 70).toFixed(1)}" r="${(r() * .4 + .1).toFixed(2)}" fill="#fff" opacity="${(r() * .8 + .2).toFixed(2)}"/>`;
      defs += `<filter id="${id}b" x="-20%" y="-50%" width="140%" height="200%"><feGaussianBlur stdDeviation="3.5"/></filter>`;
      [['#2FD8B8', 30, .8], ['#47f08a', 40, .55], ['#7a5cff', 22, .45]].forEach(([c, y, o], i) => {
        const pts = []; for (let x = -10; x <= 170; x += 10) pts.push([x, y + Math.sin(x * .05 + ph + i) * 8]);
        b += `<path d="${smooth(pts)}" stroke="${c}" stroke-width="${10 - i * 2}" fill="none" opacity="${o}" filter="url(#${id}b)"/>`;
      });
      b += `<path d="${jaggedRidge(r, 82, 2.5)}" fill="#02060c"/>`;
      break;
    }
    case 'oldcity': {
      const sx = 50 + r() * 60;
      b += glow(sx, 62, 50, '#ffe2a8', .7) + `<circle cx="${sx}" cy="62" r="8" fill="#fff0c8"/>`;
      let s = '';
      let x = -5 - t * 6;
      while (x < 165) {
        const k = r();
        if (k < 0.18) { const h = 30 + r() * 20; s += `<rect x="${x}" y="${100 - h}" width="3" height="${h}"/><path d="M${x - .6},${100 - h} l2.1,-4 l2.1,4z"/><rect x="${x - .5}" y="${100 - h + 6}" width="4" height="1.2"/>`; x += 5; }
        else if (k < 0.45) { const w = 14 + r() * 10, h = 14 + r() * 8; s += `<rect x="${x}" y="${100 - h}" width="${w}" height="${h}"/><path d="M${x + w * .15},${100 - h} a${w * .35},${w * .38} 0 0 1 ${w * .7},0z"/>`; x += w + 1; }
        else { const w = 6 + r() * 12, h = 8 + r() * 12; s += `<rect x="${x}" y="${100 - h}" width="${w}" height="${h}"/>`; x += w; }
      }
      b += `<g fill="#1d0f22">${s}</g>`;
      break;
    }
    case 'canyon': {
      b += glow(40 + r() * 80, 26, 40, '#fff6e0', .5);
      ['#e7b48a', '#cf8452', '#ad5c36', '#7e3b22'].forEach((c, i) => {
        b += `<path d="${mesas(r, 60 + i * 12, 26 - i * 4)}" fill="${c}"/>`;
      });
      break;
    }
    case 'palms': {
      b += glow(110, 40, 40, '#fff4d6', .5);
      b += `<rect x="-10" y="72" width="180" height="40" fill="#e6c999"/><rect x="-10" y="66" width="180" height="7" fill="#2a8db0" opacity=".9"/>`;
      for (let i = 0; i < 4; i++) {
        const px = 18 + i * 38 + r() * 10, top = 22 + r() * 18;
        b += `<path d="M${px},86 Q${px + 3},${(86 + top) / 2} ${px + 1},${top}" stroke="#2c2016" stroke-width="1.6" fill="none"/>`;
        for (let a = 0; a < 7; a++) {
          const ang = (a / 7) * Math.PI * 2, lx = px + 1 + Math.cos(ang) * 11, ly = top + Math.sin(ang) * 5 + 3;
          b += `<path d="M${px + 1},${top} Q${(px + 1 + lx) / 2},${top - 4} ${lx.toFixed(1)},${ly.toFixed(1)}" stroke="#163b26" stroke-width="1.8" fill="none" stroke-linecap="round"/>`;
        }
      }
      break;
    }
    case 'bokeh': {
      defs += `<filter id="${id}k"><feGaussianBlur stdDeviation="1.6"/></filter>`;
      const pal = ['#ffb36b', '#ff7a8a', '#ffd88a', '#7ad6ff', '#2FD8B8'];
      let s = '';
      for (let i = 0; i < 26; i++) s += `<circle cx="${(r() * 170 - 5).toFixed(1)}" cy="${(r() * 100).toFixed(1)}" r="${(3 + r() * 11).toFixed(1)}" fill="${pal[Math.floor(r() * pal.length)]}" opacity="${(r() * .45 + .15).toFixed(2)}"/>`;
      b += `<g filter="url(#${id}k)">${s}</g>`;
      break;
    }
  }
  defs += `<radialGradient id="${id}v" cx=".5" cy=".5" r=".75"><stop offset=".55" stop-color="#000" stop-opacity="0"/><stop offset="1" stop-color="#000" stop-opacity=".38"/></radialGradient>`;
  b += `<rect x="-10" y="-10" width="180" height="120" fill="url(#${id}v)"/>`;
  return `<svg viewBox="0 0 160 100" preserveAspectRatio="xMidYMid slice"><defs>${defs}</defs>${b}</svg>`;
}

function thumb(kind, seed, w, h, extra = '', style = '', t = 0) {
  return `<div class="thumb" style="width:${w}px;height:${h}px;${style}">${scene(kind, seed, t)}${extra}</div>`;
}

/** Mirrored audio waveform as an SVG. */
function waveform(w, h, seed, color = 'rgba(255,255,255,.55)', step = 3) {
  const r = rng(seed);
  let d = '';
  let env = 0.5;
  for (let x = 0; x < w; x += step) {
    env += (r() - 0.5) * 0.25; env = Math.max(0.15, Math.min(0.95, env));
    const a = Math.max(1, env * (0.45 + r() * 0.55) * (h / 2 - 1));
    d += `M${x + .5},${(h / 2 - a).toFixed(1)}V${(h / 2 + a).toFixed(1)}`;
  }
  return `<svg width="${w}" height="${h}" viewBox="0 0 ${w} ${h}" style="display:block"><path d="${d}" stroke="${color}" stroke-width="${Math.max(1, step - 1.2)}" stroke-linecap="round"/></svg>`;
}

/** Mixed gallery for the "Desert trip" folder. */
const MEDIA = [
  { n: 'IMG_2041.HEIC', k: 'dunes', s: 3, a: 1.5 },
  { n: 'Rub al Khali drone.mp4', ar: 'الربع الخالي - تصوير جوي.mp4', k: 'dunes', s: 11, a: 1.78, v: '41:06' },
  { n: 'IMG_2043.jpg', k: 'oldcity', s: 5, a: 1.33 },
  { n: 'Old town at dusk.jpg', ar: 'البلدة القديمة عند الغروب.jpg', k: 'oldcity', s: 9, a: 0.75 },
  { n: 'Corniche timelapse.mp4', ar: 'الكورنيش - فاصل زمني.mp4', k: 'sea', s: 2, a: 1.78, v: '0:48' },
  { n: 'IMG_2050.jpg', k: 'palms', s: 4, a: 1.5 },
  { n: 'Night skyline.webp', ar: 'أفق المدينة ليلًا.webp', k: 'city', s: 8, a: 1.78 },
  { n: 'Camel crossing.gif', ar: 'عبور الجمال.gif', k: 'dunes', s: 21, a: 1.33, gif: true },
  { n: 'IMG_2058.HEIC', k: 'canyon', s: 6, a: 0.8 },
  { n: 'Wadi hike.mp4', ar: 'رحلة الوادي.mp4', k: 'canyon', s: 12, a: 1.78, v: '6:32' },
  { n: 'IMG_2061.jpg', k: 'mountains', s: 7, a: 1.5 },
  { n: 'Stars over camp.jpg', ar: 'نجوم فوق المخيم.jpg', k: 'aurora', s: 14, a: 1.5 },
  { n: 'Lanterns.jpg', ar: 'فوانيس.jpg', k: 'bokeh', s: 15, a: 1 },
  { n: 'Harbour sunset.avif', ar: 'غروب الميناء.avif', k: 'sea', s: 16, a: 1.5 },
  { n: 'Sandboarding.mp4', ar: 'التزلج على الرمال.mp4', k: 'dunes', s: 17, a: 1.78, v: '2:14' },
  { n: 'IMG_2077.jpg', k: 'forest', s: 18, a: 1.33 },
  { n: 'Oasis.png', ar: 'الواحة.png', k: 'palms', s: 19, a: 0.8 },
  { n: 'Market lights.jpg', ar: 'أضواء السوق.jpg', k: 'bokeh', s: 23, a: 1.5 },
  { n: 'Mountain road.mp4', ar: 'طريق الجبل.mp4', k: 'mountains', s: 24, a: 1.78, v: '12:09' },
  { n: 'IMG_2090.HEIC', k: 'oldcity', s: 25, a: 1.5 },
  { n: 'Dhow at sunrise.jpg', ar: 'مركب عند الشروق.jpg', k: 'sea', s: 26, a: 1.33 },
  { n: 'Northern camp.jpg', ar: 'المخيم الشمالي.jpg', k: 'aurora', s: 27, a: 1.78 },
  { n: 'Canyon drive.mp4', ar: 'القيادة في الوادي.mp4', k: 'canyon', s: 28, a: 1.78, v: '4:51' },
  { n: 'IMG_2104.jpg', k: 'city', s: 29, a: 1.33 },
];

function page({ dir = 'ltr', css = '', body = '' }) {
  return `<!doctype html><html dir="${dir}" lang="${dir === 'rtl' ? 'ar' : 'en'}"><head><meta charset="utf-8"><style>${BASE_CSS}${css}</style></head><body>${body}</body></html>`;
}

module.exports = { BRAND, icon, logo, captionButtons, scene, thumb, waveform, MEDIA, page, rng, nextId };
