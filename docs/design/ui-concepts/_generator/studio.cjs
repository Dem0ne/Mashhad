// Concept C — "Studio": editor-first, LosslessCut-style. A dense, panel-based workspace that is the
// same in every mode (Browse · Play · Cut): media bin, preview, inspector, and a multi-track timeline
// with keyframes and waveforms always at hand.
const { BRAND, icon, logo, captionButtons, scene, thumb, waveform, MEDIA, page } = require('./shared.cjs');

const CSS = `
body{background:#111316;font-size:13px}
.tb{position:absolute;top:0;left:0;right:0;height:40px;display:flex;align-items:center;gap:4px;padding-inline-start:12px;background:#0d0f12;border-bottom:1px solid #23262c}
.tb .m{height:28px;padding:0 9px;display:flex;align-items:center;border-radius:5px;font-size:13px;color:rgba(255,255,255,.82)}
.modes{position:absolute;left:50%;transform:translateX(-50%);top:5px;display:flex;padding:3px;border-radius:8px;background:#1b1e23;border:1px solid #2a2e35}
.modes span{height:24px;padding:0 14px;display:flex;align-items:center;gap:6px;border-radius:6px;font-size:12.5px;font-weight:500;color:rgba(255,255,255,.7)}
.modes span.on{background:${BRAND};color:#fff;box-shadow:0 2px 10px rgba(22,163,217,.4)}
.panel{position:absolute;background:#16181c;border:1px solid #23262c}
.ph{height:34px;display:flex;align-items:center;gap:8px;padding:0 12px;border-bottom:1px solid #23262c;font-size:12px;font-weight:600;
  color:rgba(255,255,255,.7);text-transform:uppercase;letter-spacing:.05em}
html[dir=rtl] .ph{text-transform:none;letter-spacing:0;font-size:13px}
.ph .sp{flex:1}
.tabs{display:flex;height:34px;border-bottom:1px solid #23262c}
.tabs span{flex:1;display:flex;align-items:center;justify-content:center;gap:6px;font-size:12.5px;color:rgba(255,255,255,.6);position:relative}
.tabs span.on{color:#fff}
.tabs span.on::after{content:"";position:absolute;bottom:-1px;left:22%;right:22%;height:2px;border-radius:1px;background:${BRAND}}
.binrow{display:flex;align-items:center;gap:10px;height:50px;padding:0 10px;border-bottom:1px solid rgba(255,255,255,.03)}
.binrow .thumb{border-radius:4px;flex:none}
.binrow .nm{font-size:12.5px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.binrow .mt{font-size:11.5px;color:rgba(255,255,255,.45)}
.binrow.on{background:rgba(22,163,217,.16);box-shadow:inset 3px 0 0 #16A3D9}
html[dir=rtl] .binrow.on{box-shadow:inset -3px 0 0 #16A3D9}
.mini{height:28px;display:flex;align-items:center;gap:8px;margin:8px 10px;padding:0 10px;border-radius:6px;background:#1f2228;border:1px solid #2a2e35;color:rgba(255,255,255,.5);font-size:12px}
.preview{position:absolute;background:#000;overflow:hidden}
.preview>svg{width:100%;height:100%;display:block}
.ovl{position:absolute;display:flex;gap:6px}
.ovl span{height:22px;padding:0 8px;border-radius:4px;background:rgba(0,0,0,.6);font-size:11.5px;display:flex;align-items:center;gap:5px;color:rgba(255,255,255,.85)}
.transport{position:absolute;display:flex;align-items:center;gap:2px;padding:0 12px;background:#16181c;border:1px solid #23262c;direction:ltr}
.tc{font-family:"JetBrains Mono",monospace;font-size:20px;font-weight:600;letter-spacing:-.02em}
.tb2{width:34px;height:34px;border-radius:6px;display:flex;align-items:center;justify-content:center;color:rgba(255,255,255,.88)}
.tb2.on{background:rgba(255,255,255,.08)}
.playb{width:40px;height:40px;border-radius:50%;background:${BRAND};display:flex;align-items:center;justify-content:center;margin:0 6px}
.tool{height:30px;display:flex;align-items:center;gap:6px;padding:0 9px;border-radius:6px;font-size:12.5px;background:#1f2228;border:1px solid #2a2e35;color:rgba(255,255,255,.9)}
.tool .kbd{height:18px;min-width:18px;font-size:10.5px;padding:0 4px}
.tool.on{background:rgba(47,216,184,.14);border-color:rgba(47,216,184,.6);color:#bff7ea}
.tl{position:absolute;direction:ltr;background:#131519;border:1px solid #23262c}
.tlbar{height:42px;display:flex;align-items:center;gap:6px;padding:0 10px;border-bottom:1px solid #23262c}
.tracks{position:absolute;left:0;right:0;bottom:0}
.trk{position:absolute;left:0;right:0;display:flex}
.trk .hdr{width:134px;flex:none;white-space:nowrap;display:flex;align-items:center;gap:7px;padding:0 10px;font-size:11.5px;color:rgba(255,255,255,.7);background:#16181c;border-right:1px solid #23262c;border-bottom:1px solid #1d2025}
.trk .hdr b{font-weight:700;color:#fff;font-size:11.5px}
.trk .lane{flex:1;position:relative;border-bottom:1px solid #1d2025;overflow:hidden}
.rul span{position:absolute;top:4px;font-size:10.5px;color:rgba(255,255,255,.5);transform:translateX(3px);font-family:"JetBrains Mono",monospace}
.rul i{position:absolute;bottom:0;width:1px;background:rgba(255,255,255,.22)}
.blk{position:absolute;top:5px;bottom:5px;border-radius:5px;display:flex;align-items:center;padding:0 8px;font-size:11.5px;font-weight:600;overflow:hidden;white-space:nowrap}
.blk .hd{position:absolute;top:0;bottom:0;width:6px;background:rgba(255,255,255,.85)}
.blk.cur{box-shadow:0 0 0 2px #fff}
.frs{display:flex;height:100%}
.frs .thumb{flex:1;height:100%!important;width:auto!important;border-right:1px solid #000}
.kd{position:absolute;top:50%;width:7px;height:7px;margin:-3.5px 0 0 -3.5px;transform:rotate(45deg);background:#cfd6df;opacity:.7}
.kd.snap{background:#2FD8B8;opacity:1;box-shadow:0 0 0 2px rgba(47,216,184,.3)}
.ins{position:absolute;top:0;bottom:0;background:rgba(10,11,13,.6)}
.playhead{position:absolute;width:2px;margin-left:-1px;background:#fff;z-index:5;box-shadow:0 0 6px rgba(0,0,0,.7)}
.playhead::before{content:"";position:absolute;top:0;left:-6px;width:14px;height:12px;background:#fff;clip-path:polygon(0 0,100% 0,100% 55%,50% 100%,0 55%)}
.abreg{position:absolute;top:0;bottom:0;background:rgba(242,184,75,.14);border-left:2px solid #F2B84B;border-right:2px solid #F2B84B}
.insp{padding:12px 14px}
.lbl{font-size:11.5px;font-weight:600;color:rgba(255,255,255,.5);text-transform:uppercase;letter-spacing:.05em;margin:12px 0 6px}
html[dir=rtl] .lbl{text-transform:none;letter-spacing:0;font-size:12.5px}
.tbl{width:100%;border-collapse:collapse;font-size:12.5px}
.tbl td{padding:5px 4px;border-bottom:1px solid rgba(255,255,255,.05)}
.tbl tr.cur td{background:rgba(22,163,217,.14)}
.tbl .sw{display:inline-block;width:9px;height:9px;border-radius:2px;margin-inline-end:6px}
.ck{width:16px;height:16px;border-radius:4px;border:1.5px solid rgba(255,255,255,.55);display:inline-flex;align-items:center;justify-content:center;flex:none}
.ck.on{background:#16A3D9;border-color:#16A3D9}
.line{display:flex;align-items:center;gap:8px;height:28px;font-size:12.5px}
.line .r{margin-inline-start:auto;color:rgba(255,255,255,.55)}
.dd{height:30px;display:flex;align-items:center;justify-content:space-between;padding:0 10px;border-radius:6px;background:#1f2228;border:1px solid #2a2e35;font-size:12.5px}
.cmdbox{font-family:"JetBrains Mono",monospace;font-size:11px;line-height:1.55;color:#9fe9d9;background:#0c0e11;border:1px solid #23262c;border-radius:6px;padding:9px 10px;direction:ltr;text-align:left;white-space:pre-wrap;word-break:break-all}
.cmdbox b{color:#7fb6ff;font-weight:500}
.stat{display:flex;gap:8px;margin-top:10px}
.stat div{flex:1;background:#1b1e23;border:1px solid #2a2e35;border-radius:6px;padding:6px 10px}
.stat b{display:block;font-size:15px;font-weight:600;margin-top:2px}
.stat span{font-size:11px;color:rgba(255,255,255,.5)}
.gcell{position:relative}
.gcell .thumb{border-radius:5px}
.gcell .nm{font-size:11.5px;margin-top:5px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;color:rgba(255,255,255,.85)}
.gcell .mt{font-size:10.5px;color:rgba(255,255,255,.45)}
.gcell.on .thumb{box-shadow:0 0 0 2px #16181c,0 0 0 4px #16A3D9}
.tree .it{height:30px;display:flex;align-items:center;gap:8px;padding:0 10px;border-radius:5px;font-size:12.5px;color:rgba(255,255,255,.82)}
.tree .it.on{background:rgba(22,163,217,.16)}
.tree .it .c{margin-inline-start:auto;font-size:11px;color:rgba(255,255,255,.4)}
`;

