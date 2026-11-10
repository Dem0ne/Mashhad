// Concept B — "Library": gallery-first, like a modern Photos app. A navigation pane of folders,
// a justified, date-grouped grid of photos and videos together, and a viewer that keeps the folder
// (filmstrip) and details in view. Editing is a mode of the viewer.
const { BRAND, icon, logo, captionButtons, scene, thumb, waveform, MEDIA, page } = require('./shared.cjs');

const CSS = `
body{background:radial-gradient(1100px 500px at 0% 0%,rgba(47,216,184,.10),transparent 60%),
  radial-gradient(1000px 600px at 100% 0%,rgba(47,91,227,.13),transparent 60%),#17191d}
.tb{position:absolute;top:0;left:0;right:0;height:48px;display:flex;align-items:center;gap:12px;padding-inline-start:16px}
.tb .app{font-size:13px;font-weight:600}
.search{position:absolute;left:50%;transform:translateX(-50%);top:8px;width:440px;height:32px;border-radius:7px;display:flex;align-items:center;gap:10px;
  padding:0 12px;background:rgba(255,255,255,.06);border:1px solid rgba(255,255,255,.08);border-bottom-color:rgba(255,255,255,.18);color:rgba(255,255,255,.55);font-size:13px}
.nav{position:absolute;top:48px;bottom:0;inset-inline-start:0;width:264px;padding:6px 8px;display:flex;flex-direction:column;gap:2px}
.nav .it{height:38px;display:flex;align-items:center;gap:12px;padding:0 12px;border-radius:6px;font-size:14px;color:rgba(255,255,255,.88);position:relative}
.nav .it .c{margin-inline-start:auto;font-size:12px;color:rgba(255,255,255,.45)}
.nav .it.on{background:rgba(255,255,255,.07)}
.nav .it.on::before{content:"";position:absolute;inset-inline-start:0;top:10px;bottom:10px;width:3px;border-radius:2px;background:${BRAND}}
.nav .h{font-size:12px;font-weight:600;color:rgba(255,255,255,.5);padding:14px 12px 6px}
.nav .sub{padding-inline-start:30px}
.rail{position:absolute;top:48px;bottom:0;inset-inline-start:0;width:56px;display:flex;flex-direction:column;align-items:center;gap:4px;padding-top:6px}
.rail i{width:40px;height:36px;border-radius:6px;display:flex;align-items:center;justify-content:center;color:rgba(255,255,255,.85);position:relative}
.rail i.on{background:rgba(255,255,255,.07)}
.rail i.on::before{content:"";position:absolute;inset-inline-start:0;top:10px;bottom:10px;width:3px;border-radius:2px;background:${BRAND}}
.card{position:absolute;top:48px;bottom:0;inset-inline-end:0;background:rgba(255,255,255,.035);border-top:1px solid rgba(255,255,255,.07);
  border-inline-start:1px solid rgba(255,255,255,.07);border-start-start-radius:10px;overflow:hidden}
.head{display:flex;align-items:flex-end;gap:10px;padding:22px 28px 0}
.head h1{font-size:28px;font-weight:700;letter-spacing:-.015em}
.crumb{font-size:13px;color:rgba(255,255,255,.55);margin-bottom:2px;display:flex;align-items:center;gap:4px}
.chips{display:flex;gap:8px;padding:16px 28px 4px}
.chip{height:30px;padding:0 13px;border-radius:15px;display:flex;align-items:center;gap:7px;font-size:13px;background:rgba(255,255,255,.05);border:1px solid rgba(255,255,255,.08)}
.chip b{font-weight:500;color:rgba(255,255,255,.5)}
.chip.on{background:rgba(47,216,184,.14);border-color:rgba(47,216,184,.6)}
.group{padding:18px 28px 0}
.group h3{font-size:15px;font-weight:600;margin-bottom:10px;display:flex;align-items:center;gap:8px}
.group h3 span{font-weight:400;color:rgba(255,255,255,.5);font-size:13px;display:flex;align-items:center;gap:4px}
.jrow{display:flex;gap:6px;margin-bottom:6px}
.jt{border-radius:6px;overflow:hidden;position:relative}
.jt .b{position:absolute;inset-inline-start:8px;bottom:8px}
.jt .ck{position:absolute;top:8px;inset-inline-start:8px}
.jt.sel{box-shadow:0 0 0 3px #17191d,0 0 0 5px #2FD8B8}
.jt.sel>.thumb{transform:scale(.9);border-radius:6px}
.jt.hov{box-shadow:0 10px 26px rgba(0,0,0,.55)}
.selbar{position:absolute;bottom:22px;left:50%;transform:translateX(-50%);display:flex;align-items:center;gap:6px;height:52px;padding:0 10px 0 18px;border-radius:12px;
  background:rgba(40,42,48,.82);backdrop-filter:blur(24px);border:1px solid rgba(255,255,255,.1);box-shadow:0 18px 40px rgba(0,0,0,.5);font-size:14px}
/* viewer */
.vhead{height:58px;display:flex;align-items:center;gap:6px;padding:0 16px}
.vhead .t{font-size:15px;font-weight:600;margin-inline-start:6px}
.vhead .n{font-size:13px;color:rgba(255,255,255,.5);margin-inline-start:6px}
.cmd{height:34px;display:flex;align-items:center;gap:8px;padding:0 11px;border-radius:6px;font-size:13.5px;color:rgba(255,255,255,.9)}
.cmd.on{background:rgba(255,255,255,.08);box-shadow:inset 0 -2px 0 #2FD8B8}
.media{position:absolute;border-radius:8px;overflow:hidden;background:#000}
.media>svg{width:100%;height:100%;display:block}
.ctrl{position:absolute;direction:ltr}
.sbar{position:relative;height:16px;display:flex;align-items:center}
.sbar .rl{position:absolute;left:0;right:0;height:4px;border-radius:2px;background:rgba(255,255,255,.16)}
.sbar .dn{position:absolute;left:0;height:4px;border-radius:2px;background:${BRAND}}
.sbar .kn{position:absolute;width:16px;height:16px;margin-left:-8px;border-radius:50%;background:#fff;box-shadow:0 0 0 4px rgba(22,163,217,.3)}
.crow{display:flex;align-items:center;margin-top:8px}
.crow .s{flex:1;display:flex;align-items:center;gap:2px}
.cb{width:38px;height:38px;border-radius:7px;display:flex;align-items:center;justify-content:center}
.pp{width:46px;height:46px;border-radius:50%;background:${BRAND};display:flex;align-items:center;justify-content:center;margin:0 8px;box-shadow:0 4px 16px rgba(22,163,217,.45)}
.film{position:absolute;display:flex;gap:8px;align-items:center;overflow:hidden;
  -webkit-mask:linear-gradient(90deg,#000 88%,transparent)}
.filmlabel{position:absolute;font-size:12.5px;color:rgba(255,255,255,.55);display:flex;gap:6px;align-items:center}
.film .thumb{border-radius:6px;flex:none;opacity:.75}
.film .cur{opacity:1;box-shadow:0 0 0 2px #17191d,0 0 0 4px #2FD8B8}
.pane{position:absolute;top:58px;bottom:0;inset-inline-end:0;width:340px;border-inline-start:1px solid rgba(255,255,255,.07);padding:4px 20px;overflow:hidden}
.pane h2{font-size:16px;font-weight:600;margin:10px 0 14px;display:flex;align-items:center;gap:8px}
.sec{margin-bottom:18px}
.sec .h{font-size:12px;font-weight:600;color:rgba(255,255,255,.5);margin-bottom:8px;text-transform:uppercase;letter-spacing:.04em}
html[dir=rtl] .sec .h{text-transform:none;letter-spacing:0}
.kv{display:flex;justify-content:space-between;gap:12px;font-size:13.5px;padding:5px 0}
.kv span:first-child{color:rgba(255,255,255,.6)}
.kv span:last-child{text-align:end}
.radio{display:flex;align-items:center;gap:10px;height:34px;font-size:13.5px}
.radio i{width:18px;height:18px;border-radius:50%;border:1.5px solid rgba(255,255,255,.6);flex:none}
.radio i.on{border:5px solid #16A3D9}
.segc{display:flex;padding:3px;border-radius:8px;background:rgba(255,255,255,.05);border:1px solid rgba(255,255,255,.07)}
.segc span{flex:1;height:28px;display:flex;align-items:center;justify-content:center;border-radius:6px;font-size:12.5px;color:rgba(255,255,255,.75)}
.segc span.on{background:rgba(255,255,255,.12);color:#fff}
.hw{display:inline-flex;align-items:center;gap:6px;height:22px;padding:0 8px;border-radius:11px;font-size:12px;font-weight:600;background:rgba(47,216,184,.15);color:#7ef0d8}
/* trim */
.tl{position:absolute;direction:ltr}
.ruler{position:relative;height:20px;font-size:11px;color:rgba(255,255,255,.5)}
.ruler span{position:absolute;top:0;transform:translateX(-50%)}
.ruler i{position:absolute;bottom:0;width:1px;height:5px;background:rgba(255,255,255,.3)}
.track{position:relative;border-radius:6px;overflow:hidden}
.track .fr{display:flex;height:100%}
.track .fr .thumb{flex:1;height:100%!important;width:auto!important}
.dim{position:absolute;top:0;bottom:0;background:rgba(10,11,14,.66)}
.sg{position:absolute;top:0;bottom:0;border:2px solid rgba(47,216,184,.85);border-radius:6px;background:rgba(47,216,184,.08)}
.sg.cur{border-color:#fff;background:rgba(22,163,217,.12);box-shadow:0 0 0 1px rgba(0,0,0,.4)}
.sg .hd{position:absolute;top:-2px;bottom:-2px;width:10px;background:#fff;border-radius:4px}
.ph{position:absolute;width:2px;background:#fff;margin-left:-1px;box-shadow:0 0 6px rgba(0,0,0,.6)}
.ph::before{content:"";position:absolute;top:-7px;left:-5px;border:6px solid transparent;border-top-color:#fff;border-bottom:0}
.kfrow{position:relative;height:10px}
.kfrow i{position:absolute;top:2px;width:6px;height:6px;margin-left:-3px;transform:rotate(45deg);background:rgba(255,255,255,.4)}
.segcard{border-radius:8px;padding:10px 12px;margin-bottom:8px;background:rgba(255,255,255,.04);border:1px solid rgba(255,255,255,.07)}
.segcard.cur{background:rgba(47,216,184,.1);border-color:rgba(47,216,184,.55)}
.segcard .top{display:flex;align-items:center;gap:8px;font-size:13.5px;font-weight:600}
.segcard .sw{width:10px;height:10px;border-radius:3px}
.segcard .tm{font-size:12.5px;color:rgba(255,255,255,.65);margin-top:4px;display:flex;gap:10px}
.optrow{display:flex;align-items:center;justify-content:space-between;font-size:13.5px;height:38px}
.note{font-size:12.5px;color:rgba(255,255,255,.6);line-height:1.5;display:flex;gap:8px;align-items:flex-start}
`;

