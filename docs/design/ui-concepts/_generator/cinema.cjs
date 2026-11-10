// Concept A — "Cinema": minimal, immersive. The media is the whole window; controls float on
// acrylic and get out of the way.
const { BRAND, icon, logo, captionButtons, scene, thumb, MEDIA, page } = require('./shared.cjs');

const CSS = `
body{background:#07080a}
.stage{position:absolute;inset:0}
.stage>svg{width:100%;height:100%;display:block}
.scrim-t{position:absolute;left:0;right:0;top:0;height:150px;background:linear-gradient(rgba(0,0,0,.62),rgba(0,0,0,0))}
.scrim-b{position:absolute;left:0;right:0;bottom:0;height:300px;background:linear-gradient(rgba(0,0,0,0),rgba(0,0,0,.7))}
.tbar{position:absolute;top:0;left:0;right:0;height:44px;display:flex;align-items:center;gap:12px;padding-inline-start:18px}
.tbar .name{font-size:14px;font-weight:600}
.tbar .ctx{font-size:13px;color:rgba(255,255,255,.62)}
.tbar .grow{flex:1}
.acrylic{background:rgba(26,28,32,.58);backdrop-filter:blur(28px) saturate(150%);border:1px solid rgba(255,255,255,.09);
  box-shadow:0 18px 50px rgba(0,0,0,.45),inset 0 1px 0 rgba(255,255,255,.06)}
.dock{position:absolute;left:50%;transform:translateX(-50%);bottom:28px;width:1080px;border-radius:20px;padding:14px 20px 12px}
.seek{position:relative;height:18px;display:flex;align-items:center;direction:ltr}
.seek .rail{position:absolute;left:0;right:0;height:4px;border-radius:2px;background:rgba(255,255,255,.18)}
.seek .buf{position:absolute;left:0;height:4px;border-radius:2px;background:rgba(255,255,255,.28)}
.seek .done{position:absolute;left:0;height:4px;border-radius:2px;background:${BRAND}}
.seek .knob{position:absolute;width:14px;height:14px;margin-left:-7px;border-radius:50%;background:#fff;box-shadow:0 0 0 4px rgba(255,94,126,.25),0 2px 6px rgba(0,0,0,.5)}
.seek .chap{position:absolute;width:3px;height:6px;background:rgba(10,10,12,.9)}
.seek .ab{position:absolute;height:8px;border-radius:2px;background:rgba(242,184,75,.28);border-left:2px solid #F2B84B;border-right:2px solid #F2B84B}
.preview{position:absolute;bottom:26px;width:176px;margin-left:-88px;border-radius:10px;overflow:hidden;border:2px solid rgba(255,255,255,.9);box-shadow:0 10px 30px rgba(0,0,0,.6)}
.preview .tm{position:absolute;bottom:6px;left:0;right:0;text-align:center;font-size:12px;font-weight:600}
.row{display:flex;align-items:center;margin-top:10px;direction:ltr}
.row .side{flex:1;display:flex;align-items:center;gap:4px}
.row .side.end{justify-content:flex-end}
.cbtn{width:40px;height:40px;border-radius:10px;display:flex;align-items:center;justify-content:center;color:rgba(255,255,255,.92)}
.cbtn.on{background:rgba(255,255,255,.1)}
.play{width:54px;height:54px;border-radius:50%;background:${BRAND};display:flex;align-items:center;justify-content:center;margin:0 10px;
  box-shadow:0 6px 24px rgba(230,34,63,.55),inset 0 1px 0 rgba(255,255,255,.3)}
.time{font-size:14px;font-weight:600}.time b{font-weight:600;color:rgba(255,255,255,.55)}
.pill{height:30px;padding:0 11px;border-radius:15px;background:rgba(255,255,255,.1);display:flex;align-items:center;gap:6px;font-size:13px;font-weight:600}
.vol{width:86px;height:4px;border-radius:2px;background:rgba(255,255,255,.2);position:relative;margin:0 6px}
.vol::before{content:"";position:absolute;inset:0 30% 0 0;border-radius:2px;background:#fff}
.sub{position:absolute;left:0;right:0;bottom:252px;text-align:center}
.sub span{font-size:30px;font-weight:600;padding:4px 14px;border-radius:6px;background:rgba(0,0,0,.42);text-shadow:0 2px 6px rgba(0,0,0,.6)}
.edge{position:absolute;top:50%;margin-top:-28px;width:44px;height:56px;border-radius:12px;display:flex;align-items:center;justify-content:center;color:#fff}
.chip{position:absolute;display:flex;align-items:center;gap:8px;height:34px;padding:0 14px;border-radius:17px;font-size:13px;font-weight:500}
.dot{width:8px;height:8px;border-radius:50%;background:${BRAND}}
/* gallery */
.wall{position:absolute;inset:0;background:radial-gradient(900px 420px at 15% -8%,rgba(255,94,126,.16),transparent 70%),
  radial-gradient(900px 500px at 95% -10%,rgba(142,16,53,.20),transparent 70%),#0b0c0f}
.ghead{position:absolute;top:44px;left:40px;right:40px;display:flex;align-items:flex-end;gap:16px}
.ghead h1{font-size:34px;font-weight:700;letter-spacing:-.02em}
.ghead .meta{font-size:14px;color:rgba(255,255,255,.6);margin-top:4px}
.seg{display:flex;padding:3px;border-radius:10px;background:rgba(255,255,255,.06);border:1px solid rgba(255,255,255,.08)}
.seg span{height:30px;padding:0 14px;display:flex;align-items:center;border-radius:7px;font-size:13px;font-weight:500;color:rgba(255,255,255,.75)}
.seg span.on{background:rgba(255,255,255,.12);color:#fff;box-shadow:0 1px 0 rgba(255,255,255,.08) inset}
.grid{position:absolute;top:150px;left:40px;right:40px;display:grid;grid-template-columns:repeat(5,1fr);gap:12px}
.tile{border-radius:12px;overflow:hidden;position:relative;aspect-ratio:16/10}
.tile .thumb{position:absolute;inset:0;width:100%!important;height:100%!important}
.tile .b{position:absolute;left:8px;bottom:8px}
.tile .fav{position:absolute;right:8px;top:8px;color:#fff}
.tile.focus{outline:3px solid transparent;transform:scale(1.045);z-index:2;box-shadow:0 0 0 3px #0b0c0f,0 0 0 6px #FF5E7E,0 24px 50px rgba(0,0,0,.7)}
.tile.focus .cap{position:absolute;left:0;right:0;bottom:0;padding:30px 12px 10px;background:linear-gradient(rgba(0,0,0,0),rgba(0,0,0,.75));font-size:13px}
.tile.focus .cap b{display:block;font-weight:600;font-size:14px}
.fade{position:absolute;left:0;right:0;bottom:0;height:200px;background:linear-gradient(rgba(11,12,15,0),#0b0c0f 85%)}
.hint{position:absolute;bottom:30px;left:50%;transform:translateX(-50%);display:flex;align-items:center;gap:10px;height:44px;padding:0 18px;border-radius:22px;font-size:13px;color:rgba(255,255,255,.8)}
/* trim */
.trim{position:absolute;left:50%;transform:translateX(-50%);bottom:24px;width:1440px;border-radius:20px;padding:16px 20px}
.trow{display:flex;align-items:center;gap:10px}
.strip{position:relative;height:72px;margin-top:54px;direction:ltr}
.frames{position:absolute;inset:0;display:flex;border-radius:10px;overflow:hidden}
.frames .thumb{flex:1;height:72px!important;width:auto!important}
.shade{position:absolute;top:0;bottom:0;background:rgba(5,6,8,.62)}
.segbox{position:absolute;top:-4px;bottom:-4px;border-radius:10px;border:3px solid rgba(255,94,126,.75)}
.segbox.cur{border:0;padding:3px;background:${BRAND};-webkit-mask:linear-gradient(#000 0 0) content-box,linear-gradient(#000 0 0);-webkit-mask-composite:xor;mask-composite:exclude}
.handle{position:absolute;top:-4px;bottom:-4px;width:16px;background:#FF5E7E;display:flex;align-items:center;justify-content:center;color:#4a0617}
.handle.l{border-radius:10px 0 0 10px}.handle.r{border-radius:0 10px 10px 0;background:#8E1035;color:#fff}
.seglabel{position:absolute;top:-26px;font-size:11.5px;font-weight:700;padding:2px 7px;border-radius:5px;background:rgba(255,94,126,.18);color:#ffa8b8}
.kf{position:absolute;top:-12px;width:6px;height:6px;margin-left:-3px;transform:rotate(45deg);background:rgba(255,255,255,.55)}
.phead{position:absolute;top:-14px;bottom:-14px;width:2px;margin-left:-1px;background:#fff;box-shadow:0 0 8px rgba(0,0,0,.6)}
.phead::before{content:"";position:absolute;top:-6px;left:-6px;width:14px;height:10px;border-radius:3px;background:#fff}
.tcbubble{position:absolute;top:-50px;transform:translateX(-50%);padding:4px 9px;border-radius:7px;background:#fff;color:#0b0c0f;font-size:12.5px;font-weight:700;white-space:nowrap}
.schip{display:flex;align-items:center;gap:10px;height:40px;padding:0 14px;border-radius:12px;background:rgba(255,255,255,.06);border:1px solid rgba(255,255,255,.08);font-size:13px}
.schip.cur{background:rgba(255,94,126,.12);border-color:rgba(255,94,126,.55)}
.schip .n{width:22px;height:22px;border-radius:7px;display:flex;align-items:center;justify-content:center;font-weight:700;font-size:12px;background:rgba(255,255,255,.12)}
.schip.cur .n{background:${BRAND}}
.opt{display:flex;align-items:center;gap:10px;font-size:13px;color:rgba(255,255,255,.88)}
/* flyout */
.menu{position:absolute;width:280px;border-radius:12px;padding:6px;font-size:14px}
.menu .h{padding:10px 12px 6px;font-size:12px;font-weight:600;color:rgba(255,255,255,.55)}
.menu .it{height:38px;display:flex;align-items:center;gap:10px;padding:0 12px;border-radius:7px}
.menu .it.on{background:rgba(255,255,255,.08)}
.menu .it .ck{width:16px;color:#FF5E7E}
.menu .div{height:1px;background:rgba(255,255,255,.08);margin:5px 0}
`;