const ACCENTS = ['#2FD8B8', '#16A3D9', '#2F5BE3'];

function titleBar(mode, rtl) {
  const menus = rtl ? ['ملف', 'تحرير', 'عرض', 'تشغيل', 'أدوات', 'مساعدة'] : ['File', 'Edit', 'View', 'Playback', 'Tools', 'Help'];
  const modes = rtl ? [['grid', 'استعراض'], ['play', 'تشغيل'], ['scissors', 'قص']] : [['grid', 'Browse'], ['play', 'Play'], ['scissors', 'Cut']];
  return `<div class="tb">${logo(18)}<span style="width:6px"></span>${menus.map(m => `<span class="m">${m}</span>`).join('')}
    <span style="flex:1"></span>${captionButtons(40)}</div>
    <div class="modes">${modes.map(([i, l], k) => `<span class="${k === mode ? 'on' : ''}">${icon(i, 's14')}${l}</span>`).join('')}</div>`;
}

function bin({ x, y, w, h, rtl, cur = 1 }) {
  const rows = MEDIA.slice(0, 15).map((m, i) => {
    const name = rtl && m.ar ? m.ar : m.n;
    const meta = `<span class="num">${m.v ? `${m.v} · 3840×2160` : m.gif ? 'GIF · 640×480' : `${m.n.split('.').pop().toUpperCase()} · 4032×3024`}</span>`;
    return `<div class="binrow ${i === cur ? 'on' : ''}">${thumb(m.k, m.s, 56, 34)}<div style="min-width:0;flex:1"><div class="nm">${name}</div><div class="mt">${meta}</div></div>
      ${m.v ? icon('video', 's14', 'style="opacity:.5"') : ''}</div>`;
  }).join('');
  return `<div class="panel" style="left:${x}px;top:${y}px;width:${w}px;height:${h}px;overflow:hidden">
    <div class="ph">${icon('folder', 's14')}${rtl ? 'رحلة الصحراء' : 'Desert trip'}<span class="sp"></span><span class="t3 num">42</span>${icon('list', 's14')}</div>
    <div class="mini">${icon('search', 's14')}${rtl ? 'تصفية الملفات' : 'Filter files'}</div>${rows}</div>`;
}

