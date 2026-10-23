using CrashEngine.Assets;
using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

// Amedo
public sealed partial class GameScene
{
    // Amedo
    private void DrawMainMenus()
    {
        bool scene = _workspace == Workspace.Scene;
        if (ImGui.BeginMenu("File")) { DrawFileMenu(); ImGui.EndMenu(); }
        if (ImGui.BeginMenu("Edit")) { DrawEditMenu(); ImGui.EndMenu(); }
        if (ImGui.BeginMenu("GameObject", scene)) { DrawGameObjectMenu(); ImGui.EndMenu(); }
        if (ImGui.BeginMenu("Collision", scene)) { DrawCollisionMenu(); ImGui.EndMenu(); }
        if (ImGui.BeginMenu("View", scene)) { DrawViewMenu(); ImGui.EndMenu(); }
        if (ImGui.BeginMenu("Tools", scene)) { DrawToolsMenu(); ImGui.EndMenu(); }
        if (ImGui.BeginMenu("Window")) { DrawWindowMenu(); ImGui.EndMenu(); }
        if (ImGui.BeginMenu("Help")) { DrawHelpMenu(); ImGui.EndMenu(); }
        ImGui.TextDisabled("|");
    }

    // Amedo
    private void DrawFileMenu()
    {
        ImGui.BeginDisabled(_building);
        if (ImGui.MenuItem("Save Chunk (.rm2/.sm2)")) SaveChunk();
        ImGui.Separator();
        if (ImGui.MenuItem("Reload Level (keep saved edits)")) ReloadLevel(fromDiscOnly: false);
        if (ImGui.MenuItem("Reload Level from Disc (discard saved edits)")) ReloadLevel(fromDiscOnly: true);
        if (ImGui.IsItemHovered())
            MaybeTooltip("Re-extracts just THIS level fresh from the real disc archive —\nignores SavedChunks entirely. Use this to recover from a broken\nsaved edit (e.g. a bad texture import) without redoing the whole\nCreate Project extraction. Any unsaved live edits on this level\nare lost too.");
        ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1f, 0.45f, 0.45f, 1f));
        if (ImGui.MenuItem("Reset ALL Levels From Disc (delete all saved edits)...")) _openResetAllConfirmPopup = true;
        ImGui.PopStyleColor();
        if (ImGui.IsItemHovered())
            MaybeTooltip("PROJECT-WIDE, not just this level: permanently deletes the entire\nSavedChunks folder (every level's saved edits) so everything reads\nfresh from the real disc archive again. No backup, no Undo — asks\nfor confirmation first.");
        ImGui.Separator();
        if (ImGui.MenuItem("Build ISO (PS2)")) BuildIso();
        if (ImGui.BeginMenu("ISO Output Path"))
        {
            EnsureIsoOutputPathLoaded();
            ImGui.TextDisabled("ISO output (blank = default path):");
            ImGui.SetNextItemWidth(360f);
            if (ImGui.InputText("##isoOutPath", ref _isoOutputPath, 512u)) SaveIsoOutputPath();
            ImGui.SameLine();
            if (ImGui.Button("...##isoOutBrowse"))
                ShowSaveFileDialog("Choose ISO output file (overwritten every build)", "PS2 ISO\0*.iso\0All Files\0*.*\0\0", "game_test.iso", path =>
                {
                    if (path is null) return;
                    _isoOutputPath = path;
                    SaveIsoOutputPath();
                });
            ImGui.EndMenu();
        }
        if (ImGui.MenuItem(_buildingPc ? "Building PC..." : "Build to PC", null, false, !_buildingPc)) BuildPc();
        if (ImGui.IsItemHovered())
            MaybeTooltip("Builds a Windows version of the game (demo) with the CrashGame player:\nit plays THIS level from this project's saved chunks + the extracted disc,\nwithout the editor. Output: <project>\\Build\\PC\\CrashGame.exe (runs without\ninstalling anything). Save Chunk first. Needs the .NET SDK (dotnet).\nIn the game: Esc quits, F1 shows/hides stats, F3 frame rate.");
        ImGui.EndDisabled();
        ImGui.Separator();
        if (ImGui.MenuItem("Import Object (OBJ + textures)..."))
        {
            bool flipX = _flipModelX;
            ShowOpenFileDialog("Import Object (real, as PS2 Object)", ModelImporter.FileFilter, modelPath =>
            {
                if (modelPath != null) BakeExternalModel(modelPath, flipX);
            });
        }
        if (ImGui.IsItemHovered())
            MaybeTooltip("Imports a mesh file (.obj + its .mtl + textures, or .glb/.fbx/etc.) as\na REAL PS2 RigidModel/OGI/Object, placed as a normal Instance at\nthe camera. Survives Save Chunk + Build ISO.");
        if (ImGui.MenuItem("Export Collision to OBJ...")) ExportCollisionToObj();
        if (ImGui.IsItemHovered())
            MaybeTooltip("Writes this level's ENTIRE current collision to a plain .obj file — edit it\nin Blender, then use \"Import Collision from OBJ...\" to bring it back.\nVertex ORDER is preserved exactly — don't reorder/reindex vertices in Blender.");
        if (ImGui.MenuItem("Import Collision from OBJ...")) ImportCollisionFromObj();
        if (ImGui.IsItemHovered())
            MaybeTooltip("Replaces this level's ENTIRE collision from a .obj file — use the SAME\nfile \"Export Collision to OBJ...\" wrote. Triggers are left untouched.\nSave Chunk + Build ISO to test.");
    }

    // Amedo
    private void DrawEditMenu()
    {
        bool anim = _workspace == Workspace.Animation;
        if (ImGui.MenuItem("Undo", "Ctrl+Z", false, anim ? _animUndo.CanUndo : _undoStack.CanUndo))
        {
            if (anim) AnimUndo();
            else { CommitInspectorEdit(force: true); SelectAffected(_undoStack.Undo()); } // Amedo
        }
        if (ImGui.MenuItem("Redo", "Ctrl+Y", false, anim ? _animUndo.CanRedo : _undoStack.CanRedo))
        {
            if (anim) AnimRedo();
            else SelectAffected(_undoStack.Redo());
        }
        if (anim) return;
        ImGui.Separator();
        if (ImGui.MenuItem("Deselect")) SelectClicked(null, additive: false);
        if (ImGui.MenuItem("Delete Selected", "Del", false, _selectedSet.Count > 0)) DeleteSelected();
        if (ImGui.IsItemHovered())
            MaybeTooltip("Only real PS2 objects/scenery tiles are removed from the archive —\ncubes just disappear from the editor.");
    }

    // Amedo
    private void DrawGameObjectMenu()
    {
        if (ImGui.MenuItem("Cube")) _pendingSpawn = true;
        if (ImGui.IsItemHovered())
            MaybeTooltip("A real, unit-sized (1x1x1) box baked as a genuine PS2 RigidModel/OGI/Object,\nplaced as a normal Instance at the camera. Import Texture (Inspector) can\nreplace its gray texture. Save Chunk to keep it, then Build ISO + test.");
        ImGui.Separator();
        if (ImGui.MenuItem("Crash (full, with scripts)...")) _showCrashPicker = true;
        if (ImGui.IsItemHovered())
            MaybeTooltip("Crash is defined LOCALLY per-level — this does a real full transplant\n(object + OGIs + animations + behaviours/scripts + sounds) from a level\nthat already has a working Crash.");
        if (ImGui.MenuItem("Object (full, from another level)..."))
        {
            _objectTransplantLevelFilter = "";
            _showObjectTransplantLevelPicker = true;
        }
        if (ImGui.IsItemHovered())
            MaybeTooltip("Full transplant of ANY object from ANY level — pick the source level,\nthen the object by name. Save Chunk to keep it, then Build ISO + test.");
        if (ImGui.MenuItem("Trigger (from another level)..."))
        {
            _triggerTransplantLevelFilter = "";
            _showTriggerTransplantLevelPicker = true;
        }
        if (ImGui.IsItemHovered())
            MaybeTooltip("Copies a Trigger volume + its TriggerMessages from another level. Do Add Object\nfirst (same session) and the trigger is pointed at that object's new instance.");
        ImGui.Separator();
        if (ImGui.MenuItem("Model (real, as Object)..."))
        {
            _importError = null;
            bool flipX = _flipModelX;
            ShowOpenFileDialog("Import 3D Model (real)", ModelImporter.FileFilter, modelPath =>
            {
                if (modelPath != null) BakeExternalModel(modelPath, flipX);
            });
        }
        if (ImGui.IsItemHovered())
            MaybeTooltip("Bakes the mesh into a real PS2 RigidModel/OGI/Object placed as a normal\nInstance. One real PS2 texture per submesh. Save Chunk to keep it.");
        if (ImGui.MenuItem("Scenery (real)..."))
        {
            _importError = null;
            bool flipX = _flipModelX;
            ShowOpenFileDialog("Import 3D Model as Scenery (real)", ModelImporter.FileFilter, modelPath =>
            {
                if (modelPath != null) BakeExternalModelAsScenery(modelPath, flipX);
            });
        }
        if (ImGui.IsItemHovered())
            MaybeTooltip("Bakes the mesh into real, independent PS2 SCENERY placed at the world origin.\nNo Object/Instance is created. Save Chunk to keep it.");
        ImGui.MenuItem("Flip X on import (fix mirror)", null, ref _flipModelX);
        ImGui.Separator();
        if (ImGui.MenuItem("AI Position")) AddAiPositionAt(System.Numerics.Vector3.Zero);
        if (ImGui.IsItemHovered())
            MaybeTooltip("Drops a new AI position at the world origin. Enemies roam among nearby\nAI positions. Connect them with \"AI Path\". Save Chunk to keep.");
        if (ImGui.MenuItem("AI Path (link all selected)")) AddAiPathBetweenSelected();
        if (ImGui.IsItemHovered())
            MaybeTooltip("Select ALL the AI positions to link (Ctrl+click): 2 -> one edge,\n3+ -> a closed loop. Save Chunk.");
    }

    // Amedo
    private void DrawCollisionMenu()
    {
        if (ImGui.MenuItem("Collision Box")) _pendingSpawnCollisionBox = true;
        if (ImGui.IsItemHovered())
            MaybeTooltip("A visible, resizable box that ALSO emits real PS2 collision geometry matching\nits position/scale. Synced into the real collision data on every Save Chunk.");
        if (ImGui.MenuItem("Cylinder Collision")) _pendingSpawnCollisionCylinder = true;
        if (ImGui.IsItemHovered())
            MaybeTooltip("Flat-capped cylinder — Radius/Height/Segments editable in the Inspector.\nSame sync-on-Save-Chunk behaviour as Collision Box.");
        if (ImGui.MenuItem("Plane Collision")) _pendingSpawnCollisionPlane = true;
        if (ImGui.IsItemHovered())
            MaybeTooltip("A single flat, single-sided quad (Width x Length in the Inspector) for patching\na ramp/wall/ceiling. Same sync-on-Save-Chunk behaviour as Collision Box.");
        ImGui.Separator();
        if (ImGui.MenuItem("Show Collision", null, ref _showCollision)) SetCollisionVisible(_showCollision);
        if (ImGui.MenuItem("Show Object Collision", null, ref _showObjectCollision) && _objectCollisionRoot != null)
            _objectCollisionRoot.Active = _showObjectCollision;
        if (ImGui.IsItemHovered())
            MaybeTooltip("The game's collision parts of every object (OGI convex hulls on their joints):\n" +
                         "green = Crash collides, yellow = Crash only touches it, grey = Crash ignores it.\n" +
                         "Blue = character box. In Play, physics balls show their sphere, and orange spheres\n" +
                         "= the movement sphere of walkers that use the physics controller.");
        ImGui.Separator();
        if (ImGui.MenuItem("Clear Collision Data (experimental)")) ClearCollisionData();
        if (ImGui.IsItemHovered())
            MaybeTooltip("Empties this level's ground/world collision triangles — for testing whether\nthe real game falls through the world without them. Save Chunk + Build ISO to test.");
    }

    // Amedo: a menu tick box that leaves the menu open, so several can be flipped in one visit
    // (ImGui's MenuItem always closes the menu; Selectable can be told not to). Draws the same
    // check mark MenuItem does, at the same place.
    private static bool MenuToggle(string label, ref bool value)
    {
        float mark = ImGui.GetFontSize();
        float gap = ImGui.GetStyle().ItemInnerSpacing.X;
        // blanks wide enough for the check mark keep the longest label from running into it (MenuItem reserves that
        // column by itself); ### keeps the id on the label alone, so the padding can change freely
        int blanks = (int)MathF.Ceiling((mark + gap * 2f) / MathF.Max(ImGui.CalcTextSize(" ").X, 1f));
        bool clicked = ImGui.Selectable($"{label}{new string(' ', blanks)}###{label}", false,
                                        ImGuiSelectableFlags.DontClosePopups);
        if (value)
        {
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            float size = mark;
            float thickness = MathF.Max(size / 5f, 1f);
            size -= thickness * 0.5f;
            var at = new Vector2(max.X - mark - gap, min.Y + (max.Y - min.Y - mark) * 0.5f)
                   + new Vector2(thickness * 0.25f, thickness * 0.25f);
            float third = size / 3f;
            float bx = at.X + third, by = at.Y + size - third * 0.5f;
            var draw = ImGui.GetWindowDrawList();
            draw.PathLineTo(new Vector2(bx - third, by - third));
            draw.PathLineTo(new Vector2(bx, by));
            draw.PathLineTo(new Vector2(bx + third * 2f, by - third * 2f));
            draw.PathStroke(ImGui.GetColorU32(ImGuiCol.Text), 0, thickness);
        }
        if (clicked) value = !value;
        return clicked;
    }

    // Amedo
    private void DrawViewMenu()
    {
        if (MenuToggle("Triggers", ref _showTriggers) && _triggersRoot != null) _triggersRoot.Active = _showTriggers;
        if (MenuToggle("Cameras", ref _showCameras) && _camerasRoot != null) _camerasRoot.Active = _showCameras;
        if (MenuToggle("Positions", ref _showPositions) && _positionsRoot != null) _positionsRoot.Active = _showPositions;
        if (MenuToggle("AI Positions", ref _showAiPositions) && _aiPosRoot != null) _aiPosRoot.Active = _showAiPositions;
        if (MenuToggle("Load Scenes", ref _showLoadScenes) && _loadScenesRoot != null) _loadScenesRoot.Active = _showLoadScenes;
        if (MenuToggle("Particle Emitters", ref _showParticleEmitters) && _particleEmittersRoot != null) _particleEmittersRoot.Active = _showParticleEmitters;
        if (MenuToggle("Lights", ref _showLights) && _lightsRoot != null) _lightsRoot.Active = _showLights;
        ImGui.Separator();
        if (MenuToggle("Particle Effects", ref _showParticleEffects) && _particleEffectsRoot != null) _particleEffectsRoot.Active = _showParticleEffects;
        if (MenuToggle("Game Distance Culling", ref _particleDistanceGating) && _particleEffects != null) _particleEffects.DistanceGating = _particleDistanceGating;
        if (ImGui.IsItemHovered())
            MaybeTooltip("Like the game: each effect only emits while the camera is between its\nmin and max emit distance, and vanishes outside it.\nOff = every effect runs no matter where the camera is.");
        if (RenderPipeline.Instance is { } pipeline)
        {
            ImGui.Separator();
            bool wire = pipeline.Wireframe;
            if (MenuToggle("Wireframe", ref wire)) pipeline.Wireframe = wire;
            bool lighting = GameLightingEnvironment.Enabled;
            if (MenuToggle("Game Lighting", ref lighting)) GameLightingEnvironment.Enabled = lighting;
            if (ImGui.IsItemHovered())
                MaybeTooltip("Lights objects exactly like the game: ambient + the 3 strongest lights of the\nlevel, per vertex, on lit materials only. Scenery stays unlit, like the game.");
            bool shadows = GameShadowPass.ShowShadows;
            if (MenuToggle("Game Shadows", ref shadows)) GameShadowPass.ShowShadows = shadows;
            if (ImGui.IsItemHovered())
                MaybeTooltip("Shadows exactly like the game: each object's own shadow shapes are extruded\nstraight down as volumes and darken what they touch.");
            DrawSunShadowsMenu(); // Amedo
        }
    }

    // Amedo
    private static void DrawSunShadowsMenu()
    {
        var s = GraphicsSettings.Current;
        bool sun = s.Shadows == ShadowMode.Sun;
        if (MenuToggle("Sun Shadows (PC)", ref sun)) GraphicsSettings.Set(s with { Shadows = sun ? ShadowMode.Sun : ShadowMode.Original });
        if (ImGui.IsItemHovered())
            MaybeTooltip("PC only: a real sun lights the level and everything casts a shadow on everything\n(scenery, objects, Crash). Replaces Game Shadows while on. The PS2 build keeps the\noriginal shadows. Saved in this project's graphics settings (also GRAPHIC OPTIONS > SHADOWS).");
        if (!sun) return;
        if (ImGui.BeginMenu("Sun Settings"))
        {
            int strength = s.SunStrength, azimuth = s.SunAzimuth, elevation = s.SunElevation, range = s.SunRange, softness = s.SunSoftness;
            bool changed = ImGui.SliderInt("Strength", ref strength, GraphicsSettings.SunStrengthMin, GraphicsSettings.SunStrengthMax);
            bool done = ImGui.IsItemDeactivatedAfterEdit();
            bool fromLevel = s.SunFromLevel;
            bool levelChanged = ImGui.Checkbox("Follow Level Sun", ref fromLevel);
            if (ImGui.IsItemHovered()) MaybeTooltip("The shadows follow the sun (or moon) drawn in this level's sky, found automatically\n(if the sky has none: the level's first directional light). Turn off to set Direction / Height by hand.");
            changed |= levelChanged;
            done |= levelChanged;
            ImGui.BeginDisabled(fromLevel);
            changed |= ImGui.SliderInt("Direction", ref azimuth, 0, 359, "%d deg");
            done |= ImGui.IsItemDeactivatedAfterEdit();
            changed |= ImGui.SliderInt("Height", ref elevation, 5, 90, "%d deg");
            done |= ImGui.IsItemDeactivatedAfterEdit();
            ImGui.EndDisabled();
            changed |= ImGui.SliderInt("Shadow Range", ref range, GraphicsSettings.SunRangeMin, GraphicsSettings.SunRangeMax);
            if (ImGui.IsItemHovered()) MaybeTooltip("How far from the camera shadows are drawn. Sharp up to 150 m, coarser beyond that\n(keeps performance).");
            done |= ImGui.IsItemDeactivatedAfterEdit();
            changed |= ImGui.SliderInt("Softness", ref softness, GraphicsSettings.SunSoftnessMin, GraphicsSettings.SunSoftnessMax);
            if (ImGui.IsItemHovered()) MaybeTooltip("How soft the shadow edges are (0 = hard).");
            done |= ImGui.IsItemDeactivatedAfterEdit();
            int shapeSoft = s.ShapeSoftness;
            changed |= ImGui.SliderInt("Shape Softness", ref shapeSoft, GraphicsSettings.ShapeSoftnessMin, GraphicsSettings.ShapeSoftnessMax);
            if (ImGui.IsItemHovered()) MaybeTooltip("How much characters and objects are shaded by their overall shape instead of their\ntriangles (0 = surface only, 10 = fully rounded).");
            done |= ImGui.IsItemDeactivatedAfterEdit();
            ImGui.Separator();
            bool points = s.PointLights;
            int pointStrength = s.PointLightStrength;
            bool pointsChanged = ImGui.Checkbox("Point Lights", ref points);
            if (ImGui.IsItemHovered()) MaybeTooltip("The level's own lamps/torches light the scenery and every object (Crash too) per pixel,\nand cast soft shadows (the nearest 4).");
            changed |= pointsChanged;
            done |= pointsChanged;
            ImGui.BeginDisabled(!points);
            changed |= ImGui.SliderInt("Point Light Strength", ref pointStrength, GraphicsSettings.PointLightStrengthMin, GraphicsSettings.PointLightStrengthMax);
            done |= ImGui.IsItemDeactivatedAfterEdit();
            ImGui.EndDisabled();
            if (changed)
                GraphicsSettings.Set(s with { SunStrength = strength, SunAzimuth = azimuth, SunElevation = elevation, SunRange = range, SunSoftness = softness, SunFromLevel = fromLevel,
                                              PointLights = points, PointLightStrength = pointStrength, ShapeSoftness = shapeSoft },
                    save: false);
            if (done) GraphicsSettings.Current.Save();
            ImGui.EndMenu();
        }
    }

    // Amedo
    private void DrawToolsMenu()
    {
        bool playerRunning = _playerProcess is { HasExited: false };
        if (ImGui.MenuItem("Launch Player", null, false, !playerRunning)) LaunchPlayer();
        if (ImGui.IsItemHovered())
            MaybeTooltip("Runs the game outside the editor, exactly like the built game (same CrashGame player,\nsame project data): saves the open level first and starts in it. It uses its own\ntest copy in <project>\\LaunchPlayer (own saves) and never touches Build\\PC.\nEsc quits."); // Amedo
        if (ImGui.MenuItem("Stop Player", null, false, playerRunning)) StopPlayer();
        ImGui.Separator();
        if (ImGui.MenuItem("Show Scripts (names)...")) _showScriptsBrowser = true;
        if (ImGui.IsItemHovered())
            MaybeTooltip("Lists every item in this chunk's CODE_BEHAVIOURS_SECTION with its real name\n(graphs, odd id); starters (even id) have no name.");
        if (ImGui.MenuItem("Copy All Ground Colors From Level..."))
        {
            _groundColorPickerFilter = "";
            _showGroundColorPicker = true;
        }
        if (ImGui.IsItemHovered())
            MaybeTooltip("Copies vertex colors for EVERY scenery mesh in this chunk that shares the\nsame mesh id with the chosen level. Tiles with no matching id are untouched.");
        ImGui.Separator();
        if (ImGui.MenuItem("Delete Linked Scenery (test)")) DeleteLinkedScenery();
        if (ImGui.IsItemHovered())
            MaybeTooltip("Clears this chunk's whole LinksList in-memory only. Reload Level from Disc\nto undo (not on the undo stack). Save Chunk to persist.");
        if (ImGui.MenuItem("Delete Skydome (test)")) DeleteSkydome();
        if (ImGui.IsItemHovered())
            MaybeTooltip("Clears this chunk's skydome in-memory only. Reload Level from Disc to undo\n(not on the undo stack). Save Chunk to persist.");
    }

    // Amedo
    private void DrawWindowMenu()
    {
        ImGui.MenuItem("Game Settings", null, ref _showGameSettings);
        if (ImGui.IsItemHovered())
            MaybeTooltip("Gameplay-rule toggles — real script edits to shared behaviour\ngraphs (Startup\\Default.rm2), not per-object Inspector tweaks.");
        ImGui.MenuItem("UI Browser", null, ref _showUiBrowser);
        if (ImGui.IsItemHovered())
            MaybeTooltip("Browse the game's own loading screens, icons, decals, and fonts —\nreal assets decoded straight from disc.");
        ImGui.MenuItem("Default.rm2 Data", null, ref _showGlobalData);
        if (ImGui.IsItemHovered())
            MaybeTooltip("Browse Startup\\Default.rm2's own shared objects/behaviours/sounds by list.");
        ImGui.Separator();
        if (ImGui.MenuItem("Reset Layout")) ResetWorkspaceLayout();
        if (ImGui.IsItemHovered())
            MaybeTooltip("Restore this workspace's default panel layout.");
    }

    // Amedo
    private void DrawHelpMenu()
    {
        ImGui.MenuItem("Shortcuts", null, ref _showShortcuts);
        ImGui.MenuItem("About", null, ref _showAbout);
    }
}
