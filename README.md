# CrashEngine

**A modding engine and level editor for _Crash Twinsanity_ (PlayStation 2).**

![Status](https://img.shields.io/badge/status-Alpha-orange)
![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%28experimental%29-blue)
![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)
![Graphics](https://img.shields.io/badge/OpenGL-4.6-5586A4)
![License](https://img.shields.io/badge/license-Noncommercial-lightgrey)

CrashEngine is a desktop tool for viewing, editing, and rebuilding the game data
of _Crash Twinsanity_. It reads and writes the game's native `RM2`/`SM2`
archives directly, renders levels with a real-time OpenGL viewport, and repacks
the results back into a bootable disc image.

> **Alpha software.** Early and functional, but expect bugs, missing features,
> and occasional crashes. Not production-ready, so save your work often.

> **Unofficial fan project.** Not affiliated with or endorsed by the rights
> holders of _Crash Bandicoot_. No copyrighted game data is included in this
> repository. You supply your own legally-obtained disc.

## Features

**Level editing:** Move, duplicate, swap, and delete scenery and objects with a
full 3D viewport, hierarchy, and inspector.

**Custom assets:** Import models as real in-game objects, build a personal
reusable asset library, and duplicate or array them with a live preview.

**Collision:** Generate and bake mesh collision, edit triggers, and assign
surface types.

**Lighting:** Real, positional scenery lighting reproduced from the original
engine.

**AI and behaviour:** Edit the enemy navigation graph and the AgentLab
behaviour scripts.

**Level linking:** Connect scenes, manage load zones, and author transitions.

**Play in the editor:** Run the level inside the editor with the original game
logic ported to C#, including characters, enemies, camera, sound, music, and
saves.

**Shadows:** The game's own character shadows, plus optional sun shadows.

**CrashEngine Hub:** Manage your projects, then install and play your mod like
a normal PC game.

**Disc building:** Repack edited data into a working ISO.

**Build to PC:** Export your mod as a standalone PC game.

**Regions and languages:** NTSC and PAL discs, and text in any language
through TrueType fonts.

**Linux (experimental):** The editor and the game run on Linux.

## Built With

**C# and .NET 9**

**[Silk.NET](https://github.com/dotnet/Silk.NET):** Windowing and OpenGL 4.6
bindings.

**Dear ImGui:** The editor interface.

## Project Structure

**CrashEngine Hub:** The starting point. Creates projects from your disc,
installs and plays your mod, and opens the editor.

**CrashLauncher:** The editor, with the 3D viewport, hierarchy, inspector, and
all the editing tools.

**CrashGame:** The game player that runs your mod as a PC game, outside the
editor.

**CrashEngine.Runtime:** The game's own logic ported to C#, covering
characters, enemies, AgentLab behaviours, camera, physics, sound, music, menus,
and saves. Both Play in the editor and CrashGame run on it.

**CrashEngine.Core:** The base of the engine, with scenes, entities,
components, input, timing, and shared math.

**CrashEngine.Renderer:** OpenGL drawing of meshes, materials, fog, shadows,
and the camera.

**CrashEngine.Importer:** Reads the game's level files, builds them into
editable scenes, and writes your edits back.

**CrashEngine.Assets:** Imports your own models and characters (FBX, glTF)
into the game.

**CrashEngine.Install:** Finds and unpacks game discs and packages the game
player. Shared by the Hub and the editor.

**CrashEngine.Platform:** Everything that talks to the operating system
directly, such as sound output, controllers, and system fonts, with a Windows
side and a Linux side.

**CrashEngine.ScriptGen:** Turns AgentLab behaviour scripts into C# classes.

**CrashEngine.Stealth:** Patrol paths and enemy awareness for the AI tools.

**TwinTech:** Reads and writes the game's file formats, based on TT Lab.

## Download

Grab the latest build from the [**Releases**](../../releases) page, unzip it
anywhere, and run **`CrashEngine Hub.exe`**. No installation is required. From
the Hub, create a project from your own disc image, then open it in the editor.

## Build from Source

For developers who want to build it themselves:

1. Install the [.NET 9 SDK](https://dotnet.microsoft.com/download).
2. Clone the repository.
3. Open `CrashEngine.sln` and build, or run:
   ```
   dotnet build CrashEngine.sln -c Release
   ```
4. Launch **CrashEngine Hub** and create a project from your own disc image.

## Support

CrashEngine is free and made in my spare time. If you enjoy it and want to help
it keep growing, you can support development at
[**creators.sa/amedo**](https://creators.sa/amedo). Every bit is appreciated,
and nothing in the project is locked behind support.

## Attribution

CrashEngine is built on the foundation of **TT Lab** by **Smartkin** and
**NeoKesha**, with thanks for making their work available. Full third-party
credits are in [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).

## License

The original work in this repository is licensed under the **PolyForm
Noncommercial License 1.0.0**. See [`LICENSE.md`](LICENSE.md). Third-party
components retain their own separate licenses, listed in
[`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).

© 2026 Amedo