function ruler(w, secs, step, offset = 0) {
  let s = '';
  for (let t = 0; t <= secs; t += step / 5) {
    const x = (t / secs) * w;
    const major = Math.abs(t / step - Math.round(t / step)) < 1e-6;
    s += `<i style="left:${x.toFixed(1)}px;height:${major ? 10 : 5}px"></i>`;
    if (major) s += `<span style="left:${x.toFixed(1)}px">${String(Math.floor((t + offset) / 60)).padStart(2, '0')}:${String(Math.round((t + offset) % 60)).padStart(2, '0')}</span>`;
  }
  return s;
}

/** The multi-track timeline. Time always runs left→right, also in Arabic. */
function timeline({ x, y, w, h, mode, rtl }) {
  const laneW = w - 136;
  const secs = 41.06;
  const X = (t) => (t / secs) * laneW;
  const segs = [[3.2, 11.04, rtl ? 'المقدمة' : '1 · Intro'], [16.8, 23.84, rtl ? 'الكثبان' : '2 · Dunes'], [30.4, 35.2, rtl ? 'الغروب' : '3 · Sunset']];
  const play = 19.76;
  const kfs = Array.from({ length: 42 }, (_, i) => i * 1.001 + 0.02).filter(t => t < secs);
  const frames = Array.from({ length: 22 }, (_, i) => thumb('dunes', 11, 60, 52, '', '', i / 22 * 4)).join('');
  const tools = mode === 2
    ? (rtl
      ? `<span class="tool"><span class="kbd">I</span>بداية</span><span class="tool"><span class="kbd">O</span>نهاية</span><span class="tool">${icon('plus', 's14')}مقطع</span>
         <span class="tool"><span class="kbd">B</span>تقسيم</span><span class="tool">${icon('trash', 's14')}</span><span class="sep"></span>
         <span class="tool">${icon('keyPrev', 's16')}</span><span class="tool">${icon('keyNext', 's16')}</span><span class="tool on">${icon('magnet', 's14')}محاذاة للإطارات المفتاحية</span>`
      : `<span class="tool"><span class="kbd">I</span>Set in</span><span class="tool"><span class="kbd">O</span>Set out</span><span class="tool">${icon('plus', 's14')}Segment</span>
         <span class="tool"><span class="kbd">B</span>Split</span><span class="tool">${icon('trash', 's14')}Delete</span><span class="sep"></span>
         <span class="tool">${icon('keyPrev', 's16')}Prev key</span><span class="tool">${icon('keyNext', 's16')}Next key</span><span class="tool on">${icon('magnet', 's14')}Snap to keyframes</span>`)
    : (rtl ? '' : `<span class="tool">${icon('loop', 's14')}A-B loop</span><span class="tool">${icon('layers', 's14')}Chapters</span><span class="tool">${icon('camera', 's14')}Screenshot</span>`);
  const rows = mode === 2
    ? [['ruler', 26], ['seg', 40], ['video', 56], ['keys', 24], ['a1', 52], ['a2', 40]]
    : [['ruler', 26], ['chap', 30], ['video', 48], ['a1', 52]];
  let top = 42, lanes = '';
  for (const [k, hh] of rows) {
    let hdr = '', lane = '';
    if (k === 'ruler') { hdr = ''; lane = `<div class="rul" style="position:absolute;inset:0">${ruler(laneW, secs, 5)}</div>`; }
    if (k === 'seg') {
      hdr = `${icon('scissors', 's14')}<b>${rtl ? 'المقاطع' : 'Segments'}</b>`;
      lane = segs.map(([a, b, l], i) => `<div class="blk ${i === 1 ? 'cur' : ''}" style="left:${X(a)}px;width:${X(b) - X(a)}px;background:${ACCENTS[i]};color:${i === 0 ? '#06332b' : '#fff'}">
        <div class="hd" style="left:0;border-radius:5px 0 0 5px;${i === 1 ? '' : 'opacity:0'}"></div>${l}<div class="hd" style="right:0;border-radius:0 5px 5px 0;${i === 1 ? '' : 'opacity:0'}"></div></div>`).join('');
    }
    if (k === 'chap') {
      hdr = `${icon('layers', 's14')}<b>${rtl ? 'الفصول' : 'Chapters'}</b>`;
      lane = [[0, 5, 'Takeoff'], [5, 15.6, 'Over the dunes'], [15.6, 29, 'Old city'], [29, 41, 'Sunset']].map(([a, b, l]) =>
        `<div class="blk" style="left:${X(a) + 1}px;width:${X(b) - X(a) - 2}px;background:#22262d;color:rgba(255,255,255,.8);font-weight:500">${l}</div>`).join('')
        + `<div class="abreg" style="left:${X(21.3)}px;width:${X(25.1) - X(21.3)}px"></div>`;
    }
    if (k === 'video') { hdr = `${icon('video', 's14')}<b>V1</b><span class="t3">HEVC</span>`; lane = `<div class="frs">${frames}</div>`; }
    if (k === 'keys') {
      hdr = `${icon('key', 's14')}<b>${rtl ? 'مفتاحية' : 'Keys'}</b>`;
      lane = kfs.map(t => `<div class="kd ${Math.abs(t - 16.8) < 0.6 ? 'snap' : ''}" style="left:${X(t)}px"></div>`).join('');
    }
    if (k === 'a1') { hdr = `${icon('audio', 's14')}<b>A1</b><span class="t3">ara · AAC</span>`; lane = waveform(laneW, hh - 1, 5, 'rgba(126,240,216,.65)', 3); }
    if (k === 'a2') { hdr = `${icon('audio', 's14')}<b>A2</b><span class="t3">eng · AC-3</span>`; lane = waveform(laneW, hh - 1, 9, 'rgba(127,182,255,.55)', 3); }
    if (mode === 2 && k !== 'ruler' && k !== 'seg') {
      let last = 0, d = '';
      for (const [a, b] of segs) { d += `<div class="ins" style="left:${X(last)}px;width:${X(a) - X(last)}px"></div>`; last = b; }
      lane += d + `<div class="ins" style="left:${X(last)}px;right:0"></div>`;
    }
    lanes += `<div class="trk" style="top:${top}px;height:${hh}px"><div class="hdr">${hdr}</div><div class="lane">${lane}</div></div>`;
    top += hh;
  }
  const playX = 136 + X(play);
  return `<div class="tl" style="left:${x}px;top:${y}px;width:${w}px;height:${h}px">
    <div class="tlbar" dir="${rtl ? 'rtl' : 'ltr'}">${tools}<span style="flex:1"></span>
      <span class="tool">${icon('zoomout', 's14')}</span><span style="width:90px;height:4px;border-radius:2px;background:linear-gradient(90deg,#16A3D9 40%,#2a2e35 40%)"></span>
      <span class="tool">${icon('zoomin', 's14')}</span><span class="tool">${rtl ? 'ملاءمة' : 'Fit'}</span></div>
    ${lanes}<div class="playhead" style="left:${playX}px;top:44px;height:${top - 44}px"></div></div>`;
}

