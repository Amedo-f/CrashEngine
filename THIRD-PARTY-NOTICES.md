# Third-Party Notices

CrashEngine incorporates the following third-party components. Each is the
property of its respective authors and is distributed under its own terms.

---

## TT Lab

CrashEngine is based on **TT Lab** by **Smartkin** and **NeoKesha**. Its engine
foundation — the Scene / Component / Entity system, the rendering pipeline, and
the **TwinTech** Twinsanity RM2/SM2 file-format library — originates from that
project.

- Source: <https://github.com/NeoKesha/TT-Lab>
- Authors: Smartkin, NeoKesha

---

## PS2ImageMaker

The **Build ISO** feature packs the PS2 disc image with **PS2ImageMaker** by
**Smartkin**, shipped unmodified as a prebuilt library: `PS2ImageMaker.dll` on
Windows and `libPS2ImageMaker.so` on Linux.

- Source: <https://github.com/Smartkin/PS2ImageMaker>
- Author: Smartkin
- License: GPL-3.0

---

## minizip (zlib)

On Linux, model import (Assimp) needs **minizip** from the zlib project. It is
shipped unmodified as `libminizip.so.1`, taken from the Ubuntu package
`libminizip1t64`. The full license text is in `libminizip.copyright` next to it.

- Source: <https://github.com/madler/zlib> (`contrib/minizip`)
- Authors: Gilles Vollant, Mathias Svensson, Jean-loup Gailly, Mark Adler
- License: zlib
