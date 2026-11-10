// Hybrid — the combined direction: Cinema for playback, Cinema's visual language with Library's
// folder browsing for the gallery, Studio for lossless editing. One floating, auto-hiding chrome
// (title pill + Folder · Play · Edit switcher) ties the three modes together.
const fs = require('fs');
const path = require('path');
const { BRAND, icon, logo, captionButtons, scene, thumb, MEDIA, page } = require('./shared.cjs');
const cinema = require('./cinema.cjs').parts;
const studio = require('./studio.cjs').parts;

const HY = `
.acrylic{background:rgba(26,28,32,.58);backdrop-filter:blur(28px) saturate(150%);border:1px solid rgba(255,255,255,.09);
  box-shadow:0 18px 50px rgba(0,0,0,.45),inset 0 1px 0 rgba(255,255,255,.06)}
.hy-wall{position:absolute;inset:0;background:radial-gradient(900px 420px at 15% -8%,rgba(255,94,126,.16),transparent 70%),
  radial-gradient(900px 500px at 95% -10%,rgba(142,16,53,.20),transparent 70%),#0b0c0f}
.hy-title{position:absolute;top:9px;inset-inline-start:12px;height:34px;display:flex;align-items:center;gap:10px;padding:0 14px 0 10px;border-radius:17px;z-index:20;font-size:13.5px}
html[dir=rtl] .hy-title{padding:0 10px 0 14px}
.hy-title b{font-weight:600}.hy-title .c{color:rgba(255,255,255,.6)}
.hy-modes{position:absolute;top:7px;left:50%;transform:translateX(-50%);display:flex;gap:2px;padding:4px;border-radius:21px;z-index:20}
.hy-modes span{height:30px;padding:0 15px;border-radius:15px;display:flex;align-items:center;gap:7px;font-size:13px;font-weight:500;color:rgba(255,255,255,.72)}
.hy-modes span.on{background:${BRAND};color:#fff;box-shadow:0 3px 14px rgba(230,34,63,.45),inset 0 1px 0 rgba(255,255,255,.25)}
.hy-caps{position:absolute;top:0;inset-inline-end:0;z-index:20}
.hy-search{position:absolute;top:9px;inset-inline-end:156px;height:34px;width:250px;border-radius:17px;display:flex;align-items:center;gap:9px;padding:0 14px;
  font-size:13px;color:rgba(255,255,255,.55);z-index:20}
`;

const MODES = { en: [['folder', 'Folder'], ['play', 'Play'], ['scissors', 'Edit']], ar: [['folder', 'المجلد'], ['play', 'تشغيل'], ['scissors', 'تحرير']] };

function chrome({ mode, title, ctx, rtl = false, search = null, logoOnly = false }) {
  const m = MODES[rtl ? 'ar' : 'en'];
  return `<div class="hy-title acrylic">${logo(18)}${logoOnly ? `<b>${title}</b>` : `<b>${title}</b><span class="c">${ctx}</span>`}</div>
    <div class="hy-modes acrylic">${m.map(([i, l], k) => `<span class="${k === mode ? 'on' : ''}">${icon(i, 's14')}${l}</span>`).join('')}</div>
    ${search ? `<div class="hy-search acrylic">${icon('search', 's16')}${search}</div>` : ''}
    <div class="hy-caps">${captionButtons(44)}</div>`;
}

// ---- a · playback = Cinema -------------------------------------------------------------------
function player() {
  return page({
    css: cinema.CSS + HY, body: `
    <div class="stage">${cinema.FRAME}</div><div class="scrim-t"></div><div class="scrim-b"></div>
    ${chrome({ mode: 1, title: 'Rub al Khali drone.mp4', ctx: '3 of 42 · Desert trip' })}
    <div class="chip acrylic" style="top:56px;left:12px"><span class="dot"></span>Hardware decoding · D3D11VA · 4K HDR</div>
    <div class="edge acrylic" style="left:18px">${icon('chevL', 's24')}</div><div class="edge acrylic" style="right:18px">${icon('chevR', 's24')}</div>
    <div class="sub"><span>And there, beyond the dunes, the old city was still asleep.</span></div>
    <div class="dock acrylic">${cinema.seekBar({})}${cinema.controls()}</div>`
  });
}