function transport({ x, y, w, rtl, mode }) {
  return `<div class="transport" style="left:${x}px;top:${y}px;width:${w}px;height:56px">
    <span class="tc">00:00:19<span class="t3">.760</span></span><span class="t3 num" style="margin:0 10px;font-size:12px">/ 00:00:41.060</span>
    <span style="flex:1"></span>
    <span class="tb2">${icon('keyPrev')}</span><span class="tb2">${icon('frameBack')}</span>
    <span class="playb">${icon(mode === 1 ? 'pause' : 'play')}</span>
    <span class="tb2">${icon('frameFwd')}</span><span class="tb2">${icon('keyNext')}</span>
    <span style="flex:1"></span>
    <span class="tool num">1.25×</span><span class="tb2">${icon('loop', 's18')}</span><span class="tb2">${icon('volume', 's18')}</span>
    <span class="tb2">${icon('cc', 's18')}</span><span class="tb2">${icon('fullscreen', 's18')}</span></div>`;
}

function inspectorCut(rtl) {
  const T = rtl ? {
    tabs: ['المقاطع', 'المسارات', 'التصدير'], segs: 'المقاطع · 3', name: 'الاسم', in: 'بداية', out: 'نهاية', dur: 'المدة',
    out2: 'المخرجات', merge: 'دمج المقاطع في ملف واحد', snap: 'محاذاة البدايات للإطارات المفتاحية', keep: 'الإبقاء على كل المسارات',
    fmt: 'الحاوية', same: 'مثل المصدر (MP4)', streams: 'المسارات', files: 'ملفات',
    cmd: 'أمر FFmpeg (معاينة)', exp: 'تصدير 3 مقاطع بلا فقد',
  } : {
    tabs: ['Segments', 'Streams', 'Export'], segs: 'Segments · 3', name: 'Name', in: 'In', out: 'Out', dur: 'Dur',
    out2: 'Output', merge: 'Merge segments into one file', snap: 'Snap starts to keyframes', keep: 'Keep all tracks & chapters',
    fmt: 'Container', same: 'Same as source (MP4)', streams: 'Streams', files: 'files',
    cmd: 'FFmpeg command (preview)', exp: 'Export 3 segments losslessly',
  };
  const segRows = [['1', 'Intro', '00:03.200', '00:11.040', '7.84'], ['2', 'Dunes', '00:16.800', '00:23.840', '7.04', 1], ['3', 'Sunset', '00:30.400', '00:35.200', '4.80']]
    .map(([n, l, a, b, d, cur], i) => `<tr class="${cur ? 'cur' : ''}"><td><span class="sw" style="background:${ACCENTS[i]}"></span>${rtl ? ['المقدمة', 'الكثبان', 'الغروب'][i] : l}</td>
      <td class="num">${a}</td><td class="num">${b}</td><td class="num t2">${d}s</td></tr>`).join('');
  return `<div class="tabs"><span>${icon('list', 's14')}${T.tabs[0]}</span><span>${icon('layers', 's14')}${T.tabs[1]}</span><span class="on">${icon('export', 's14')}${T.tabs[2]}</span></div>
    <div class="insp">
      <div class="lbl" style="margin-top:2px">${T.segs}</div>
      <table class="tbl"><tr class="t3"><td>${T.name}</td><td>${T.in}</td><td>${T.out}</td><td>${T.dur}</td></tr>${segRows}</table>
      <div class="lbl">${T.out2}</div>
      <div class="line"><span class="ck">${''}</span>${T.merge}</div>
      <div class="line"><span class="ck on">${icon('check', 's14', 'stroke-width="2.4"')}</span>${T.snap}</div>
      <div class="line"><span class="ck on">${icon('check', 's14', 'stroke-width="2.4"')}</span>${T.keep}</div>
      <div class="line" style="margin-top:4px"><span style="width:80px">${T.fmt}</span><div class="dd" style="flex:1">${T.same}${icon('chevD', 's14')}</div></div>
      <div class="line" style="margin-top:2px"><span style="width:80px">${T.streams}</span><div class="dd" style="flex:1"><span class="num">V + 2×A</span>${icon('chevD', 's14')}</div></div>
      <div class="line t2" style="margin-top:4px">${icon('info', 's14')}<span class="num">3</span> ${T.files} · <span class="num">412 MB</span> · <span class="num">~4 s</span></div>
      <div class="lbl">${T.cmd}</div>
      <div class="cmdbox"><b>ffmpeg</b> -ss 16.131 -i "Rub al Khali drone.mp4" -t 7.709 -map 0:v -map 0:a -c copy -avoid_negative_ts make_zero "…drone-00.00.16.000-00.00.23.840.mp4"</div>
      <span class="btn accent" style="width:100%;justify-content:center;height:38px;margin-top:12px">${icon('export', 's18')}${T.exp}</span>
    </div>`;
}