const FRAME = scene('dunes', 11, 0.35);

function titleBar(name, ctx) {
  return `<div class="tbar">${logo(18)}<span class="name">${name}</span><span class="ctx">${ctx}</span><span class="grow"></span>${captionButtons(44)}</div>`;
}

function seekBar({ pos = 0.31, buf = 0.46, ab = [0.52, 0.61], preview = 0.44, previewTime = '18:05', chapters = [0.12, 0.38, 0.71] }) {
  return `<div class="seek"><div class="rail"></div><div class="buf" style="width:${buf * 100}%"></div>
    ${chapters.map(c => `<div class="chap" style="left:${c * 100}%"></div>`).join('')}
    ${ab ? `<div class="ab" style="left:${ab[0] * 100}%;width:${(ab[1] - ab[0]) * 100}%"></div>` : ''}
    <div class="done" style="width:${pos * 100}%"></div><div class="knob" style="left:${pos * 100}%"></div>
    ${preview ? `<div class="preview" style="left:${preview * 100}%">${thumb('dunes', 11, 172, 97, `<div class="tm">${previewTime}</div>`, '', 0.6)}</div>` : ''}</div>`;
}

function controls({ rtl = false } = {}) {
  return `<div class="row">
    <div class="side"><span class="time num">12:48 <b>/ 41:06</b></span></div>
    <div style="display:flex;align-items:center">
      <span class="cbtn">${icon('prev')}</span><span class="cbtn">${icon('back10', 's24')}</span>
      <span class="play">${icon('pause', 's24')}</span>
      <span class="cbtn">${icon('fwd10', 's24')}</span><span class="cbtn">${icon('next')}</span>
    </div>
    <div class="side end">
      <span class="pill num">${icon('speed', 's16')}1.25×</span>
      <span class="cbtn ${rtl ? 'on' : ''}">${icon('cc')}</span><span class="cbtn">${icon('audio')}</span>
      <span class="cbtn">${icon('volume')}</span><span class="vol"></span>
      <span class="cbtn">${icon('fullscreen')}</span><span class="cbtn">${icon('more')}</span>
    </div></div>`;
}