function titleBar(searchText) {
  return `<div class="tb">${logo(18)}<span class="app">Mashhad</span></div>
    <div class="search">${icon('search', 's16')}<span>${searchText}</span></div>
    <div style="position:absolute;top:0;${'inset-inline-end'}:0">${captionButtons(48)}</div>`;
}

function navPane(rtl) {
  const T = rtl
    ? { home: 'الرئيسية', recent: 'الأخيرة', fav: 'المفضلة', folders: 'المجلدات', pics: 'الصور', travel: 'السفر', desert: 'رحلة الصحراء', family: 'العائلة 2026', videos: 'مقاطع الفيديو', rec: 'تسجيلات الشاشة', dev: 'هذا الجهاز', cloud: 'OneDrive', set: 'الإعدادات' }
    : { home: 'Home', recent: 'Recent', fav: 'Favorites', folders: 'Folders', pics: 'Pictures', travel: 'Travel', desert: 'Desert trip', family: 'Family 2026', videos: 'Videos', rec: 'Screen recordings', dev: 'This PC', cloud: 'OneDrive', set: 'Settings' };
  return `<div class="nav">
    <div class="it">${icon('home', 's18')}${T.home}</div>
    <div class="it">${icon('clock', 's18')}${T.recent}</div>
    <div class="it">${icon('heart', 's18')}${T.fav}<span class="c num">6</span></div>
    <div class="h">${T.folders}</div>
    <div class="it">${icon('pictures', 's18')}${T.pics}<span class="c num">1,204</span></div>
    <div class="it sub">${icon('folder', 's18')}${T.travel}</div>
    <div class="it sub on" style="padding-inline-start:46px">${icon('folder', 's18')}${T.desert}<span class="c num">42</span></div>
    <div class="it sub">${icon('folder', 's18')}${T.family}</div>
    <div class="it">${icon('video', 's18')}${T.videos}<span class="c num">318</span></div>
    <div class="it sub">${icon('folder', 's18')}${T.rec}</div>
    <div class="h"></div>
    <div class="it">${icon('device', 's18')}${T.dev}</div>
    <div class="it">${icon('cloud', 's18')}${T.cloud}</div>
    <div style="flex:1"></div>
    <div class="it">${icon('settings', 's18')}${T.set}</div></div>`;
}