function inspectorPlay() {
  const radio = (on, l, r) => `<div class="line"><span class="ck" style="border-radius:50%;${on ? 'border:5px solid #16A3D9' : ''}"></span>${l}<span class="r">${r || ''}</span></div>`;
  return `<div class="tabs"><span class="on">${icon('audio', 's14')}Tracks</span><span>${icon('info', 's14')}Info</span><span>${icon('list', 's14')}Playlist</span></div>
    <div class="insp">
      <div class="lbl" style="margin-top:2px">Audio</div>${radio(true, 'Arabic · AAC 2.0', '#1')}${radio(false, 'English · AC-3 5.1', '#2')}
      <div class="lbl">Subtitles</div>${radio(false, 'Off')}${radio(true, 'English · SubRip', 'external')}${radio(false, 'Arabic · ASS', '#3')}
      <div class="line" style="margin-top:4px"><span style="width:90px" class="t2">Delay</span><div class="dd num" style="flex:1">+0.20 s</div></div>
      <div class="lbl">Playback</div>
      <div class="line"><span style="width:90px" class="t2">Speed</span><div class="dd num" style="flex:1">1.25×${icon('chevD', 's14')}</div></div>
      <div class="line"><span style="width:90px" class="t2">A-B loop</span><div class="dd num" style="flex:1;border-color:rgba(242,184,75,.6)">00:21.30 → 00:25.10</div></div>
      <div class="line"><span class="ck on">${icon('check', 's14', 'stroke-width="2.4"')}</span>Resume where I left off</div>
      <div class="line"><span class="ck">${''}</span>Repeat file</div>
      <div class="lbl">Engine</div>
      <div class="line"><span class="t2" style="width:90px">Decoder</span><span style="color:#7ef0d8;font-weight:600">d3d11va (hardware)</span></div>
      <div class="line"><span class="t2" style="width:90px">Output</span>gpu-next · d3d11 · HDR passthrough</div>
      <div class="line"><span class="t2" style="width:90px">Dropped</span><span class="num">0 frames</span></div>
      <div class="line"><span class="t2" style="width:90px">Cache</span><span class="num">38.4 s · 62 MB</span></div>
    </div>`;
}

