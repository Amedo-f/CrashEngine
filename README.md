# CrashEngine

**A modding engine and level editor for _Crash Twinsanity_ (PlayStation 2).**

![Status](https://img.shields.io/badge/status-Alpha-orange)
![Platform](https://img.shields.io/badge/platform-Windows-blue)
![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)
![Graphics](https://img.shields.io/badge/OpenGL-4.6-5586A4)
![License](https://img.shields.io/badge/license-Noncommercial-lightgrey)

CrashEngine is a desktop tool for viewing, editing, and rebuilding the game data
of _Crash Twinsanity_. It reads and writes the game's native `RM2`/`SM2`
archives directly, renders levels with a real-time OpenGL viewport, and repacks
the results back into a bootable disc image.

> **Alpha software.** Early and functional, but expect bugs, missing features,
> and occasional crashes. Not production-ready — save your work often.

> **Unofficial fan project.** Not affiliated with or endorsed by the rights
> holders of _Crash Bandicoot_. No copyrighted game data is included in this
> repository — you supply your own legally-obtained disc.

---

## Features

- **Level editing** — move, duplicate, swap, and delete scenery and objects with
  a full 3D viewport, hierarchy, and inspector.
- **Custom assets** — import models as real in-game objects, build a personal
  reusable asset library, and duplicate/array with live preview.
- **Collision** — generate and bake mesh collision, edit triggers, and assign
  surface types.
- **Lighting** — real, positional scenery lighting reproduced from the original
  engine.
- **AI & behaviour** — edit the enemy navigation graph and AgentLab behaviour
  scripts.
- **Level linking** — connect scenes, manage load zones, and author transitions.
- **Disc building** — repack edited data into a working ISO.

## Built With

- **C# / .NET 9**
- **[Silk.NET](https://github.com/dotnet/Silk.NET)** — windowing and OpenGL 4.6
  bindings
- **Dear ImGui** — editor UI

## Download

Grab the latest build from the [**Releases**](../../releases) page, unzip it
anywhere, and run **`CrashLauncher.exe`** — no installation required. Then open
a project folder containing your own extracted game data.

## Build from Source

For developers who want to build it themselves:

1. Install the [.NET 9 SDK](https://dotnet.microsoft.com/download).
2. Clone the repository.
3. Open `CrashEngine.sln` and build, or run:
   ```
   dotnet build CrashEngine.sln -c Release
   ```
4. Launch **CrashLauncher** and open a project folder containing your own
   extracted game data.

## Attribution

CrashEngine is built on the foundation of **TT Lab** by **Smartkin** and
**NeoKesha**, with thanks for making their work available. Full third-party
credits are in [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).

## License

The original work in this repository is licensed under the **PolyForm
Noncommercial License 1.0.0** — see [`LICENSE.md`](LICENSE.md). Third-party
components retain their own separate licenses, listed in
[`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).

---

© 2026 Amedo