/** Justified rows: fill each row to `width` at roughly `h` height. */
function justify(items, width, h, gap = 6) {
  const rows = [];
  let cur = [];
  for (const it of items) {
    cur.push(it);
    const sum = cur.reduce((s, x) => s + x.a, 0);
    if (sum * h + gap * (cur.length - 1) >= width) { rows.push(cur); cur = []; }
  }
  if (cur.length) {
    // A short leftover row would be stretched into one giant tile: fold it into the previous row.
    if (cur.length < 3 && rows.length) rows[rows.length - 1].push(...cur); else rows.push(cur);
  }
  return rows.map((row, ri) => {
    const sum = row.reduce((s, x) => s + x.a, 0);
    const last = ri === rows.length - 1 && sum * h + gap * (row.length - 1) < width * 0.8;
    const rh = last ? h : (width - gap * (row.length - 1)) / sum;
    return { rh, row };
  });
}

function tile(m, w, h, { sel = false, hov = false, rtl = false } = {}) {
  const badge = m.v ? `<span class="badge b">${icon('play', 's14')}<span class="num">${m.v}</span></span>` : m.gif ? '<span class="badge b">GIF</span>' : '';
  const ck = sel ? `<span class="chk on ck">${icon('check', 's14', 'stroke-width="2.4"')}</span>` : hov ? '<span class="chk ck"></span>' : '';
  return `<div class="jt ${sel ? 'sel' : ''} ${hov ? 'hov' : ''}" style="width:${w.toFixed(1)}px;height:${h.toFixed(1)}px">${thumb(m.k, m.s, Math.round(w), Math.round(h), '', 'position:absolute;inset:0;width:100%;height:100%')}${badge}${ck}</div>`;
}