function cut({ rtl = false } = {}) {
  const binW = 250, inspW = 352, top = 40, tlH = 300, trH = 56;
  const prevW = 1600 - binW - inspW, prevH = 1000 - top - tlH - trH;
  const L = (x, w) => rtl ? 1600 - x - w : x; // mirror panel columns for RTL
  return page({
    dir: rtl ? 'rtl' : 'ltr', css: CSS, body: `${titleBar(2, rtl)}
    ${bin({ x: L(0, binW), y: top, w: binW, h: 1000 - top - tlH, rtl })}
    <div class="preview" style="left:${L(binW, prevW)}px;top:${top}px;width:${prevW}px;height:${prevH}px">${scene('dunes', 11, 1.8)}
      <div class="ovl" style="top:10px;${rtl ? 'right' : 'left'}:10px"><span class="num">3840×2160</span><span class="num">HEVC · HDR10</span><span class="num">29.97 fps</span></div>
      <div class="ovl" style="top:10px;${rtl ? 'left' : 'right'}:10px"><span style="background:rgba(22,163,217,.8);color:#fff">${icon('scissors', 's14')}${rtl ? 'المقطع 2 من 3' : 'Segment 2 of 3'}</span></div></div>
    ${transport({ x: L(binW, prevW), y: top + prevH, w: prevW, rtl, mode: 2 })}
    <div class="panel" style="left:${L(binW + prevW, inspW)}px;top:${top}px;width:${inspW}px;height:${1000 - top - tlH}px;overflow:hidden">${inspectorCut(rtl)}</div>
    ${timeline({ x: 0, y: 1000 - tlH, w: 1600, h: tlH, mode: 2, rtl })}`
  });
}