// ---- b · folder = Cinema look + Library browsing --------------------------------------------
const GCSS = `
.pane{position:absolute;top:56px;bottom:12px;inset-inline-start:12px;width:250px;border-radius:16px;padding:8px;display:flex;flex-direction:column;gap:1px;z-index:5}
.pane .it{height:36px;display:flex;align-items:center;gap:11px;padding:0 11px;border-radius:9px;font-size:13.5px;color:rgba(255,255,255,.86);position:relative}
.pane .it .c{margin-inline-start:auto;font-size:12px;color:rgba(255,255,255,.42)}
.pane .it.on{background:rgba(255,255,255,.09)}
.pane .it.on::before{content:"";position:absolute;inset-inline-start:0;top:9px;bottom:9px;width:3px;border-radius:2px;background:${BRAND}}
.pane .h{font-size:11.5px;font-weight:600;color:rgba(255,255,255,.45);padding:12px 11px 5px;letter-spacing:.04em;text-transform:uppercase}
html[dir=rtl] .pane .h{text-transform:none;letter-spacing:0;font-size:12.5px}
.pane .foot{margin-top:auto;display:flex;align-items:center;gap:8px;padding:10px 11px 4px;font-size:12px;color:rgba(255,255,255,.45);border-top:1px solid rgba(255,255,255,.07)}
.content{position:absolute;top:56px;bottom:0;inset-inline-start:278px;inset-inline-end:24px}
.ghd{display:flex;align-items:flex-end;gap:12px;padding-top:6px}
.ghd h1{font-size:34px;font-weight:700;letter-spacing:-.02em;line-height:1.15}
html[dir=rtl] .ghd h1{letter-spacing:0}
.ghd .meta{font-size:13.5px;color:rgba(255,255,255,.58)}
.crumb{font-size:13px;color:rgba(255,255,255,.55);display:flex;align-items:center;gap:4px;margin-bottom:2px}
.seg{display:flex;padding:3px;border-radius:10px;background:rgba(255,255,255,.06);border:1px solid rgba(255,255,255,.08)}
.seg span{height:30px;padding:0 13px;display:flex;align-items:center;gap:6px;border-radius:7px;font-size:13px;font-weight:500;color:rgba(255,255,255,.75)}
.seg span b{font-weight:500;color:rgba(255,255,255,.45)}
.seg span.on{background:rgba(255,255,255,.12);color:#fff}
.grp{margin-top:22px}
.grp h3{font-size:15px;font-weight:600;margin-bottom:10px;display:flex;align-items:center;gap:10px}
.grp h3 span{font-weight:400;color:rgba(255,255,255,.5);font-size:13px;display:flex;align-items:center;gap:4px}
.jrow{display:flex;gap:8px;margin-bottom:8px}
.jt{border-radius:10px;overflow:hidden;position:relative;flex:none}
.jt .b{position:absolute;inset-inline-start:8px;bottom:8px}
.jt .ck{position:absolute;top:8px;inset-inline-start:8px}
.jt.sel{box-shadow:0 0 0 2px #0b0c0f,0 0 0 4px #FF5E7E}
.jt.sel>.thumb{opacity:.82}
.jt.focus{transform:scale(1.04);z-index:2;box-shadow:0 0 0 3px #0b0c0f,0 0 0 6px #FF5E7E,0 24px 50px rgba(0,0,0,.7)}
.jt .cap{position:absolute;left:0;right:0;bottom:0;padding:30px 12px 10px;background:linear-gradient(rgba(0,0,0,0),rgba(0,0,0,.78));font-size:12.5px}
.jt .cap b{display:block;font-weight:600;font-size:13.5px}
.fade{position:absolute;left:0;right:0;bottom:0;height:150px;background:linear-gradient(rgba(11,12,15,0),#0b0c0f 90%);z-index:3}
.selbar{position:absolute;bottom:24px;display:flex;align-items:center;gap:4px;height:52px;padding:0 8px 0 18px;border-radius:26px;z-index:6;font-size:14px}
html[dir=rtl] .selbar{padding:0 18px 0 8px}
.selbar .btn{height:36px;border-radius:18px}
`;