function gallery({ rtl = false } = {}) {
  const T = rtl ? {
    crumb: ['الصور', 'السفر'], title: 'رحلة الصحراء', sort: 'تاريخ الالتقاط', filter: 'تصفية', size: 'الحجم', select: 'تحديد', show: 'عرض الشرائح',
    all: 'الكل', photos: 'الصور', videos: 'الفيديو', favs: 'المفضلة', g1: 'الجمعة، 7 نوفمبر 2026', p1: 'العُلا', g2: 'السبت، 8 نوفمبر 2026', p2: 'الربع الخالي',
    sel: 'تم تحديد 2', share: 'مشاركة', copy: 'نسخ', rot: 'تدوير', del: 'حذف', search: 'البحث في رحلة الصحراء',
  } : {
    crumb: ['Pictures', 'Travel'], title: 'Desert trip', sort: 'Date taken', filter: 'Filter', size: 'Size', select: 'Select', show: 'Slideshow',
    all: 'All', photos: 'Photos', videos: 'Videos', favs: 'Favorites', g1: 'Friday, 7 November 2026', p1: 'AlUla', g2: 'Saturday, 8 November 2026', p2: 'Empty Quarter',
    sel: '2 selected', share: 'Share', copy: 'Copy', rot: 'Rotate', del: 'Delete', search: 'Search Desert trip',
  };
  const W = 1336 - 56;
  const g1 = justify(MEDIA.slice(0, 11), W, 190);
  const g2 = justify(MEDIA.slice(11, 24), W, 190);
  let idx = 0;
  const render = (rows) => rows.map(({ rh, row }) => `<div class="jrow">${row.map(m => {
    const i = idx++;
    return tile(m, m.a * rh, rh, { sel: i === 1 || i === 4, hov: i === 2, rtl });
  }).join('')}</div>`).join('');
  const chev = icon(rtl ? 'chevL' : 'chevR', 's14');
  return page({
    dir: rtl ? 'rtl' : 'ltr', css: CSS, body: `${titleBar(T.search)}${navPane(rtl)}
    <div class="card" style="width:1336px">
      <div class="head"><div style="flex:1"><div class="crumb">${T.crumb[0]}${chev}${T.crumb[1]}${chev}</div><h1>${T.title}</h1></div>
        <span class="btn subtle">${icon('sort', 's18')}${T.sort}${icon('chevD', 's14')}</span>
        <span class="btn subtle">${icon('filter', 's18')}${T.filter}</span>
        <span class="btn subtle">${icon('grid', 's18')}${T.size}</span>
        <span class="btn subtle">${icon('check', 's18')}${T.select}</span>
        <span class="btn accent">${icon('slideshow', 's18')}${T.show}</span></div>
      <div class="chips"><span class="chip on">${T.all} <b class="num">42</b></span><span class="chip">${icon('image', 's16')}${T.photos} <b class="num">31</b></span>
        <span class="chip">${icon('video', 's16')}${T.videos} <b class="num">11</b></span><span class="chip">${icon('heart', 's16')}${T.favs} <b class="num">6</b></span></div>
      <div class="group"><h3>${T.g1}<span>${icon('pin', 's14')}${T.p1}</span></h3>${render(g1)}</div>
      <div class="group"><h3>${T.g2}<span>${icon('pin', 's14')}${T.p2}</span></h3>${render(g2)}</div>
    </div>
    <div class="selbar" style="left:calc(50% + ${rtl ? -132 : 132}px)"><span style="margin-inline-end:10px;font-weight:600">${T.sel}</span>
      <span class="btn subtle">${icon('share', 's18')}${T.share}</span><span class="btn subtle">${icon('copy', 's18')}${T.copy}</span>
      <span class="btn subtle">${icon('rotate', 's18')}${T.rot}</span><span class="btn subtle">${icon('trash', 's18')}${T.del}</span>
      <span class="sep"></span><span class="btn subtle icon">${icon('close', 's18')}</span></div>`
  });
}