function play() {
  const binW = 250, inspW = 320, top = 40, tlH = 200, trH = 56;
  const prevW = 1600 - binW - inspW, prevH = 1000 - top - tlH - trH;
  return page({
    css: CSS, body: `${titleBar(1)}
    ${bin({ x: 0, y: top, w: binW, h: 1000 - top - tlH })}
    <div class="preview" style="left:${binW}px;top:${top}px;width:${prevW}px;height:${prevH}px">${scene('dunes', 11, 0.35)}
      <div style="position:absolute;left:0;right:0;bottom:22px;text-align:center"><span style="font-size:24px;font-weight:600;padding:3px 12px;border-radius:5px;background:rgba(0,0,0,.45)">And there, beyond the dunes, the old city was still asleep.</span></div></div>
    ${transport({ x: binW, y: top + prevH, w: prevW, mode: 1 })}
    <div class="panel" style="left:${binW + prevW}px;top:${top}px;width:${inspW}px;height:${1000 - top - tlH}px;overflow:hidden">${inspectorPlay()}</div>
    ${timeline({ x: 0, y: 1000 - tlH, w: 1600, h: tlH, mode: 1 })}`
  });
}

function histogram(w, h) {
  const ch = [['#ff5a5a', 3], ['#5aff8a', 7], ['#5aa0ff', 11]];
  return `<svg width="${w}" height="${h}" style="display:block">${ch.map(([c, s]) => {
    let d = `M0,${h}`; let v = 0.3;
    for (let x = 0; x <= w; x += 4) {
      const t = x / w; v = Math.max(0.05, Math.min(1, v + (Math.sin(x * 0.07 + s) * 0.08) + (Math.cos(x * 0.031 * s) * 0.05)));
      const env = Math.exp(-Math.pow((t - (0.35 + s * 0.03)) / 0.28, 2));
      d += `L${x},${(h - h * v * env * 0.95).toFixed(1)}`;
    }
    return `<path d="${d}L${w},${h}Z" fill="${c}" opacity=".35" style="mix-blend-mode:screen"/>`;
  }).join('')}</svg>`;
}

