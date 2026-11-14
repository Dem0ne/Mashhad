# Hybrid: the combined direction

**Cinema for watching, Library's folder browsing in Cinema's clothes, Studio for editing: one app.**

This is the direction chosen from the first three concepts. It is a design proposal only; the app
has not been restyled yet.

| Screen | |
| --- | --- |
| [a · video playing](a-video-player.png) | Cinema unchanged: edge-to-edge video, floating acrylic dock (seek preview, A-B band, chapters), prev/next edge buttons. |
| [b · folder gallery](b-folder-gallery.png) | Library's practical browsing (folder pane, breadcrumb, filters, date/place groups, justified grid, multi-select) in Cinema's look: dark immersive wall, floating acrylic pane and bars, focus ring with caption. |
| [c · lossless trim](c-lossless-trim.png) | Studio's editor (media bin, preview, Export inspector with FFmpeg preview, segment, keyframe and waveform lanes) on floating rounded cards over the same dark wall. |
| [d · Arabic RTL gallery](d-folder-gallery-ar-rtl.png) | The folder view mirrored: pane, title pill, search and mode switcher swap sides; Arabic dates and labels. |
| [e · mode transitions](e-mode-transitions.png) | Storyboard of Folder → Play → Edit, the motion specs, and the keys that move between modes. |

## What makes it one app

- **One constant chrome.** The title pill (top left) and the **Folder · Play · Edit** switcher (top
  centre) appear in every mode, in the same place and style. They fade with the rest of the chrome when idle.
- **One material.** Everything sits on the dark brand-tinted "wall". Every control surface is
  floating acrylic with 14–20 px corners, and brand-gradient primary actions.
- **Transitions instead of page changes:**
  - Folder → Play: the tile expands into the window (connected animation, 300 ms).
  - Play → Edit: the video scales into the preview while the bin, inspector and timeline slide in (350 ms).
  - Esc: reverses either transition.
  - Playback never restarts between Play and Edit, because the same libmpv surface is resized.
- **Auto-hiding.** The dock, pills and folder pane fade out after 2.5 s without input and return
  when the pointer moves. F9 pins the folder pane. Edit mode keeps its panels pinned, since editing
  needs them.
- **RTL.** Chrome and panes mirror. The seek bar, transport and timeline still run left → right.

## Notes for building it

- **Floating chrome over playing video.** In Play the dock and pills float over the video. Today
  libmpv draws in its own native child window, and XAML can't draw on top of it. So Play mode
  needs either (a) libmpv's render API into a XAML `SwapChainPanel`, or (b) the chrome hosted in a
  transparent overlay window above the video. This is the main engineering cost of the design.
- **Folder and Edit modes are cheaper.** Their video sits in a plain rectangle beside the panels,
  which works with the current native video window.
- **Mostly existing features.** The screens show features that exist in the app today. New ones
  are the folder tree pane, date/place grouping, the waveform and thumbnail timeline lanes, and the
  FFmpeg command preview.