function rail() {
  return `<div class="rail"><i>${icon('home', 's18')}</i><i>${icon('clock', 's18')}</i><i>${icon('heart', 's18')}</i>
    <i class="on">${icon('folder', 's18')}</i><i>${icon('video', 's18')}</i><span style="flex:1"></span><i style="margin-bottom:8px">${icon('settings', 's18')}</i></div>`;
}

function viewerHead(active) {
  const cmds = [['rotate', 'Rotate'], ['crop', 'Crop'], ['scissors', 'Trim'], ['info', 'Details'], ['share', 'Share'], ['open', 'Open folder']];
  return `<div class="vhead"><span class="btn subtle icon">${icon('back', 's18')}</span>
    <span class="t">Rub al Khali drone.mp4</span><span class="n num">3 of 42</span><span style="flex:1"></span>
    ${cmds.map(([i, l]) => `<span class="cmd ${l === active ? 'on' : ''}" style="${(i === 'rotate' || i === 'crop') ? 'opacity:.4' : ''}">${icon(i, 's18')}${l}</span>`).join('')}
    <span class="cmd">${icon('more', 's18')}</span></div>`;
}

function filmstrip(x, y, w) {
  const items = MEDIA.slice(0, 16);
  return `<div class="filmlabel" style="left:${x}px;top:${y}px">${icon('folder', 's14')}Desert trip · 42 items · <span class="kbd" style="height:18px;min-width:18px;font-size:10.5px">←</span><span class="kbd" style="height:18px;min-width:18px;font-size:10.5px">→</span> to browse</div>
    <div class="film" style="left:${x}px;top:${y + 24}px;width:${w}px;padding:4px">${items.map((m, i) =>
    thumb(m.k, m.s, 118, 74, m.v ? `<span class="badge" style="position:absolute;left:4px;bottom:4px;height:16px;font-size:10px;padding:0 5px">${icon('play', 's14')}</span>` : '', '', 0).replace('class="thumb"', `class="thumb ${i === 1 ? 'cur' : ''}"`)).join('')}</div>`;
}