function browse() {
  const treeW = 230, inspW = 380, top = 40;
  const gridW = 1600 - treeW - inspW;
  const cols = 5, gap = 14, pad = 16, cw = (gridW - pad * 2 - gap * (cols - 1)) / cols, chh = cw * 0.66;
  const cells = MEDIA.concat(MEDIA).slice(0, 25).map((m, i) => {
    const badge = m.v ? `<span class="badge" style="position:absolute;left:6px;bottom:6px;height:18px;font-size:10.5px">${icon('play', 's14')}<span class="num">${m.v}</span></span>` : m.gif ? '<span class="badge" style="position:absolute;left:6px;bottom:6px;height:18px;font-size:10.5px">GIF</span>' : '';
    return `<div class="gcell ${i === 6 ? 'on' : ''}">${thumb(m.k, m.s, Math.round(cw), Math.round(chh), badge)}<div class="nm">${m.n}</div>
      <div class="mt">${m.v ? 'Video · 3840×2160' : m.gif ? 'GIF · 640×480' : 'Photo · 4032×3024'}</div></div>`;
  }).join('');
  const kv = (k, v) => `<tr><td class="t3" style="width:120px">${k}</td><td>${v}</td></tr>`;
  return page({
    css: CSS, body: `${titleBar(0)}
    <div class="panel tree" style="left:0;top:${top}px;width:${treeW}px;height:${1000 - top}px;padding:0 6px">
      <div class="ph" style="margin:0 -6px 6px">${icon('folder', 's14')}Folders</div>
      <div class="it">${icon('pictures', 's16')}Pictures<span class="c num">1,204</span></div>
      <div class="it" style="padding-inline-start:26px">${icon('chevD', 's14')}${icon('folder', 's16')}Travel</div>
      <div class="it on" style="padding-inline-start:48px">${icon('folder', 's16')}Desert trip<span class="c num">42</span></div>
      <div class="it" style="padding-inline-start:48px">${icon('folder', 's16')}Istanbul 2025<span class="c num">96</span></div>
      <div class="it" style="padding-inline-start:26px">${icon('chevR', 's14')}${icon('folder', 's16')}Family 2026</div>
      <div class="it">${icon('video', 's16')}Videos<span class="c num">318</span></div>
      <div class="it" style="padding-inline-start:26px">${icon('chevR', 's14')}${icon('folder', 's16')}Screen recordings</div>
      <div class="it">${icon('device', 's16')}This PC</div>
      <div class="ph" style="margin:14px -6px 6px">${icon('star', 's14')}Collections</div>
      <div class="it">${icon('heart', 's16')}Favorites<span class="c num">6</span></div>
      <div class="it">${icon('scissors', 's16')}Exported clips<span class="c num">14</span></div>
      <div class="it">${icon('camera', 's16')}Screenshots<span class="c num">57</span></div></div>
    <div class="panel" style="left:${treeW}px;top:${top}px;width:${gridW}px;height:${1000 - top}px;overflow:hidden">
      <div class="ph" style="text-transform:none;letter-spacing:0;font-size:13px;color:#fff">Pictures › Travel › <b style="font-weight:600">Desert trip</b><span class="sp"></span>
        <span class="t3" style="font-weight:400">42 items · 3.8 GB</span></div>
      <div style="display:flex;gap:6px;padding:10px 16px 0;align-items:center">
        <span class="tool on">All</span><span class="tool">${icon('image', 's14')}Photos 31</span><span class="tool">${icon('video', 's14')}Videos 11</span>
        <span class="tool">${icon('star', 's14')}Rated</span><span style="flex:1"></span><span class="tool">${icon('sort', 's14')}Date taken</span>
        <span class="tool">${icon('grid', 's14')}</span><span class="tool">${icon('list', 's14')}</span><span class="tool">${icon('slideshow', 's14')}Slideshow</span></div>
      <div style="display:grid;grid-template-columns:repeat(${cols},${cw}px);gap:${gap}px;padding:14px ${pad}px">${cells}</div></div>
    <div class="panel" style="left:${treeW + gridW}px;top:${top}px;width:${inspW}px;height:${1000 - top}px;overflow:hidden">
      <div class="ph">${icon('info', 's14')}Inspector<span class="sp"></span>${icon('more', 's14')}</div>
      <div style="padding:12px 14px">
        ${thumb('city', 8, 350, 210, '', 'border-radius:6px')}
        <div style="font-size:14px;font-weight:600;margin-top:10px">Night skyline.webp</div>
        <div class="t3" style="font-size:12px">7 Nov 2026, 21:14 · 6.4 MB</div>
        <div class="lbl">Histogram</div>
        <div style="background:#0c0e11;border:1px solid #23262c;border-radius:6px;padding:6px">${histogram(336, 70)}</div>
        <div class="lbl">Camera</div>
        <table class="tbl">${kv('Camera', 'Sony α7 IV')}${kv('Lens', 'FE 24-70mm F2.8 GM II')}${kv('Exposure', '1/60 s · ƒ/2.8 · ISO 3200')}${kv('Focal length', '35 mm')}</table>
        <div class="lbl">Image</div>
        <table class="tbl">${kv('Dimensions', '3840 × 2160')}${kv('Format', 'WebP (lossy)')}${kv('Color', 'Display P3 · 8-bit')}${kv('Location', '24.7136, 46.6753')}</table>
        <div class="lbl">Rating & tags</div>
        <div style="display:flex;gap:3px;color:#F2B84B">${[1, 1, 1, 1, 0].map(f => icon('star', 's18', f ? 'fill="#F2B84B"' : 'style="color:rgba(255,255,255,.3)"')).join('')}</div>
        <div style="display:flex;gap:6px;margin-top:10px;flex-wrap:wrap"><span class="tool">night</span><span class="tool">city</span><span class="tool">Riyadh</span><span class="tool t3">${icon('plus', 's14')}Tag</span></div>
        <div style="display:flex;gap:6px;margin-top:14px"><span class="tool">${icon('rotate', 's14')}Rotate</span><span class="tool">${icon('crop', 's14')}Crop</span>
          <span class="tool">${icon('open', 's14')}Open</span><span class="tool">${icon('star', 's14')}Rate</span></div>
      </div></div>`
  });
}

module.exports = {
  name: 'studio',
  screens: [
    ['a-video-player.png', () => play()],
    ['b-folder-gallery.png', () => browse()],
    ['c-lossless-trim.png', () => cut()],
    ['d-lossless-trim-ar-rtl.png', () => cut({ rtl: true })],
  ],
};
