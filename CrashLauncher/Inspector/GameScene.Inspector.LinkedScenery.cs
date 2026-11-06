using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using TwinMat4 = Twinsanity.TwinsanityInterchange.Common.Matrix4;
using TwinChunkLink = Twinsanity.TwinsanityInterchange.Common.TwinChunkLink;
using PS2AnyLink = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyLink;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private void DrawInspectorLinkGroup(Entity e)
    {
        if (e.Name == "LinkedScenery")
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Linked Scenery (all links)##linkgroup", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextDisabled("Add a real NEW link (trigger only, no scenery preview -- see\ntooltip) to a level this chunk doesn't already link to:");
                ImGui.SetNextItemWidth(-1f);
                ImGui.InputTextWithHint("##addLinkFilter", "Search levels...", ref _addLinkFilter, 128);
                ImGui.BeginChild("##addLinkList", new Vector2(-1f, 140f), ImGuiChildFlags.Border);
                var existingTargets = new HashSet<string>(
                    e.Children.Select(c => c.Get<CrashEngine.Importer.LinkedSceneryLink>()?.Source.Path.Replace('/', '\\').TrimStart('\\'))
                              .Where(p => p is not null)!,
                    StringComparer.OrdinalIgnoreCase);
                foreach (var lvl in GetLinkRedirectTargets())
                {
                    if (!string.IsNullOrWhiteSpace(_addLinkFilter) &&
                        !lvl.Contains(_addLinkFilter, StringComparison.OrdinalIgnoreCase))
                        continue;
                    var basePath = lvl.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase) ? lvl[..^4] : lvl;
                    if (existingTargets.Contains(basePath)) continue;
                    if (ImGui.Selectable(lvl))
                        AddNewLink(e, basePath);
                }
                ImGui.EndChild();
            }
        }
    }

    private void DrawInspectorLinkedScenery(Entity e, CrashEngine.Importer.LinkedSceneryLink? linkedScenery)
    {
        if (linkedScenery is not null)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Linked Scenery##link", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextDisabled($"Target: {linkedScenery.Source.Path}");
                if (ImGui.Button("Go to this scene##gotolinked", new Vector2(-1f, 0f)))
                {
                    var tgt = (linkedScenery.Source.Path ?? "").Replace('/', '\\').TrimStart('\\');
                    if (tgt.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase) ||
                        tgt.EndsWith(".sm2", StringComparison.OrdinalIgnoreCase)) tgt = tgt[..^4];
                    if (!string.IsNullOrWhiteSpace(tgt)) _pendingOpenLevelPath = tgt + ".rm2";
                }
                if (!IsLinkTargetKeptInNewGame(linkedScenery.Source.Path))
                    ImGui.TextColored(new Vector4(1f, 0.5f, 0.2f, 1f),
                        "⚠ Not claimed for New Game -- this link will be DISABLED\n(neutered) in the built ISO, even though it's still live here.");
                ImGui.TextDisabled($"Load Wall Active: {linkedScenery.Source.IsLoadWallActive}");
                if (!linkedScenery.Source.IsLoadWallActive)
                    ImGui.TextDisabled("(preview-only — the load wall isn't collidable, so\n" +
                                        "walking here won't actually transition, per this data)");

                int renderMode = (linkedScenery.Source.IsRendered ? 1 : 0) | (linkedScenery.Source.UnkNum << 1); // Amedo 2026-10-06
                int renderPick = renderMode == 0 ? 0 : renderMode == 1 ? 1 : 2;
                string[] renderNames = { "Never", "Always", "When its load wall is on screen" };
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.Combo("##linkRenderMode", ref renderPick, renderNames, renderNames.Length) && renderPick != (renderMode == 0 ? 0 : renderMode == 1 ? 1 : 2))
                {
                    linkedScenery.Source.IsRendered = renderPick == 1;
                    linkedScenery.Source.UnkNum = (byte)(renderPick == 2 ? 1 : 0);
                    if (renderPick == 1)
                    {
                        if (e.Children.Count == 0)
                            RetargetLinkedScenery(e, linkedScenery.Source.Path);
                    }
                    else
                    {
                        foreach (var child in e.Children.ToList())
                            e.RemoveChild(child);
                    }
                }
                ImGui.TextDisabled(renderPick switch
                {
                    0 => "(in game: never drawn — loaded only for its logic, if at all)",
                    1 => "(in game: always drawn while its scenery is loaded)",
                    _ => "(in game: drawn only while its load wall is inside the camera view;\n no preview here)",
                });

                ImGui.TextDisabled("Redirect to a different level's chunk:");
                if (GetGameProject() is { IsNewGame: true })
                    ImGui.TextColored(new Vector4(1f, 0.7f, 0.3f, 1f),
                        "New Game mode: list limited to levels that'll actually exist in the build.");
                ImGui.SetNextItemWidth(-1f);
                ImGui.InputTextWithHint("##linkFilter", "Search levels...", ref _linkTargetFilter, 128);
                ImGui.BeginChild("##linkList", new Vector2(-1f, 160f), ImGuiChildFlags.Border);
                foreach (var lvl in GetLinkRedirectTargets())
                {
                    if (!string.IsNullOrWhiteSpace(_linkTargetFilter) &&
                        !lvl.Contains(_linkTargetFilter, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (ImGui.Selectable(lvl))
                        RetargetLinkedScenery(e, lvl);
                }
                ImGui.EndChild();
            }
        }
    }

    private void RetargetLinkedScenery(Entity linkEnt, string newRm2Path)
    {
        var basePath = newRm2Path.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase)
            ? newRm2Path[..^4] : newRm2Path;
        try
        {
            using var pkg = PackageReader.Open(_extractedRoot);
            pkg.ShadowDir = SavedChunksDir;
            if (!ChunkImporter.RetargetLinkedScenery(Engine.Instance.GL, pkg, linkEnt, basePath, out string log))
            {
                _browser.Log($"Linked Scenery: {log}");
                return;
            }
            _browser.Log($"Linked Scenery: {log} Not undoable. Save Chunk + Build ISO, then test in an " +
                          "emulator — the game's own load-wall logic is what actually reads this data at " +
                          "runtime, not verified here against a real PCSX2 run.");
        }
        catch (Exception ex) { _browser.Log($"Linked Scenery: {ex.Message}"); }
    }

    private static Vector3 XformPoint(TwinMat4 m, Vector3 p) => new Vector3(
        m.Column1.X * p.X + m.Column2.X * p.Y + m.Column3.X * p.Z + m.Column4.X,
        m.Column1.Y * p.X + m.Column2.Y * p.Y + m.Column3.Y * p.Z + m.Column4.Y,
        m.Column1.Z * p.X + m.Column2.Z * p.Y + m.Column3.Z * p.Z + m.Column4.Z);

    private void AddNewLink(Entity linksRoot, string targetBasePath)
    {
        var chunkSource = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();
        var linkItem = chunkSource?.Sm2?.GetItem<PS2AnyLink>((uint)TwinConstants.SCENERY_LINK_ITEM);
        if (linkItem is null || _loadScenesRoot is null)
        { _browser.Log("Add Link: chunk has no scenery link section."); return; }

        var thisSceneRel = (_rm2.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase) ? _rm2[..^4] : _rm2)
            .Replace('/', '\\').TrimStart('\\');
        TwinChunkLink? counterpart = null;
        try
        {
            using var pkg = PackageReader.Open(_extractedRoot);
            pkg.ShadowDir = SavedChunksDir;
            using var stream = pkg.OpenByPath($"{targetBasePath}.sm2");
            if (stream is not null)
            {
                var tgtSm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanitySM2();
                using (var reader = new BinaryReader(stream)) tgtSm2.Read(reader, (int)stream.Length);
                var tgtLinks = tgtSm2.GetItem<PS2AnyLink>((uint)TwinConstants.SCENERY_LINK_ITEM);
                counterpart = tgtLinks?.LinksList.FirstOrDefault(l =>
                    string.Equals((l.Path ?? "").Replace('/', '\\').TrimStart('\\'), thisSceneRel, StringComparison.OrdinalIgnoreCase));
            }
        }
        catch {  }
        bool aligned = counterpart is { ObjectMatrix: not null, ChunkMatrix: not null, LoadingWall: not null };

        TwinChunkLink newLink;
        if (aligned)
        {
            var cw = counterpart!.LoadingWall!;
            var a1 = XformPoint(counterpart.ObjectMatrix, new Vector3(cw.Column1.X, cw.Column1.Y, cw.Column1.Z));
            var a2 = XformPoint(counterpart.ObjectMatrix, new Vector3(cw.Column2.X, cw.Column2.Y, cw.Column2.Z));
            var a3 = XformPoint(counterpart.ObjectMatrix, new Vector3(cw.Column3.X, cw.Column3.Y, cw.Column3.Z));
            var a4 = XformPoint(counterpart.ObjectMatrix, new Vector3(cw.Column4.X, cw.Column4.Y, cw.Column4.Z));
            newLink = new TwinChunkLink
            {
                Path             = targetBasePath,
                IsRendered       = true,
                IsLoadWallActive = true,
                KeepLoaded       = false,
                ObjectMatrix     = MeshDecoder.CloneTwinMat4(counterpart.ChunkMatrix),
                ChunkMatrix      = MeshDecoder.CloneTwinMat4(counterpart.ObjectMatrix),
                LoadingWall      = new TwinMat4
                {
                    Column1 = new TwinVec4(a1.X, a1.Y, a1.Z, 1f),
                    Column2 = new TwinVec4(a4.X, a4.Y, a4.Z, 1f),
                    Column3 = new TwinVec4(a3.X, a3.Y, a3.Z, 1f),
                    Column4 = new TwinVec4(a2.X, a2.Y, a2.Z, 1f),
                },
            };
        }
        else
        {
            var camPos = _camera?.Transform.Position ?? Vector3.Zero;
            const float half = 3f;
            newLink = new TwinChunkLink
            {
                Path             = targetBasePath,
                IsRendered       = false,
                IsLoadWallActive = true,
                KeepLoaded       = false,
                ObjectMatrix     = MeshDecoder.IdentityTwinMat4(),
                ChunkMatrix      = MeshDecoder.IdentityTwinMat4(),
                LoadingWall      = new TwinMat4
                {
                    Column1 = new TwinVec4(camPos.X - half, camPos.Y - half, camPos.Z, 1f),
                    Column2 = new TwinVec4(camPos.X + half, camPos.Y - half, camPos.Z, 1f),
                    Column3 = new TwinVec4(camPos.X + half, camPos.Y + half, camPos.Z, 1f),
                    Column4 = new TwinVec4(camPos.X - half, camPos.Y + half, camPos.Z, 1f),
                },
            };
        }
        linkItem.LinksList.Add(newLink);

        var newLinkEnt = new Entity($"Link_{System.IO.Path.GetFileName(targetBasePath)}");
        newLinkEnt.Transform.LocalMatrix = CrashEngine.Importer.MeshDecoder.TwinMatToSys(newLink.ChunkMatrix);
        newLinkEnt.Add(new CrashEngine.Importer.LinkedSceneryLink { Source = newLink });
        linksRoot.AddChild(newLinkEnt);
        if (aligned)
            RetargetLinkedScenery(newLinkEnt, targetBasePath);

        var newWallEnt = MakeQuadMarker(_loadScenesRoot, $"LoadScene_{System.IO.Path.GetFileName(targetBasePath)}_new",
            newLink, new Vector4(1f, 0.4f, 0.1f, 0.6f));

        // Amedo 2026-09-20
        PushAddWithSelectionRestore(
            new LoadWallAddAction { Parent = _loadScenesRoot, Entity = newWallEnt, LinkItem = linkItem, Link = newLink },
            newWallEnt);

        _browser.Log(aligned
            ? $"Auto-aligned FULL return link -> '{targetBasePath}': it links back to '{thisSceneRel}', so matched its matrices (Object<->Chunk swap), built the aligned preview, AND auto-placed the LoadScene wall = the counterpart's real wall transformed here + normal flipped (matches the real reciprocal). Save Chunk + Build to test; if the crossing ever freezes, use 'Flip Load Wall Normal' on the LoadScene marker."
            : $"Added new link -> '{targetBasePath}' (target doesn't link back here -- plain trigger at camera, no preview). Save Chunk to persist.");
    }

    private void DeleteLinkedScenery()
    {
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        var linkItem    = chunkSource?.Sm2?.GetItem<PS2AnyLink>((uint)TwinConstants.SCENERY_LINK_ITEM);
        if (chunkRoot is null || linkItem is null)
        { _browser.Log("Delete Linked Scenery: chunk has no scenery link section."); return; }

        int count = linkItem.LinksList.Count;
        if (count == 0) { _browser.Log("Delete Linked Scenery: this chunk has no links to begin with."); return; }
        linkItem.LinksList.Clear();

        var linksRoot = AllEntities(chunkRoot).FirstOrDefault(x => x.Name == "LinkedScenery");
        if (linksRoot is not null)
            foreach (var child in linksRoot.Children.ToList())
                linksRoot.RemoveChild(child);
        if (_loadScenesRoot is not null)
            foreach (var child in _loadScenesRoot.Children.ToList())
                _loadScenesRoot.RemoveChild(child);

        _browser.Log($"Deleted all {count} linked-scenery entr{(count == 1 ? "y" : "ies")} (in-memory only). " +
                      "Reload Level from Disc to undo. Save Chunk + Build ISO to persist.");
    }

    private bool IsLinkTargetKeptInNewGame(string targetPath)
    {
        var project = GetGameProject();
        if (project is not { IsNewGame: true }) return true;
        var normalized = targetPath.Replace('/', '\\').TrimStart('\\');
        if (normalized.StartsWith(@"Levels\Custom\", StringComparison.OrdinalIgnoreCase)) return true;
        if (normalized.Equals(@"Levels\Earth\Hub\beach", StringComparison.OrdinalIgnoreCase)) return true;
        return project.ClaimedLevels.Any(p => p.Replace('/', '\\').TrimStart('\\').Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    // Amedo 2026-09-22
    private void StripDanglingLinksForNewGamePreview(Entity chunkRoot)
    {
        if (GetGameProject() is not { IsNewGame: true }) return;
        var sm2 = chunkRoot.Get<ChunkSource>()?.Sm2;
        var linkItem = sm2?.GetItem<PS2AnyLink>((uint)TwinConstants.SCENERY_LINK_ITEM);
        if (linkItem is null || linkItem.LinksList.Count == 0) return;

        int removed = linkItem.LinksList.RemoveAll(l => !IsLinkTargetKeptInNewGame(l.Path));
        if (removed == 0) return;

        var linksRoot = chunkRoot.Children.FirstOrDefault(c => c.Name == "LinkedScenery");
        if (linksRoot is not null)
        {
            var stale = linksRoot.Children
                .Where(c => c.Get<CrashEngine.Importer.LinkedSceneryLink>() is { } ls
                            && !IsLinkTargetKeptInNewGame(ls.Source.Path))
                .ToList();
            foreach (var c in stale) linksRoot.RemoveChild(c);
        }
        _browser.Log($"New Game: removed {removed} upcoming-scene link(s) to excluded levels from this scene.");
    }

    private List<string> GetLinkRedirectTargets()
    {
        var full = GetSwapLevelList();
        var project = GetGameProject();
        if (project is not { IsNewGame: true }) return full;

        var keep = new HashSet<string>(project.ClaimedLevels.Select(p => p.Replace('/', '\\').TrimStart('\\')),
                                        StringComparer.OrdinalIgnoreCase)
        { @"Levels\Earth\Hub\beach" };
        return full.Where(rel =>
            rel.StartsWith(@"Levels\Custom\", StringComparison.OrdinalIgnoreCase) ||
            keep.Contains(rel.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase) ? rel[..^4] : rel)
        ).ToList();
    }
}