function justify(items, width, h, gap = 8) {
  const rows = [];
  let cur = [];
  for (const it of items) {
    cur.push(it);
    const sum = cur.reduce((s, x) => s + x.a, 0);
    if (sum * h + gap * (cur.length - 1) >= width) { rows.push(cur); cur = []; }
  }
  if (cur.length) { if (cur.length < 3 && rows.length) rows[rows.length - 1].push(...cur); else rows.push(cur); }
  return rows.map(row => ({ row, rh: (width - gap * (row.length - 1)) / row.reduce((s, x) => s + x.a, 0) }));
}

function gallery({ rtl = false } = {}) {
  const T = rtl ? {
    home: 'الرئيسية', recent: 'الأخيرة', fav: 'المفضلة', folders: 'المجلدات', pics: 'الصور', travel: 'السفر', desert: 'رحلة الصحراء',
    family: 'العائلة 2026', ist: 'إسطنبول 2025', videos: 'مقاطع الفيديو', rec: 'تسجيلات الشاشة', dev: 'هذا الجهاز', set: 'الإعدادات',
    hide: 'تختفي تلقائيًا · F9 للتثبيت', crumb: ['الصور', 'السفر'], title: 'رحلة الصحراء', meta: '42 عنصرًا · 31 صورة · 11 فيديو · 3.8 GB',
    all: 'الكل', photos: 'الصور', vids: 'الفيديو', favs: 'المفضلة', sort: 'تاريخ الالتقاط', show: 'عرض الشرائح',
    g1: 'الجمعة، 7 نوفمبر 2026', p1: 'العُلا', g2: 'السبت، 8 نوفمبر 2026', p2: 'الربع الخالي', n1: '11 عنصرًا', n2: '10 عناصر',
    sel: 'تم تحديد 2', play: 'تشغيل', share: 'مشاركة', edit: 'تحرير', del: 'حذف', search: 'البحث في رحلة الصحراء', app: 'مشهد',
    capMeta: '3840 × 2160 · 6.4 MB', mixed: 'صور وفيديو معًا',
  } : {
    home: 'Home', recent: 'Recent', fav: 'Favorites', folders: 'Folders', pics: 'Pictures', travel: 'Travel', desert: 'Desert trip',
    family: 'Family 2026', ist: 'Istanbul 2025', videos: 'Videos', rec: 'Screen recordings', dev: 'This PC', set: 'Settings',
    hide: 'Auto-hides · F9 to pin', crumb: ['Pictures', 'Travel'], title: 'Desert trip', meta: '42 items · 31 photos · 11 videos · 3.8 GB',
    all: 'All', photos: 'Photos', vids: 'Videos', favs: 'Favorites', sort: 'Date taken', show: 'Slideshow',
    g1: 'Friday, 7 November 2026', p1: 'AlUla', g2: 'Saturday, 8 November 2026', p2: 'Empty Quarter', n1: '11 items', n2: '10 items',
    sel: '2 selected', play: 'Play', share: 'Share', edit: 'Edit', del: 'Delete', search: 'Search Desert trip', app: 'Mashhad',
    capMeta: '3840 × 2160 · 6.4 MB', mixed: 'Photos and videos together',
  };
  const W = 1600 - 278 - 24;
  let idx = 0;
  const tiles = (rows) => rows.map(({ rh, row }) => `<div class="jrow">${row.map(m => {
    const i = idx++;
    const w = m.a * rh, sel = i === 1 || i === 4, focus = i === 6;
    const badge = m.v ? `<span class="badge b">${icon('play', 's14')}<span class="num">${m.v}</span></span>` : m.gif ? '<span class="badge b">GIF</span>' : '';
    const ck = sel ? `<span class="chk on ck">${icon('check', 's14', 'stroke-width="2.4"')}</span>` : '';
    const name = rtl && m.ar ? m.ar : m.n;
    const cap = focus ? `<div class="cap"><b>${name}</b><span class="t2 num">${T.capMeta}</span></div>` : badge;
    return `<div class="jt ${sel ? 'sel' : ''} ${focus ? 'focus' : ''}" style="width:${w.toFixed(1)}px;height:${rh.toFixed(1)}px">${thumb(m.k, m.s, Math.round(w), Math.round(rh), '', 'position:absolute;inset:0;width:100%;height:100%')}${cap}${ck}</div>`;
  }).join('')}</div>`).join('');
  const chev = icon(rtl ? 'chevL' : 'chevR', 's14');
  const it = (ic, label, c = '', cls = '', pad = '') => `<div class="it ${cls}" style="${pad}">${icon(ic, 's18')}${label}${c ? `<span class="c num">${c}</span>` : ''}</div>`;
  return page({
    dir: rtl ? 'rtl' : 'ltr', css: cinema.CSS + HY + GCSS, body: `<div class="hy-wall"></div>
    ${chrome({ mode: 0, title: T.app, logoOnly: true, rtl, search: T.search })}
    <div class="pane acrylic">
      ${it('home', T.home)}${it('clock', T.recent)}${it('heart', T.fav, '6')}
      <div class="h">${T.folders}</div>
      ${it('pictures', T.pics, '1,204')}${it('folder', T.travel, '', '', 'padding-inline-start:28px')}
      ${it('folder', T.desert, '42', 'on', 'padding-inline-start:44px')}${it('folder', T.ist, '96', '', 'padding-inline-start:44px')}
      ${it('folder', T.family, '', '', 'padding-inline-start:28px')}${it('video', T.videos, '318')}${it('folder', T.rec, '', '', 'padding-inline-start:28px')}
      <div class="h"></div>${it('device', T.dev)}${it('settings', T.set)}
      <div class="foot">${icon('sidebar', 's16')}${T.hide}</div></div>
    <div class="content">
      <div class="ghd"><div style="flex:1"><div class="crumb">${T.crumb[0]}${chev}${T.crumb[1]}${chev}</div><h1>${T.title}</h1><div class="meta">${T.meta}</div></div>
        <div class="seg"><span class="on">${T.all} <b class="num">42</b></span><span>${icon('image', 's16')}${T.photos}</span><span>${icon('video', 's16')}${T.vids}</span><span>${icon('heart', 's16')}${T.favs}</span></div>
        <span class="btn subtle">${icon('sort', 's18')}${T.sort}</span>
        <span class="btn accent" style="height:36px;padding:0 16px;border-radius:18px">${icon('slideshow', 's18')}${T.show}</span></div>
      <div class="grp"><h3>${T.g1}<span>${icon('pin', 's14')}${T.p1} · ${T.n1}</span></h3>${tiles(justify(MEDIA.slice(0, 11), W, 182))}</div>
      <div class="grp"><h3>${T.g2}<span>${icon('pin', 's14')}${T.p2} · ${T.n2}</span></h3>${tiles(justify(MEDIA.slice(11, 21), W, 182))}</div>
    </div>
    <div class="fade"></div>
    <div class="selbar acrylic" style="left:${rtl ? 'auto' : 'calc(278px + 50% - 139px)'};right:${rtl ? 'calc(278px + 50% - 139px)' : 'auto'};transform:translateX(${rtl ? '50%' : '-50%'})">
      <span style="font-weight:600;margin-inline-end:10px">${T.sel}</span>
      <span class="btn accent">${icon('play', 's16')}${T.play}</span><span class="btn subtle">${icon('scissors', 's16')}${T.edit}</span>
      <span class="btn subtle">${icon('share', 's16')}${T.share}</span><span class="btn subtle">${icon('trash', 's16')}${T.del}</span>
      <span class="btn subtle icon">${icon('close', 's16')}</span></div>`
  });
}

