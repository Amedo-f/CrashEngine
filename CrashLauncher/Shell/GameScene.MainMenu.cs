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
        _windowChrome.DrawMinimizedTabs();
    }

    // Amedo
    private void DrawFileMenu()
    {
        ImGui.BeginDisabled(_building);
        if (ImGui.MenuItem("Save Chunk (.rm2/.sm2)")) SaveChunk();
        ImGui.Separator();
        if (ImGui.MenuItem("Reload Level (keep saved edits)")) ReloadLevel(fromDiscOnly: false);
        if (ImGui.MenuItem("Reload Level from Disc (discard saved edits)")) ReloadLevel(fromDiscOnly: true);
        ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1f, 0.45f, 0.45f, 1f));
        if (ImGui.MenuItem("Reset ALL Levels From Disc (delete all saved edits)...")) _openResetAllConfirmPopup = true;
        ImGui.PopStyleColor();
        ImGui.Separator();
        if (ImGui.MenuItem("Build ISO (PS2)")) BuildIso();
        if (ImGui.BeginMenu("ISO Output Path"))
        {
            EnsureIsoOutputPathLoaded();
            ImGui.TextDisabled("ISO output file (kept for this project until you change it):");
            ImGui.SetNextItemWidth(360f);
            if (ImGui.InputTextWithHint("##isoOutPath", "not set — Build ISO asks for it", ref _isoOutputPath, 512u)) SaveIsoOutputPath();
            ImGui.SameLine();
            if (ImGui.Button("...##isoOutBrowse")) ChooseIsoOutputPath(null);
            ImGui.EndMenu();
        }
        if (ImGui.MenuItem(_buildingPc ? "Building PC..." : "Build to PC", null, false, !_buildingPc)) BuildPc();
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
        if (ImGui.MenuItem("Export Collision to OBJ...")) ExportCollisionToObj();
        if (ImGui.MenuItem("Import Collision from OBJ...")) ImportCollisionFromObj();
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
    }

    // Amedo
    private void DrawGameObjectMenu()
    {
        if (ImGui.MenuItem("Cube")) _pendingSpawn = true;
        ImGui.Separator();
        if (ImGui.MenuItem("Crash (full, with scripts)...")) _showCrashPicker = true;
        if (ImGui.MenuItem("Object (full, from another level)..."))
        {
            _objectTransplantLevelFilter = "";
            _showObjectTransplantLevelPicker = true;
        }
        if (ImGui.MenuItem("Trigger (from another level)..."))
        {
            _triggerTransplantLevelFilter = "";
            _showTriggerTransplantLevelPicker = true;
        }
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
        if (ImGui.MenuItem("Scenery (real)..."))
        {
            _importError = null;
            bool flipX = _flipModelX;
            ShowOpenFileDialog("Import 3D Model as Scenery (real)", ModelImporter.FileFilter, modelPath =>
            {
                if (modelPath != null) BakeExternalModelAsScenery(modelPath, flipX);
            });
        }
        ImGui.MenuItem("Flip X on import (fix mirror)", null, ref _flipModelX);
        ImGui.Separator();
        if (ImGui.MenuItem("AI Position")) AddAiPositionAt(System.Numerics.Vector3.Zero);
        if (ImGui.MenuItem("AI Path (link all selected)")) AddAiPathBetweenSelected();
    }

    // Amedo
    private void DrawCollisionMenu()
    {
        if (ImGui.MenuItem("Collision Box")) _pendingSpawnCollisionBox = true;
        if (ImGui.MenuItem("Cylinder Collision")) _pendingSpawnCollisionCylinder = true;
        if (ImGui.MenuItem("Plane Collision")) _pendingSpawnCollisionPlane = true;
        ImGui.Separator();
        if (ImGui.MenuItem("Show Collision", null, ref _showCollision)) SetCollisionVisible(_showCollision);
        if (ImGui.MenuItem("Show Object Collision", null, ref _showObjectCollision) && _objectCollisionRoot != null)
            _objectCollisionRoot.Active = _showObjectCollision;
        ImGui.Separator();
        if (ImGui.MenuItem("Clear Collision Data (experimental)")) ClearCollisionData();
    }

    // Amedo
    private static bool MenuToggle(string label, ref bool value)
    {
        float mark = ImGui.GetFontSize();
        float gap = ImGui.GetStyle().ItemInnerSpacing.X;
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
        if (RenderPipeline.Instance is { } pipeline)
        {
            ImGui.Separator();
            bool wire = pipeline.Wireframe;
            if (MenuToggle("Wireframe", ref wire)) pipeline.Wireframe = wire;
            bool lighting = GameLightingEnvironment.Enabled;
            if (MenuToggle("Game Lighting", ref lighting)) GameLightingEnvironment.Enabled = lighting;
            bool shadows = GameShadowPass.ShowShadows;
            if (MenuToggle("Game Shadows", ref shadows)) GameShadowPass.ShowShadows = shadows;
            DrawSunShadowsMenu(); // Amedo
        }
    }

    // Amedo
    private static void DrawSunShadowsMenu()
    {
        var s = GraphicsSettings.Current;
        bool sun = s.Shadows == ShadowMode.Sun;
        if (MenuToggle("Sun Shadows (PC)", ref sun)) GraphicsSettings.Set(s with { Shadows = sun ? ShadowMode.Sun : ShadowMode.Original });
        if (!sun) return;
        if (ImGui.BeginMenu("Sun Settings"))
        {
            int strength = s.SunStrength, azimuth = s.SunAzimuth, elevation = s.SunElevation, range = s.SunRange, softness = s.SunSoftness;
            bool changed = ImGui.SliderInt("Strength", ref strength, GraphicsSettings.SunStrengthMin, GraphicsSettings.SunStrengthMax);
            bool done = ImGui.IsItemDeactivatedAfterEdit();
            bool fromLevel = s.SunFromLevel;
            bool levelChanged = ImGui.Checkbox("Follow Level Sun", ref fromLevel);
            changed |= levelChanged;
            done |= levelChanged;
            ImGui.BeginDisabled(fromLevel);
            changed |= ImGui.SliderInt("Direction", ref azimuth, 0, 359, "%d deg");
            done |= ImGui.IsItemDeactivatedAfterEdit();
            changed |= ImGui.SliderInt("Height", ref elevation, 5, 90, "%d deg");
            done |= ImGui.IsItemDeactivatedAfterEdit();
            ImGui.EndDisabled();
            changed |= ImGui.SliderInt("Shadow Range", ref range, GraphicsSettings.SunRangeMin, GraphicsSettings.SunRangeMax);
            done |= ImGui.IsItemDeactivatedAfterEdit();
            changed |= ImGui.SliderInt("Softness", ref softness, GraphicsSettings.SunSoftnessMin, GraphicsSettings.SunSoftnessMax);
            done |= ImGui.IsItemDeactivatedAfterEdit();
            int shapeSoft = s.ShapeSoftness;
            changed |= ImGui.SliderInt("Shape Softness", ref shapeSoft, GraphicsSettings.ShapeSoftnessMin, GraphicsSettings.ShapeSoftnessMax);
            done |= ImGui.IsItemDeactivatedAfterEdit();
            ImGui.Separator();
            bool points = s.PointLights;
            int pointStrength = s.PointLightStrength;
            bool pointsChanged = ImGui.Checkbox("Point Lights", ref points);
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
        if (ImGui.MenuItem("Stop Player", null, false, playerRunning)) StopPlayer();
        ImGui.Separator();
        if (ImGui.MenuItem("Show Scripts (names)...")) _showScriptsBrowser = true;
        if (ImGui.MenuItem("Copy All Ground Colors From Level..."))
        {
            _groundColorPickerFilter = "";
            _showGroundColorPicker = true;
        }
        ImGui.Separator();
        if (ImGui.MenuItem("Delete Linked Scenery (test)")) DeleteLinkedScenery();
        if (ImGui.MenuItem("Delete Skydome (test)")) DeleteSkydome();
    }

    // Amedo
    private void DrawWindowMenu()
    {
        if (_workspace == Workspace.Scene)
        {
            ImGui.MenuItem("Hierarchy", null, ref _showHierarchy);
            ImGui.MenuItem("Inspector", null, ref _showInspector);
            ImGui.MenuItem("Project", null, ref _showProject);
            ImGui.MenuItem("Console", null, ref _showConsole);
            ImGui.MenuItem("Behaviour", null, ref _showBehaviour);
        }
        else
        {
            ImGui.MenuItem("Characters", null, ref _showAnimCharacters);
            ImGui.MenuItem("Bones", null, ref _showAnimBones);
            ImGui.MenuItem("Inspector", null, ref _showInspector);
            ImGui.MenuItem("Timeline", null, ref _showAnimTimeline);
        }
        ImGui.Separator();
        ImGui.MenuItem("Build", null, ref _showBuildWindow);
        ImGui.MenuItem("Game Settings", null, ref _showGameSettings);
        ImGui.MenuItem("UI Browser", null, ref _showUiBrowser);
        ImGui.MenuItem("Default.rm2 Data", null, ref _showGlobalData);
        ImGui.Separator();
        if (ImGui.MenuItem("Reset Layout")) ResetWorkspaceLayout();
    }

    // Amedo
    private void DrawHelpMenu()
    {
        ImGui.MenuItem("Shortcuts", null, ref _showShortcuts);
        ImGui.MenuItem("About", null, ref _showAbout);
    }
}
