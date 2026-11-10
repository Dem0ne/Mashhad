# Mashhad UI concepts

Design directions for the WinUI 3 interface. **The app has not been restyled toward any of them yet.**

**Current proposal: [Hybrid](hybrid/README.md)**, which combines Cinema's playback, Library's folder
browsing (restyled in Cinema's visual language) and Studio's editor into one consistent app.
The Library concept was folded into Hybrid and removed; it remains in the git history.

| Direction | One line |
| --- | --- |
| [**Hybrid** (proposed)](hybrid/README.md) | Cinema playback · immersive folder browsing · Studio editing, with one floating chrome and animated mode changes. |
| [A · Cinema](cinema/README.md) | Reference: minimal and immersive; the media fills the window, controls float on acrylic and disappear. |
| [C · Studio](studio/README.md) | Reference: editor-first, LosslessCut-style; one dense panel workspace with a multi-track timeline. |

| | Hybrid | Cinema | Studio |
| --- | --- | --- | --- |
| a · video playing | [png](hybrid/a-video-player.png) | [png](cinema/a-video-player.png) | [png](studio/a-video-player.png) |
| b · folder gallery | [png](hybrid/b-folder-gallery.png) | [png](cinema/b-folder-gallery.png) | [png](studio/b-folder-gallery.png) |
| c · lossless trim | [png](hybrid/c-lossless-trim.png) | [png](cinema/c-lossless-trim.png) | [png](studio/c-lossless-trim.png) |
| d · Arabic RTL | [gallery](hybrid/d-folder-gallery-ar-rtl.png) | [player](cinema/d-video-player-ar-rtl.png) | [trim](studio/d-lossless-trim-ar-rtl.png) |
| e · transitions | [png](hybrid/e-mode-transitions.png) | | |

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
node docs/design/ui-concepts/_generator/render.cjs hybrid     # one concept
```