function player() {
  const cardW = 1544, paneW = 340, mx = 24, mediaW = cardW - paneW - mx * 2, mediaH = 650;
  return page({
    css: CSS, body: `${titleBar('Search Desert trip')}${rail()}
    <div class="card" style="width:${cardW}px">${viewerHead('Details')}
      <div class="media" style="left:${mx}px;top:62px;width:${mediaW}px;height:${mediaH}px">${scene('dunes', 11, 0.35)}</div>
      <div class="ctrl" style="left:${mx}px;top:${62 + mediaH + 12}px;width:${mediaW}px">
        <div class="sbar"><div class="rl"></div><div class="dn" style="width:31%"></div><div class="kn" style="left:31%"></div></div>
        <div class="crow"><div class="s"><span class="num" style="font-weight:600">12:48</span><span class="t3 num" style="margin-inline-start:6px">/ 41:06</span></div>
          <span class="cb">${icon('prev')}</span><span class="cb">${icon('back10', 's24')}</span><span class="pp">${icon('pause', 's24')}</span>
          <span class="cb">${icon('fwd10', 's24')}</span><span class="cb">${icon('next')}</span>
          <div class="s" style="justify-content:flex-end"><span class="cb">${icon('loop')}</span><span class="cb">${icon('volume')}</span>
            <span style="width:90px;height:4px;border-radius:2px;background:linear-gradient(90deg,#fff 70%,rgba(255,255,255,.2) 70%);margin:0 6px"></span>
            <span class="cb">${icon('camera')}</span><span class="cb">${icon('fullscreen')}</span></div></div></div>
      ${filmstrip(mx, 62 + mediaH + 100, mediaW)}
      <div class="pane">
        <h2>${icon('info', 's18')}Details</h2>
        <div class="sec"><div class="h">File</div>
          <div class="kv"><span>Size</span><span class="num">1.82 GB</span></div>
          <div class="kv"><span>Modified</span><span class="num">7 Nov 2026, 17:42</span></div>
          <div class="kv"><span>Folder</span><span>Pictures › Travel › Desert trip</span></div></div>
        <div class="sec"><div class="h">Video</div>
          <div class="kv"><span>Resolution</span><span class="num">3840 × 2160 · 29.97 fps</span></div>
          <div class="kv"><span>Codec</span><span>HEVC Main 10 · HDR10</span></div>
          <div class="kv"><span>Decoding</span><span class="hw">${icon('check', 's14', 'stroke-width="2.2"')}Hardware · D3D11VA</span></div></div>
        <div class="sec"><div class="h">Audio track</div>
          <div class="radio"><i class="on"></i>Arabic · AAC stereo</div><div class="radio"><i></i>English · AC-3 5.1</div></div>
        <div class="sec"><div class="h">Subtitles</div>
          <div class="radio"><i></i>Off</div><div class="radio"><i class="on"></i>English · SRT (external)</div><div class="radio"><i></i>Arabic · ASS</div></div>
        <div class="sec"><div class="h">Speed</div>
          <div class="segc num"><span>0.5×</span><span>0.75×</span><span>1×</span><span class="on">1.25×</span><span>1.5×</span><span>2×</span></div></div>
      </div></div>`
  });
}

