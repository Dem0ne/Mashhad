# Concept A · Cinema

**Minimal, immersive. The media is the interface.**

The window is the photo or video, edge to edge. A slim title scrim and one floating acrylic dock
carry everything else and fade after a moment of no input. The folder browser is a full-window
"wall" of large tiles. Trimming happens on top of the video, in a phone-style filmstrip trimmer.

| Screen | |
| --- | --- |
| [a · video playing](a-video-player.png) | Floating dock, seek-preview bubble, A-B loop band (amber), chapter ticks, hardware-decoding chip, prev/next edge buttons. |
| [b · folder gallery](b-folder-gallery.png) | Mixed photo/video wall with filter pills, focus ring with caption, keyboard hint bar, one-click slideshow. |
| [c · lossless trim](c-lossless-trim.png) | Filmstrip trimmer over the video: keyframe diamonds, gradient-framed current segment with handles, segment chips, snap/merge toggles, Export. |
| [d · Arabic RTL player](d-video-player-ar-rtl.png) | Mirrored chrome and an Arabic subtitle-track menu. The transport and seek bar keep running left → right. |

**Best for:** people who mostly *watch and look*, and want the Photos/Films apps replaced by
something quieter and faster.

**Trade-offs**
- Least visible information; details live behind the info toggle.
- Editing is light: fine for "keep these 3 parts", cramped for long multi-segment edits.
- Floating controls *over* the video need a composition-based video surface. Today libmpv draws
  in its own child window, which XAML cannot overlay. This is the costliest direction to build.