function player({ rtl = false } = {}) {
  const name = rtl ? 'الربع الخالي - تصوير جوي.mp4' : 'Rub al Khali drone.mp4';
  const ctx = rtl ? '٣ من ٤٢ · رحلة الصحراء' : '3 of 42 · Desert trip';
  const sub = rtl ? 'وهناك، خلف الكثبان، كانت المدينة نائمة.' : 'And there, beyond the dunes, the old city was still asleep.';
  const menu = rtl ? `<div class="menu acrylic" style="bottom:140px;left:1060px">
      <div class="h">الترجمة</div>
      <div class="it on"><span class="ck">${icon('check', 's16')}</span>العربية</div>
      <div class="it"><span class="ck"></span>English</div>
      <div class="it"><span class="ck"></span>إيقاف</div>
      <div class="div"></div>
      <div class="it"><span class="ck"></span>${icon('subtitle', 's16')}تحميل ملف ترجمة…</div>
      <div class="it"><span class="ck"></span>${icon('clock', 's16')}تأخير الترجمة <span class="t3 num ltr" style="margin-inline-start:auto">+0.2s</span></div>
    </div>` : '';
  return page({
    dir: rtl ? 'rtl' : 'ltr', css: CSS, body: `
    <div class="stage">${FRAME}</div><div class="scrim-t"></div><div class="scrim-b"></div>
    ${titleBar(name, ctx)}
    <div class="chip acrylic" style="top:58px;${rtl ? 'right' : 'left'}:18px"><span class="dot"></span>${rtl ? 'فك الترميز بالعتاد · D3D11VA · 4K' : 'Hardware decoding · D3D11VA · 4K HDR'}</div>
    <div class="edge acrylic" style="left:18px">${icon('chevL', 's24')}</div><div class="edge acrylic" style="right:18px">${icon('chevR', 's24')}</div>
    <div class="sub"><span>${sub}</span></div>
    <div class="dock acrylic">${seekBar(rtl ? { preview: null } : {})}${controls({ rtl })}</div>${menu}`
  });
}

