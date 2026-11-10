# Mashhad UI concepts

Three deliberately different directions for the WinUI 3 interface, for choosing before the app is
restyled. **The app has not been changed toward any of them.**

| Concept | One line |
| --- | --- |
| [A · Cinema](cinema/README.md) | Minimal and immersive: the media fills the window, controls float on acrylic and disappear. |
| [B · Library](library/README.md) | Gallery-first, like a modern Photos app: folder navigation, date-grouped grid, viewer with filmstrip and details. |
| [C · Studio](studio/README.md) | Editor-first, LosslessCut-style: one dense panel workspace with an always-visible multi-track timeline. |

Each concept has the same four screens, so they can be compared side by side:

| | Cinema | Library | Studio |
| --- | --- | --- | --- |
| a · video playing | [png](cinema/a-video-player.png) | [png](library/a-video-player.png) | [png](studio/a-video-player.png) |
| b · folder gallery | [png](cinema/b-folder-gallery.png) | [png](library/b-folder-gallery.png) | [png](studio/b-folder-gallery.png) |
| c · lossless trim | [png](cinema/c-lossless-trim.png) | [png](library/c-lossless-trim.png) | [png](studio/c-lossless-trim.png) |
| d · Arabic RTL | [player](cinema/d-video-player-ar-rtl.png) | [gallery](library/d-folder-gallery-ar-rtl.png) | [trim](studio/d-lossless-trim-ar-rtl.png) |

## Shared rules (all three)

- Dark Fluent / Windows 11 look; accent ramp and primary buttons use the logo gradient
  `#2FD8B8 → #16A3D9 → #2F5BE3`.
- **RTL:** in Arabic the whole layout mirrors (panes, title, menus, caption buttons). The **media
  timeline, seek bar and transport buttons do not mirror**: time runs left → right, following Material's
  bidirectionality guidance for media playback, and as the current app already does.
  Numbers such as `3840×2160` and timecodes are isolated so bidi never reorders them.
- Everything shown maps to features that exist in the current code (gallery, tracks, speed,
  A-B loop, keyframe snapping, merge, remux, frame export, EXIF), except where a README says otherwise.

## About the images

- The photos and video frames are **procedural illustrations**, not real media.
- **Inter** and **Noto Sans Arabic** stand in for Segoe UI Variable, which isn't available off Windows.
- The screens are 1600 × 1000, rendered with headless Chromium from HTML/CSS in [`_generator/`](_generator).
  Regenerate with:

```bash
node docs/design/ui-concepts/_generator/render.cjs            # all
node docs/design/ui-concepts/_generator/render.cjs studio     # one concept
```