// ---- c · edit = Studio, wearing the same floating chrome ------------------------------------
const ECSS = `
body{background:#0b0c0f}
.panel,.tl,.transport{border-radius:14px;background:rgba(22,24,28,.78)!important;backdrop-filter:blur(24px);border:1px solid rgba(255,255,255,.08)!important;overflow:hidden;
  box-shadow:0 14px 40px rgba(0,0,0,.35)}
.preview{border-radius:14px;box-shadow:0 14px 40px rgba(0,0,0,.45)}
.ph{border-bottom-color:rgba(255,255,255,.07)!important}
.tlbar{border-bottom-color:rgba(255,255,255,.07)!important}
.trk .hdr{background:rgba(255,255,255,.025)!important;border-color:rgba(255,255,255,.05)!important}
.trk .lane{border-color:rgba(255,255,255,.04)!important}
.binrow{border-bottom-color:rgba(255,255,255,.035)}
.binrow.on{background:rgba(255,94,126,.12);box-shadow:inset 3px 0 0 #FF5E7E}
html[dir=rtl] .binrow.on{box-shadow:inset -3px 0 0 #FF5E7E}
.tool{background:rgba(255,255,255,.05);border-color:rgba(255,255,255,.08)}
.tabs span.on::after{left:18%;right:18%}
`;