function gallery() {
  const tiles = MEDIA.slice(0, 20).map((m, i) => {
    const badge = m.v ? `<span class="badge b">${icon('play', 's14')}${m.v}</span>` : m.gif ? `<span class="badge b">GIF</span>` : '';
    const focus = i === 6;
    const cap = focus ? `<div class="cap"><b>${m.n}</b><span class="t2">3840 × 2160 · 6.4 MB · 7 Nov 2026</span></div>` : '';
    const fav = (i === 2 || i === 11) ? `<span class="fav">${icon('heart', 's18', 'fill="#fff"')}</span>` : '';
    return `<div class="tile ${focus ? 'focus' : ''}">${thumb(m.k, m.s, 300, 188)}${focus ? '' : badge}${fav}${cap}</div>`;
  }).join('');
  return page({
    css: CSS, body: `<div class="wall"></div>
    <div class="tbar" style="z-index:5">${logo(18)}<span class="name">Mashhad</span><span class="grow"></span>${captionButtons(44)}</div>
    <div class="ghead"><div style="flex:1"><div class="meta" style="margin:0 0 2px">Pictures › Travel</div><h1>Desert trip</h1>
      <div class="meta">42 items · 31 photos · 11 videos · 3.8 GB</div></div>
      <div class="seg"><span class="on">All</span><span>Photos</span><span>Videos</span><span>Favorites</span></div>
      <span class="btn subtle">${icon('sort', 's18')}Date taken</span>
      <span class="btn icon subtle">${icon('search', 's18')}</span>
      <span class="btn accent" style="height:36px;padding:0 16px">${icon('slideshow', 's18')}Slideshow</span></div>
    <div class="grid">${tiles}</div><div class="fade"></div>
    <div class="hint acrylic"><span class="kbd">←</span><span class="kbd">→</span> browse <span class="t3">·</span>
      <span class="kbd">Enter</span> open <span class="t3">·</span> <span class="kbd">Space</span> peek <span class="t3">·</span>
      <span class="kbd">F5</span> slideshow <span class="t3">·</span> <span class="kbd">Ctrl</span><span class="kbd">E</span> trim</div>`
  });
}

