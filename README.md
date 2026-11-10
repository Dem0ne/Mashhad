<div align="center">

<img src="assets/brand/mashhad-logo.svg" width="96" alt="Mashhad logo" />

# Mashhad · مشهد

**One fast, lightweight Windows app for photos *and* videos — with lossless cutting.**
**تطبيق ويندوز واحد، سريع وخفيف، للصور *والفيديو* — مع قصّ بلا فقد للجودة.**

[English](#english) · [العربية](#العربية)

</div>

---

## English

Mashhad combines the parts of **VLC**, **mpv** and **LosslessCut** that matter day to day, so one small app can
replace both the Windows Photos app and the default video player.

- **Engine:** embedded **libmpv** shows both videos and images; **FFmpeg** does editing/export.
  Hardware decoding (**D3D11VA**) is on by default. Native **WinUI 3** UI — no Electron, no browser.
- **Three modes, one window** ([design](docs/design/ui-concepts/hybrid/README.md)):
  - **Folder:** browse photos and videos together in a date-grouped, justified grid with a folder pane, filters,
    search and multi-select.
  - **Play:** Cinema-style. The picture fills the window and controls float over it, then fade away.
  - **Edit:** Studio-style lossless editor with a multi-lane timeline.

  Switching modes is animated, and Escape always goes back.
- **Unified gallery:** arrow keys move through *every image and video in the same folder*. Neighbouring files are
  pre-read in the background, so the next one opens without disk latency.
- **Video:** everything FFmpeg can decode, subtitles (embedded, external, drag & drop), audio/subtitle track
  selection, speed, frame stepping, A-B loop, screenshots, resume position, playlists (M3U), drag & drop.
- **Images:** JPG, PNG, WebP, GIF/APNG (animated), HEIC/AVIF/JXL where the bundled FFmpeg can decode them;
  zoom to cursor, pan, rotate, EXIF panel, crop.
- **Lossless editing:** in/out points on the timeline, multiple segments, cut and merge with stream copy
  (no re-encode), keyframe-aware cutting, frame export, remux to MKV/MP4/MOV.
- **Slideshow** mixing images (timed) and videos (played to the end).
- **Arabic and English** with a full right-to-left layout, in a dark theme with the crimson brand gradient
  (`#FF5E7E → #E6223F → #8E1035`).
- **UI font:** Segoe UI Variable (Consolas for timecodes). Thmanyah Sans is embedded, encrypted, in official
  builds but not applied yet — see [The UI font](#the-ui-font).

### Status

This is a first version. What has and has not been verified is listed honestly under
[Verification](#verification) and [Known gaps](#known-gaps).

### Keyboard and mouse

| Action | Keys |
| --- | --- |
| Play / pause | `Space`, `K`, right-click |
| Seek ±5 s / ±1 s exact / ±60 s | `←` `→` / `Shift`+`←` `→` / `Ctrl`+`←` `→` |
| Previous / next file in folder | `PageUp` `PageDown`, `P` `N`, mouse back/forward (images: also `←` `→`) |
| Frame step back / forward | `,` / `.` |
| Speed down / up / reset | `[` / `]` / `Backspace` |
| Volume / mute | `↑` `↓`, `9` `0`, mouse wheel / `M` |
| Audio track / subtitle track / subtitles on-off | `B` / `J` / `V` |
| Subtitle delay | `Z` / `X` |
| A-B loop (A, B, clear) / repeat file | `L` / `Shift`+`L` |
| Screenshot (with / without subtitles) | `S` / `Shift`+`S` |
| Full screen | `F`, `F11`, `Alt`+`Enter`, double-click; `Esc` leaves |
| Slideshow | `F5` |
| Images: zoom in / out / fit / 100 % | `+` / `-` / `0` / `1`, mouse wheel (zooms at the cursor), drag to pan |
| Images: rotate / crop | `R`, `Shift`+`R` / `C` (drag a rectangle, `Enter` saves a copy) |
| Back (full screen → Edit → Play → Folder) | `Esc` |
| Folder: move / open / select / select all / delete | arrows / `Enter` (or double-click) / `Space`, `Ctrl`/`Shift`+click / `Ctrl`+`A` / `Delete` (Recycle Bin) |
| Folder: pin or auto-hide the folder pane | `F9` |
| Lossless edit mode (Play ⇄ Edit) | `Ctrl`+`E` |
| Edit: in / out / new segment / split / remove | `I` / `O` / `+` / `B` / `Delete` |
| Edit: previous / next keyframe | `Alt`+`←` / `Alt`+`→` |
| Edit: export | `Ctrl`+`Enter` |
| Open file / folder, info, playlist, settings | `Ctrl`+`O` / `Ctrl`+`Shift`+`O`, `Ctrl`+`I`, `Ctrl`+`L`, `Ctrl`+`,` |

### How lossless cutting works

Export never re-encodes: FFmpeg copies the compressed packets (`-c copy`).

- A stream copy can only **start on a keyframe**. With *Snap to keyframes* on (default), each in point moves
  back to the previous keyframe, so the output starts cleanly and the file name shows the real times.
- The **out point** is exact for streams without B-frames; with B-frames the last frame or two
  around it can differ (the reorder depth). This is inherent to stream copy.
- Several segments can be exported separately or **merged** into one file (FFmpeg concat demuxer).
- Output is written next to the source (or to the export folder in Settings) and never overwrites a file.
- Image rotate/crop writes a **new** file: PNG output is lossless; JPEG is re-encoded at high quality
  (true lossless JPEG transforms are not implemented).

### Build and run (Windows)

Requirements: Windows 10 1809+ or Windows 11 (x64), the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0),
PowerShell. Visual Studio is optional. For the installer: [Inno Setup 6](https://jrsoftware.org/isinfo.php).

```powershell
git clone https://github.com/dem0ne/mashhad.git
cd mashhad

# 1. Download libmpv and FFmpeg into .\native (see below)
.\scripts\fetch-deps.ps1

# 2. Run from source
dotnet run --project src/Mashhad.App -p:Platform=x64

# 3. Or build a self-contained folder / portable zip / installer
.\scripts\build.ps1                      # publish\Mashhad\Mashhad.exe
.\scripts\build.ps1 -Zip -Installer      # dist\Mashhad-<ver>-win-x64.zip, dist\Mashhad-Setup-<ver>.exe
```

The build is **unpackaged and self-contained**: the output folder runs on a clean machine without installing
.NET or the Windows App SDK runtime. Every CI run also uploads the installer and zip as build artifacts.

Run the tests (works on Windows and Linux; FFmpeg/libmpv tests skip themselves if those are not installed):

```powershell
dotnet test tests/Mashhad.Tests
```

### Where libmpv and FFmpeg come from

They are **not** committed to this repository. `scripts/fetch-deps.ps1` downloads:

| Engine | Source | Files placed in |
| --- | --- | --- |
| libmpv | latest `mpv-dev-x86_64-*.7z` from [shinchiro/mpv-winbuild-cmake](https://github.com/shinchiro/mpv-winbuild-cmake/releases) | `native\mpv\libmpv-2.dll` |
| FFmpeg | latest release-branch `win64-lgpl-shared` build from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds/releases) | `native\ffmpeg\ffmpeg.exe`, `ffprobe.exe`, DLLs |

The app project copies those folders next to `Mashhad.exe` (`mpv\` and `ffmpeg\`). You can drop in your own builds
instead, pin exact versions with `-MpvUrl` / `-FfmpegUrl`, or point the app at another FFmpeg folder in Settings
(it also falls back to `PATH`). Extracting the mpv archive needs 7-Zip or a recent Windows `tar`.

**Licensing note:** the shinchiro libmpv builds are GPL; the BtbN build used here is LGPL. If you distribute
binaries that bundle them you must comply with those licences. This repository does not yet contain a licence
file for Mashhad's own code — that choice is the owner's.

### File associations and default app

The installer (per-user by default, no admin prompt) registers Mashhad for common image, video and audio
extensions through `OpenWithProgids` and `RegisteredApplications`. Windows 10/11 do not allow an app to make
itself the default silently, so the last step is yours: **Settings → Apps → Default apps → Mashhad** (the
installer and the in-app Settings panel both offer a shortcut to that page). Opening a second file reuses the
running window.

### Project layout

```
src/Mashhad.Core      media logic, no UI: gallery, queue, preloader, resume, shortcuts, image/crop math,
                      settings, localisation (en/ar), libmpv interop                       (net8.0)
src/Mashhad.Editing   FFmpeg wrapper: segments, keyframe index, command builder, planner, runner (net8.0)
src/Mashhad.App       WinUI 3 app (Windows App SDK 1.7): modes, views (built in C#), native video host,
                      Play overlay window                                                 (net8.0-windows)
tests/Mashhad.Tests   xUnit tests incl. real FFmpeg and headless libmpv integration tests
installer/            Inno Setup script (file associations)
scripts/              fetch-deps.ps1, build.ps1
assets/brand/         logo
```

**How the video gets into a WinUI window.** WinUI 3 has no native child-window control, so the app does this:
1. It creates a plain Win32 child window and hands its handle to libmpv (`--wid`).
2. It keeps that window over the area the current mode gives the video: the whole window in Play, the preview in
   Edit. During mode changes it moves the window smoothly.
3. mpv renders with its own D3D11 pipeline (zero-copy D3D11VA), which is the lowest-overhead path.

XAML cannot draw *on top of* that window. So Play mode's floating chrome lives in a separate, borderless,
transparent overlay window that the main window owns and keeps exactly over its client area. If the overlay
cannot be created, the controls dock below the video instead.

**Views.** The views (folder, editor, player chrome, settings) are built in C# rather than XAML. That lets the
whole app type-check off Windows.

### Verification

Verified in automated tests (run on Linux, and again on Windows in CI):

- Gallery scanning/sorting, queue navigation, shuffle, playlists, preloader, resume store, slideshow policy,
  shortcut map, settings, zoom/pan/crop math, localisation (key parity, every key used by the UI exists).
- **Lossless cut/merge/remux against real FFmpeg**: outputs are decoded and compared frame-by-frame with the
  source for MP4, MKV, MOV and MPEG-TS, with and without B-frames; also frame export, image rotate/crop,
  failure clean-up and cancellation.
- **libmpv interop against a real libmpv** (headless): load, properties and events, exact seek, frame step,
  speed, A-B loop, resume start position, still images with zoom/pan/rotate, error paths, and every mpv
  command/property the app uses.

On Windows, CI also builds the app and runs `Mashhad.exe --smoke-test`. On a folder this is a tour: Folder →
Play (real video playback) → Edit (keyframes, waveform, thumbnails, segments, export preview) → Arabic RTL in
each mode → an image with the info panel. CI screenshots each step and prints the images into the job log. It
also starts the app empty, on a single video (with and without the overlay window) and on a single image.

### The UI font

Thmanyah Sans may not be redistributed as font files, so the repository only holds it encrypted
(`assets/fonts/encrypted/*.otf.enc`, AES-256-GCM). In CI, `scripts/embed-font-key.ps1` reads the
`MASHHAD_FONT_KEY` secret and compiles it into the app in a masked form for that build only (the generated
file is git-ignored and deleted after the build); the encrypted fonts are embedded as resources in
`Mashhad.exe`. A test decrypts every weight with the key, and at start-up the app decrypts them in memory and
logs the result. The build fails if any `.otf`/`.ttf`/`.enc` file ends up in the output.

The font is **not applied yet**: an unpackaged WinUI 3 app renders custom fonts only from its install folder
(`ms-appx`) or from fonts installed for the user. CI confirmed that a file path, a `file://` URI and a
process-private registration are all ignored. Both workable routes conflict with the licence terms, so the UI
uses Segoe UI Variable and no decrypted font is ever written to disk. Builds without the secret skip the
font entirely.

### Known gaps

- The UI has been exercised only by the automated smoke test, **not by a person on a real desktop**: expect
  visual rough edges. GPU hardware decoding cannot be exercised on CI machines (no GPU).
- The Play chrome floats over the video in a transparent window. Its panels are translucent but not blurred
  (true acrylic cannot see the video window), and the pointer is not hidden when the chrome fades.
- The approved design is dark only; there is no light theme.
- "Preload" warms the OS file cache for the neighbouring files; it does not pre-decode them.
- Not implemented: lossless JPEG rotation/crop, video crop, choosing individual streams on export (all tracks or
  video+audio+subtitles), chapter editing, renaming segments in the UI, zooming the timeline.
- HEIC/AVIF depend on the decoders in the bundled libmpv build.
- x64 only; no MSIX/Store package; no code signing (SmartScreen will warn on the installer).
- Thmanyah Sans is embedded but not applied (see [The UI font](#the-ui-font)). The font key is masked, not
  secret: a determined person with the binary and a debugger can recover the fonts.

---

## العربية

<div dir="rtl">

«مشهد» يجمع أهم ما في **VLC** و**mpv** و**LosslessCut** في تطبيق صغير واحد، ليغني عن تطبيق «الصور» ومشغّل الفيديو
الافتراضي في ويندوز معًا.

- **المحرك:** مكتبة **libmpv** مضمَّنة لعرض الفيديو والصور، و**FFmpeg** للتحرير والتصدير. فكّ الترميز بالعتاد
  (**D3D11VA**) مفعّل افتراضيًا. واجهة **WinUI 3** أصلية — بلا Electron ولا متصفح.
- **ثلاثة أوضاع في نافذة واحدة:**
  - **المجلد:** تصفّح الصور والفيديو معًا في شبكة مرتّبة حسب التاريخ، مع لوحة مجلدات وتصفية وبحث وتحديد متعدد.
  - **التشغيل:** بأسلوب Cinema؛ الصورة تملأ النافذة وأدوات التحكم عائمة فوقها ثم تختفي.
  - **التحرير:** بأسلوب Studio؛ محرر بلا فقد بخط زمني متعدد المسارات.

  التنقل بين الأوضاع متحرك، و`Esc` يعود دائمًا خطوة إلى الوراء.
- **معرض موحَّد:** مفاتيح الأسهم تتنقل بين *كل الصور والفيديوهات في المجلد نفسه*، وتُقرأ الملفات المجاورة مسبقًا.
- **الفيديو:** كل الصيغ التي يدعمها FFmpeg، الترجمات (المضمّنة والخارجية وبالسحب والإفلات)، اختيار مسار الصوت
  والترجمة، السرعة، التنقل إطارًا بإطار، تكرار A-B، لقطات الشاشة، متابعة التشغيل من موضع التوقف، قوائم التشغيل.
- **الصور:** JPG و PNG و WebP و GIF المتحركة، و HEIC/AVIF حيث أمكن؛ تكبير عند المؤشر، تحريك، تدوير، لوحة EXIF، قص.
- **تحرير بلا فقد:** نقاط بداية ونهاية على الخط الزمني، مقاطع متعددة، قص ودمج بنسخ المسارات (دون إعادة ترميز)،
  قص يراعي الإطارات المفتاحية، تصدير الإطارات، تحويل الحاوية إلى MKV/MP4/MOV.
- **عرض شرائح** يمزج الصور (بمدة محددة) والفيديو (حتى نهايته).
- **العربية والإنجليزية** مع تخطيط كامل من اليمين إلى اليسار، بمظهر داكن وتدرّج العلامة القرمزي
  (`#FF5E7E → #E6223F → #8E1035`).

### الحالة

هذه نسخة أولى. ما تم التحقق منه وما لم يتم مذكور بصراحة في قسمَي «التحقق» و«النواقص المعروفة».

### أهم الاختصارات

| الإجراء | المفاتيح |
| --- | --- |
| تشغيل / إيقاف مؤقت | `Space`، `K`، النقر بالزر الأيمن |
| تقديم/ترجيع ٥ ث / ١ ث بدقة / ٦٠ ث | `←` `→` / `Shift`+`←` `→` / `Ctrl`+`←` `→` |
| الملف السابق / التالي في المجلد | `PageUp` `PageDown`، `P` `N` (وفي الصور: `←` `→`) |
| إطار للخلف / للأمام | `,` / `.` |
| السرعة: أبطأ / أسرع / إعادة | `[` / `]` / `Backspace` |
| الصوت / كتم | `↑` `↓`، عجلة الفأرة / `M` |
| مسار الصوت / الترجمة / إظهار الترجمة | `B` / `J` / `V` |
| تكرار A-B | `L` |
| لقطة شاشة | `S` |
| ملء الشاشة | `F`، `F11`، نقر مزدوج؛ `Esc` للخروج |
| عرض الشرائح | `F5` |
| الصور: تكبير / تصغير / ملاءمة / ‎100% | `+` / `-` / `0` / `1`، عجلة الفأرة، السحب للتحريك |
| الصور: تدوير / قص | `R` / `C` |
| رجوع (ملء الشاشة ← التحرير ← التشغيل ← المجلد) | `Esc` |
| المجلد: تنقّل / فتح / تحديد / تحديد الكل / حذف | الأسهم / `Enter` / `Space` / `Ctrl`+`A` / `Delete` (سلة المحذوفات) |
| المجلد: تثبيت لوحة المجلدات أو إخفاؤها تلقائيًا | `F9` |
| وضع التحرير بلا فقد (التشغيل ⇄ التحرير) | `Ctrl`+`E` |
| التحرير: بداية / نهاية / مقطع جديد / تقسيم / حذف | `I` / `O` / `+` / `B` / `Delete` |
| التحرير: الإطار المفتاحي السابق / التالي | `Alt`+`←` / `Alt`+`→` |
| التحرير: تصدير | `Ctrl`+`Enter` |

### كيف يعمل القص بلا فقد

التصدير لا يعيد الترميز أبدًا: ينسخ FFmpeg الحزم المضغوطة كما هي (`-c copy`).

- النسخ لا يبدأ إلا من **إطار مفتاحي**. مع خيار «المحاذاة للإطارات المفتاحية» (الافتراضي) تُرجَع كل نقطة بداية إلى
  الإطار المفتاحي السابق، فيبدأ الناتج نظيفًا ويُظهر اسم الملف الأزمنة الفعلية.
- **نقطة النهاية** دقيقة في المسارات الخالية من إطارات B، وقد يختلف آخر إطار أو إطارين حولها عند وجودها. هذا من طبيعة النسخ.
- يمكن تصدير المقاطع منفصلة أو **دمجها** في ملف واحد.
- يُكتب الناتج بجانب الملف الأصلي (أو في مجلد التصدير المحدد في الإعدادات) ولا يُستبدل أي ملف موجود.
- تدوير/قص الصور يكتب ملفًا **جديدًا**: ‏PNG بلا فقد، و JPEG يُعاد ترميزه بجودة عالية.

### البناء والتشغيل (ويندوز)

المتطلبات: ويندوز 10 (1809 فما فوق) أو ويندوز 11 (x64)، و[‎.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)،
و PowerShell. ‏Visual Studio اختياري. ولبناء المثبِّت: [Inno Setup 6](https://jrsoftware.org/isinfo.php).

</div>

```powershell
git clone https://github.com/dem0ne/mashhad.git
cd mashhad

# 1. تنزيل libmpv و FFmpeg إلى المجلد native
.\scripts\fetch-deps.ps1

# 2. التشغيل من المصدر
dotnet run --project src/Mashhad.App -p:Platform=x64

# 3. أو بناء مجلد مستقل / ملف مضغوط محمول / مثبِّت
.\scripts\build.ps1
.\scripts\build.ps1 -Zip -Installer
```

<div dir="rtl">

الناتج **مستقل بذاته**: يعمل على جهاز نظيف دون تثبيت ‎.NET أو Windows App SDK. كما يرفع كل تشغيل للـ CI المثبِّتَ
والملف المضغوط كمخرجات بناء.

تشغيل الاختبارات (على ويندوز أو لينكس؛ اختبارات FFmpeg و libmpv تتخطى نفسها إن لم يكونا مثبتين):

</div>

```powershell
dotnet test tests/Mashhad.Tests
```

<div dir="rtl">

### من أين تأتي ملفات libmpv و FFmpeg

هذه الملفات **غير مضمّنة** في المستودع. السكربت `scripts/fetch-deps.ps1` ينزّل:

| المحرك | المصدر | مكان الملفات |
| --- | --- | --- |
| libmpv | أحدث `mpv-dev-x86_64-*.7z` من [shinchiro/mpv-winbuild-cmake](https://github.com/shinchiro/mpv-winbuild-cmake/releases) | `native\mpv\libmpv-2.dll` |
| FFmpeg | أحدث بناء `win64-lgpl-shared` من [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds/releases) | `native\ffmpeg\` |

ينسخ مشروع التطبيق هذين المجلدين بجانب `Mashhad.exe`. يمكنك وضع نسخك الخاصة بدلًا منها، أو تثبيت إصدارات محددة
بالخيارين `-MpvUrl` و `-FfmpegUrl`، أو تحديد مجلد FFmpeg آخر من الإعدادات. فكّ أرشيف mpv يحتاج 7-Zip أو `tar`
حديثًا في ويندوز.

**ملاحظة ترخيص:** بناءات libmpv من shinchiro مرخّصة بـ GPL، وبناء FFmpeg المستخدم هنا بـ LGPL؛ توزيعها مع التطبيق
يستلزم الالتزام بهذه التراخيص. ولم يُضَف بعدُ ملف ترخيص لشيفرة «مشهد» نفسها — القرار لمالك المستودع.

### ربط أنواع الملفات والتطبيق الافتراضي

يسجّل المثبِّت (للمستخدم الحالي افتراضيًا، دون صلاحيات مدير) «مشهد» لأشهر امتدادات الصور والفيديو والصوت. ويندوز
10/11 لا يسمح لأي تطبيق بأن يجعل نفسه افتراضيًا تلقائيًا، فالخطوة الأخيرة عندك:
**الإعدادات ← التطبيقات ← التطبيقات الافتراضية ← Mashhad**. فتح ملف ثانٍ يستخدم النافذة المفتوحة نفسها.

### التحقق

تم التحقق باختبارات آلية (على لينكس، ثم على ويندوز في CI):

- منطق المعرض والترتيب والتنقل وقوائم التشغيل والتحميل المسبق ومتابعة التشغيل وعرض الشرائح والاختصارات والإعدادات
  وحسابات التكبير والقص، والترجمة (تطابق المفاتيح بين اللغتين ووجود كل مفتاح تستخدمه الواجهة).
- **القص والدمج وتحويل الحاوية بلا فقد على FFmpeg حقيقي**: تُفكّ ترميز النواتج وتُقارن إطارًا بإطار مع الأصل لصيغ
  MP4 و MKV و MOV و MPEG-TS، مع إطارات B وبدونها.
- **الربط مع libmpv حقيقي** (دون شاشة): التحميل، الخصائص والأحداث، التنقل الدقيق، إطار بإطار، السرعة، تكرار A-B،
  موضع المتابعة، الصور الثابتة مع التكبير والتدوير، ومسارات الخطأ.

على ويندوز يبني الـ CI التطبيق ويشغّل جولة آلية: المجلد ← التشغيل (فيديو حقيقي) ← التحرير ← العربية في كل وضع ←
صورة مع لوحة المعلومات، ويلتقط صورة لكل خطوة.

### خط الواجهة

الواجهة بخط Segoe UI Variable، و Consolas للأزمنة. رخصة خط **ثمانية** ([Thmanyah Sans](https://font.thmanyah.com))
تمنع توزيعه كملفات، لذا لا يحوي المستودع إلا نسخة مشفّرة (AES-256-GCM) في `assets/fonts/encrypted`. في الـ CI يقرأ
`scripts/embed-font-key.ps1` السر `MASHHAD_FONT_KEY` ويُدرجه في التطبيق مموَّهًا لذلك البناء فقط، وتُضمَّن الخطوط
المشفّرة موارد داخل `Mashhad.exe`؛ ويفك اختبارٌ كل الأوزان بالمفتاح، ويفكها التطبيق في الذاكرة عند التشغيل للتحقق.

لكن الخط **لم يُطبَّق بعد**: تطبيق WinUI 3 غير المحزَّم لا يعرض خطًا مخصصًا إلا من مجلد التثبيت أو من خط مثبَّت
للمستخدم، وكلاهما يتعارض مع الرخصة (وقد تأكد الـ CI أن المسار المباشر و `file://` والتسجيل الخاص بالعملية لا تعمل).
لذلك لا يُكتب أي خط مفكوك على القرص.

### النواقص المعروفة

- الواجهة جُرّبت بالاختبار الآلي فقط، **ولم يجرّبها إنسان على سطح مكتب حقيقي**: توقّع بعض العيوب الشكلية. ولا يمكن
  اختبار فك الترميز بالعتاد على أجهزة CI (بلا بطاقة رسوميات).
- أدوات التشغيل العائمة في نافذة شفافة فوق الفيديو: ألواحها شبه شفافة بلا تمويه، والمؤشر لا يُخفى عند اختفاء الأدوات.
- التصميم المعتمد داكن فقط؛ لا يوجد مظهر فاتح.
- «التحميل المسبق» يُدفئ ذاكرة الملفات المؤقتة للملفات المجاورة ولا يفك ترميزها مسبقًا.
- غير منفَّذ بعد: تدوير/قص JPEG بلا فقد، قص الفيديو، اختيار مسارات بعينها عند التصدير، تحرير الفصول، إعادة تسمية
  المقاطع من الواجهة، تكبير الخط الزمني.
- ‏x64 فقط؛ لا حزمة MSIX؛ المثبِّت غير موقَّع رقميًا.
- خط ثمانية مضمَّن لكنه غير مطبَّق بعد. ومفتاح الخط مموَّه لا سرّي: من يملك الملف التنفيذي ومنقِّحًا يستطيع استخراج
  الخطوط.

</div>