function trim() {
  const cardW = 1544, paneW = 340, mx = 24, mediaW = cardW - paneW - mx * 2, mediaH = 610;
  const tlTop = 62 + mediaH + 18;
  const N = 18;
  const frames = Array.from({ length: N }, (_, i) => thumb('dunes', 11, 64, 64, '', '', i / N * 4)).join('');
  const segs = [[0.078, 0.269, '#2FD8B8'], [0.409, 0.581, '#16A3D9', true], [0.740, 0.857, '#2F5BE3']];
  let dims = '', last = 0;
  for (const [a, b] of segs) { dims += `<div class="dim" style="left:${a * 0 + last * 100}%;width:${(a - last) * 100}%"></div>`; last = b; }
  dims += `<div class="dim" style="left:${last * 100}%;right:0"></div>`;
  const ticks = Array.from({ length: 9 }, (_, i) => `<span style="left:${i * 12.5}%">${['0:00', '0:05', '0:10', '0:15', '0:20', '0:25', '0:30', '0:35', '0:40'][i]}</span>`).join('')
    + Array.from({ length: 41 }, (_, i) => `<i style="left:${i * 2.5}%;height:${i % 5 ? 4 : 7}px"></i>`).join('');
  const kfs = Array.from({ length: 41 }, (_, i) => `<i style="left:${i * 2.44 + 0.6}%"></i>`).join('');
  return page({
    css: CSS, body: `${titleBar('Search Desert trip')}${rail()}
    <div class="card" style="width:${cardW}px">${viewerHead('Trim')}
      <div class="media" style="left:${mx}px;top:62px;width:${mediaW}px;height:${mediaH}px">${scene('dunes', 11, 1.8)}
        <span class="badge" style="position:absolute;left:14px;top:14px;height:26px;font-size:13px;padding:0 10px">Segment 2 · <span class="num">00:19.76</span></span></div>
      <div class="tl" style="left:${mx}px;top:${tlTop}px;width:${mediaW}px">
        <div style="display:flex;align-items:center;gap:6px;margin-bottom:12px">
          <span class="pp" style="width:40px;height:40px;margin:0 6px 0 0">${icon('play')}</span>
          <span class="cb">${icon('keyPrev')}</span><span class="cb">${icon('frameBack')}</span><span class="cb">${icon('frameFwd')}</span><span class="cb">${icon('keyNext')}</span>
          <span class="num" style="font-size:20px;font-weight:600;margin:0 8px">00:19.76</span><span class="t3 num">/ 00:41.06</span>
          <span style="flex:1"></span>
          <span class="btn"><span class="kbd">I</span>Set start</span><span class="btn"><span class="kbd">O</span>Set end</span>
          <span class="btn">${icon('split', 's16')}Split</span><span class="btn">${icon('plus', 's16')}Add segment</span>
          <span class="btn icon">${icon('zoomout', 's18')}</span><span class="btn icon">${icon('zoomin', 's18')}</span></div>
        <div class="ruler">${ticks}</div>
        <div class="kfrow">${kfs}</div>
        <div class="track" style="height:64px">${`<div class="fr">${frames}</div>`}${dims}
          ${segs.map(([a, b, c, cur]) => `<div class="sg ${cur ? 'cur' : ''}" style="left:${a * 100}%;width:${(b - a) * 100}%;${cur ? '' : `border-color:${c}`}">
            ${cur ? '<div class="hd" style="left:-6px"></div><div class="hd" style="right:-6px"></div>' : ''}</div>`).join('')}</div>
        <div class="track" style="height:40px;margin-top:6px;background:rgba(255,255,255,.04)">${waveform(mediaW, 40, 5, 'rgba(126,240,216,.6)', 3)}${dims}</div>
        <div class="ph" style="left:48.1%;top:58px;height:142px"></div>
      </div>
      <div class="pane">
        <h2>${icon('scissors', 's18')}Lossless trim</h2>
        <div class="note" style="margin:-4px 0 16px">${icon('info', 's16')}Streams are copied, not re-encoded: no quality loss and exports take seconds.</div>
        <div class="sec"><div class="h">Segments · 3</div>
          ${[['1', '#2FD8B8', '00:03.20', '00:11.04', '7.8 s'], ['2', '#16A3D9', '00:16.80', '00:23.84', '7.0 s', 1], ['3', '#2F5BE3', '00:30.40', '00:35.20', '4.8 s']]
            .map(([n, c, a, b, d, cur]) => `<div class="segcard ${cur ? 'cur' : ''}"><div class="top"><span class="sw" style="background:${c}"></span>Segment ${n}<span style="flex:1"></span>
              <span class="t3" style="font-weight:400">${icon('edit', 's16')}</span><span class="t3" style="font-weight:400">${icon('trash', 's16')}</span></div>
              <div class="tm num"><span>${a} → ${b}</span><span class="t3">${d}</span></div></div>`).join('')}</div>
        <div class="sec"><div class="h">Options</div>
          <div class="optrow">Snap starts to keyframes<span class="toggle on"></span></div>
          <div class="optrow">Merge into one file<span class="toggle"></span></div>
          <div class="optrow">Keep all audio & subtitle tracks<span class="toggle on"></span></div>
          <div class="optrow"><span>Save to</span><span class="btn" style="height:30px">Next to original${icon('chevD', 's14')}</span></div></div>
        <span class="btn accent" style="width:100%;justify-content:center;height:40px;font-size:14px">${icon('export', 's18')}Export 3 clips · 19.6 s</span>
        <span class="btn subtle" style="width:100%;justify-content:center;margin-top:6px">${icon('camera', 's18')}Export current frame</span>
      </div></div>`
  });
}

module.exports = {
  name: 'library',
  screens: [
    ['a-video-player.png', () => player()],
    ['b-folder-gallery.png', () => gallery()],
    ['c-lossless-trim.png', () => trim()],
    ['d-folder-gallery-ar-rtl.png', () => gallery({ rtl: true })],
  ],
};