function trimView() {
  const N = 16;
  const frames = Array.from({ length: N }, (_, i) => thumb('dunes', 11, 90, 72, '', '', i / N * 4)).join('');
  const segs = [[0.08, 0.27, '1'], [0.41, 0.58, '2', true], [0.74, 0.86, '3']];
  let shade = '', last = 0;
  for (const [a, b] of segs) { shade += `<div class="shade" style="left:${last * 100}%;width:${(a - last) * 100}%"></div>`; last = b; }
  shade += `<div class="shade" style="left:${last * 100}%;right:0"></div>`;
  const boxes = segs.map(([a, b, n, cur]) => cur
    ? `<div class="segbox cur" style="left:${a * 100}%;width:${(b - a) * 100}%"></div>
       <div class="handle l" style="left:calc(${a * 100}% - 8px)">${icon('chevL', 's14')}</div>
       <div class="handle r" style="left:calc(${b * 100}% - 8px)">${icon('chevR', 's14')}</div>
       <div class="seglabel" style="left:${a * 100 + .5}%">${n}</div>`
    : `<div class="segbox" style="left:${a * 100}%;width:${(b - a) * 100}%"></div><div class="seglabel" style="left:${a * 100 + .5}%;opacity:.7">${n}</div>`).join('');
  const kfs = Array.from({ length: 21 }, (_, i) => `<div class="kf" style="left:${(i * 5 + 1.5)}%"></div>`).join('');
  const chips = [['1', '00:03.20', '00:11.04', '7.8 s'], ['2', '00:16.80', '00:23.84', '7.0 s', true], ['3', '00:30.40', '00:35.20', '4.8 s']]
    .map(([n, a, b, d, cur]) => `<div class="schip ${cur ? 'cur' : ''}"><span class="n">${n}</span><span class="num">${a} → ${b}</span><span class="t3 num">${d}</span></div>`).join('');
  return page({
    css: CSS, body: `
    <div class="stage" style="filter:brightness(.8)">${scene('dunes', 11, 0.5)}</div><div class="scrim-t"></div><div class="scrim-b" style="height:380px"></div>
    ${titleBar('Rub al Khali drone.mp4', '3 of 42 · Desert trip')}
    <div class="chip acrylic" style="top:60px;left:50%;transform:translateX(-50%)"><span class="dot"></span><b style="font-weight:600">Lossless trim</b><span class="t2">· stream copy, no re-encode · 3 segments kept</span></div>
    <div class="trim acrylic">
      <div class="trow">
        <span class="play" style="width:44px;height:44px;margin:0 4px 0 0">${icon('play')}</span>
        <span class="cbtn">${icon('keyPrev')}</span><span class="cbtn">${icon('frameBack')}</span><span class="cbtn">${icon('frameFwd')}</span><span class="cbtn">${icon('keyNext')}</span>
        <span class="num" style="font-size:22px;font-weight:600;margin:0 10px">00:19.76</span><span class="t3 num">/ 00:41.06</span>
        <span style="flex:1"></span>
        <span class="opt"><span class="toggle on"></span>Snap to keyframes</span><span class="sep"></span>
        <span class="opt"><span class="toggle on"></span>Merge into one file</span><span class="sep"></span>
        <span class="btn subtle">Cancel</span>
        <span class="btn accent" style="height:40px;padding:0 18px;font-size:14px">${icon('export', 's18')}Export 19.6 s</span>
      </div>
      <div class="strip"><div class="frames">${frames}</div>${shade}${kfs}${boxes}
        <div class="phead" style="left:48.1%"></div><div class="tcbubble num" style="left:48.1%">00:19.76 · ◆ keyframe</div></div>
      <div class="trow" style="margin-top:20px">${chips}<span style="flex:1"></span>
        <span class="btn subtle"><span class="kbd">I</span>Start</span><span class="btn subtle"><span class="kbd">O</span>End</span>
        <span class="btn subtle">${icon('plus', 's16')}Segment</span><span class="btn subtle">${icon('split', 's16')}Split</span>
        <span class="btn subtle">${icon('trash', 's16')}</span><span class="btn subtle">${icon('more', 's16')}</span></div>
    </div>`
  });
}

module.exports = {
  name: 'cinema',
  screens: [
    ['a-video-player.png', () => player()],
    ['b-folder-gallery.png', () => gallery()],
    ['c-lossless-trim.png', () => trimView()],
    ['d-video-player-ar-rtl.png', () => player({ rtl: true })],
  ],
};
module.exports.parts = { CSS, FRAME, seekBar, controls };