function edit({ rtl = false } = {}) {
  const g = 10, top = 56, tlH = 292, binW = 236, inspW = 352;
  const tlY = 1000 - g - tlH;
  const upH = tlY - g - top;
  const prevX = g + binW + g, prevW = 1600 - prevX - g - inspW - g, prevH = upH - 56 - 8;
  const L = (x, w) => rtl ? 1600 - x - w : x;
  return page({
    dir: rtl ? 'rtl' : 'ltr', css: studio.CSS + HY + ECSS, body: `<div class="hy-wall" style="opacity:.7"></div>
    ${chrome({ mode: 2, title: rtl ? 'الربع الخالي - تصوير جوي.mp4' : 'Rub al Khali drone.mp4', ctx: rtl ? 'تحرير بلا فقد' : 'Lossless edit', rtl })}
    ${studio.bin({ x: L(g, binW), y: top, w: binW, h: upH, rtl })}
    <div class="preview" style="left:${L(prevX, prevW)}px;top:${top}px;width:${prevW}px;height:${prevH}px">${scene('dunes', 11, 1.8)}
      <div class="ovl" style="top:10px;${rtl ? 'right' : 'left'}:10px"><span class="num">3840×2160</span><span class="num">HEVC · HDR10</span><span class="num">29.97 fps</span></div>
      <div class="ovl" style="top:10px;${rtl ? 'left' : 'right'}:10px"><span style="background:rgba(230,34,63,.85);color:#fff">${icon('scissors', 's14')}${rtl ? 'المقطع 2 من 3' : 'Segment 2 of 3'}</span></div></div>
    ${studio.transport({ x: L(prevX, prevW), y: top + prevH + 8, w: prevW, rtl, mode: 2 })}
    <div class="panel" style="left:${L(1600 - g - inspW, inspW)}px;top:${top}px;width:${inspW}px;height:${upH}px">${studio.inspectorCut(rtl)}</div>
    ${studio.timeline({ x: g, y: tlY, w: 1600 - 2 * g, h: tlH, mode: 2, rtl })}`
  });
}

