# Concept C · Studio

**Editor-first: LosslessCut's power in a native Fluent workspace.**

One dense, panel-based layout that stays the same in three modes (**Browse · Play · Cut**):
media bin, preview, inspector and a multi-track timeline. The timeline has a segments lane,
video thumbnails, a keyframe lane and per-track waveforms.

| Screen | |
| --- | --- |
| [a · video playing](a-video-player.png) | Play mode: bin, preview with subtitles, Tracks inspector (audio, subtitles, delay, speed, A-B loop, decoder stats), chapters and waveform timeline. |
| [b · folder gallery](b-folder-gallery.png) | Browse mode: folder tree and collections, captioned grid, inspector with preview, histogram, EXIF, rating and tags. |
| [c · lossless trim](c-lossless-trim.png) | Cut mode: segment lane with handles, keyframe lane (snapped keyframe highlighted), two audio waveforms, Export inspector with the exact FFmpeg command preview. |
| [d · Arabic RTL trim](d-lossless-trim-ar-rtl.png) | Panels mirrored (bin on the right, inspector on the left), Arabic labels; the timeline still runs left → right. |

**Best for:** people who cut video often and want precise, keyframe-level control. It makes the
"lossless" part of Mashhad its headline.

**Trade-offs**
- Busiest of the three; a heavy look for someone who just opened a holiday photo, though Browse
  mode softens that.
- Needs the most new UI: multi-lane timeline, waveforms (audio peak extraction via FFmpeg) and
  thumbnail strips. Rating and tags would be new features.
- Fits the native video window well: the preview is a plain rectangle.