// ---- e · how the modes connect ---------------------------------------------------------------
function storyboard(dir) {
  const img = (f) => `data:image/png;base64,${fs.readFileSync(path.join(dir, f)).toString('base64')}`;
  const card = (f, t, d) => `<div class="sb"><img src="${img(f)}"><b>${t}</b><span>${d}</span></div>`;
  const arrow = (t, d) => `<div class="ar"><div class="ln">${icon('fwd', 's24')}</div><b>${t}</b><span>${d}</span></div>`;
  const css = `body{background:radial-gradient(1000px 500px at 50% -10%,rgba(230,34,63,.16),transparent 70%),#0b0c0f;padding:46px 44px}
  h1{font-size:30px;font-weight:700;letter-spacing:-.02em}h1+p{color:rgba(255,255,255,.6);margin:6px 0 30px;font-size:15px}
  .row{display:flex;align-items:flex-start;gap:14px}
  .sb{width:400px;flex:none}.sb img{width:400px;height:250px;border-radius:12px;display:block;box-shadow:0 0 0 1px rgba(255,255,255,.1),0 18px 40px rgba(0,0,0,.5)}
  .sb b{display:block;margin-top:12px;font-size:16px}.sb span{display:block;color:rgba(255,255,255,.6);font-size:13px;margin-top:3px;line-height:1.45}
  .ar{width:120px;flex:none;padding-top:86px;text-align:center}.ar .ln{width:52px;height:52px;margin:0 auto 10px;border-radius:50%;background:${BRAND};display:flex;align-items:center;justify-content:center;box-shadow:0 6px 20px rgba(230,34,63,.45)}
  .ar b{display:block;font-size:13px}.ar span{display:block;font-size:11.5px;color:rgba(255,255,255,.55);margin-top:4px;line-height:1.4}
  .tokens{display:grid;grid-template-columns:repeat(4,1fr);gap:14px;margin-top:40px}
  .tk{border-radius:14px;padding:16px 18px;background:rgba(255,255,255,.04);border:1px solid rgba(255,255,255,.08)}
  .tk b{display:flex;align-items:center;gap:8px;font-size:14px}.tk p{font-size:12.5px;color:rgba(255,255,255,.62);margin-top:6px;line-height:1.5}
  .tk code{font-family:"JetBrains Mono",monospace;font-size:11.5px;color:#ffa8b8}
  .low{display:flex;gap:28px;margin-top:34px;align-items:flex-start}
  .low h2{font-size:17px;font-weight:600;margin-bottom:12px}
  .keys{flex:1;display:grid;grid-template-columns:1fr 1fr;gap:10px 28px}
  .k{display:flex;align-items:center;gap:10px;font-size:13.5px;color:rgba(255,255,255,.82);height:34px}
  .k .ks{display:flex;gap:4px;min-width:120px}
  .rtl{width:420px;flex:none}.rtl img{width:420px;height:263px;border-radius:12px;display:block;box-shadow:0 0 0 1px rgba(255,255,255,.1),0 18px 40px rgba(0,0,0,.5)}
  .rtl span{display:block;color:rgba(255,255,255,.6);font-size:12.5px;margin-top:8px;line-height:1.45}`;
  return page({
    css, body: `<h1>Hybrid · one app, three modes</h1><p>The floating title pill and the Folder · Play · Edit switcher are the only chrome that never changes; everything else morphs between modes.</p>
    <div class="row">
      ${card('b-folder-gallery.png', 'Folder', 'Library-style browsing, Cinema look. Enter or double-click opens.')}
      ${arrow('Open', 'tile expands into the window · 300 ms')}
      ${card('a-video-player.png', 'Play', 'Cinema. Chrome fades after 2.5 s idle; Esc goes back to the tile.')}
      ${arrow('Edit · Ctrl+E', 'video shrinks into the preview, timeline rises · 350 ms')}
      ${card('c-lossless-trim.png', 'Edit', 'Studio panels on floating cards. Playback continues where it was.')}
    </div>
    <div class="tokens">
      <div class="tk"><b>${icon('fullscreen', 's18')}Connected expand</b><p>Gallery tile → full window and back (same element, no cut). <code>300 ms · cubic-bezier(0.1, 0.9, 0.2, 1)</code></p></div>
      <div class="tk"><b>${icon('layers', 's18')}Morph into panels</b><p>Player → Edit: frame scales into the preview; bin, inspector and timeline slide in from their edges. <code>350 ms · decelerate</code></p></div>
      <div class="tk"><b>${icon('clock', 's18')}Auto-hiding chrome</b><p>Dock, title pill, mode switcher and folder pane fade out on idle and back on pointer move. <code>fade 200 ms after 2.5 s</code></p></div>
      <div class="tk"><b>${icon('video', 's18')}One video surface</b><p>The same libmpv surface is resized between Play and Edit, so playback never restarts or flashes black.</p></div>
    </div>
    <div class="low"><div style="flex:1"><h2>Moving between modes</h2><div class="keys">
      ${[[['Enter'], 'Folder → Play (open the focused item)'], [['Esc'], 'Play → Folder (shrinks back into its tile)'],
        [['Ctrl', 'E'], 'Play ⇄ Edit (same file, same position)'], [['←', '→'], 'Previous / next item, in every mode'],
        [['F'], 'Full screen: all chrome auto-hides'], [['F9'], 'Pin / unpin the folder pane'],
        [['Space'], 'Play / pause; in Folder: quick peek'], [['Ctrl', 'Enter'], 'Edit: export segments losslessly']]
        .map(([ks, d]) => `<div class="k"><span class="ks">${ks.map(k => `<span class="kbd">${k}</span>`).join('')}</span>${d}</div>`).join('')}
    </div></div>
    <div class="rtl"><img src="${img('d-folder-gallery-ar-rtl.png')}"><span>Arabic: panes, chrome and the mode switcher mirror; media time still runs left → right.</span></div></div>`
  });
}

const OUT = path.resolve(__dirname, '..', 'hybrid');
module.exports = {
  name: 'hybrid',
  screens: [
    ['a-video-player.png', () => player()],
    ['b-folder-gallery.png', () => gallery()],
    ['c-lossless-trim.png', () => edit()],
    ['d-folder-gallery-ar-rtl.png', () => gallery({ rtl: true })],
    ['e-mode-transitions.png', () => storyboard(OUT)],
  ],
};
