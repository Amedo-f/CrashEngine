using System.Text.Json;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;

namespace CrashEngine.Importer;

public sealed record CustomSceneryAssetEntry(string Name, string ContainerFile, List<int> TileIndices);

public static class CustomSceneryAssetLibrary
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static List<string> ListFolders(string rootDir)
    {
        if (!Directory.Exists(rootDir)) return new List<string>();
        return Directory.GetDirectories(rootDir)
            .Select(Path.GetFileName)
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<(string FileName, CustomSceneryAssetEntry Entry)> ListEntries(string rootDir, string folder)
    {
        var dir = Path.Combine(rootDir, folder);
        var result = new List<(string, CustomSceneryAssetEntry)>();
        if (!Directory.Exists(dir)) return result;
        foreach (var f in Directory.GetFiles(dir, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var entry = JsonSerializer.Deserialize<CustomSceneryAssetEntry>(File.ReadAllText(f));
                if (entry is not null) result.Add((Path.GetFileName(f), entry));
            }
            catch {  }
        }
        return result;
    }

    public static void Save(string rootDir, string folder, string name, PS2AnyTwinsanitySM2 containerSm2, List<int> tileIndices)
    {
        var dir = Path.Combine(rootDir, folder);
        Directory.CreateDirectory(dir);
        var safeName = SanitizeFileName(name);
        var baseName = safeName;
        int n = 1;
        while (File.Exists(Path.Combine(dir, baseName + ".json")) || File.Exists(Path.Combine(dir, baseName + ".sm2")))
            baseName = $"{safeName} ({++n})";

        var sm2Path = Path.Combine(dir, baseName + ".sm2");
        WriteItemAtomic(containerSm2, sm2Path);

        var entry = new CustomSceneryAssetEntry(name, baseName + ".sm2", tileIndices);
        var jsonPath = Path.Combine(dir, baseName + ".json");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(entry, JsonOptions));
    }

    private static void WriteItemAtomic(Twinsanity.TwinsanityInterchange.Interfaces.ITwinSerializable item, string path)
    {
        var tempPath = path + ".tmp";
        using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
        using (var bw = new BinaryWriter(fs))
            item.Write(bw);
        File.Move(tempPath, path, overwrite: true);
    }

    public static PS2AnyTwinsanitySM2? LoadContainer(string rootDir, string folder, string containerFile)
    {
        var path = Path.Combine(rootDir, folder, containerFile);
        if (!File.Exists(path)) return null;
        try
        {
            var sm2 = new PS2AnyTwinsanitySM2();
            using var fs = File.OpenRead(path);
            using var reader = new BinaryReader(fs);
            sm2.Read(reader, (int)fs.Length);
            return sm2;
        }
        catch { return null; }
    }

    public static void MoveEntry(string rootDir, string fromFolder, string fileName, string toFolder)
    {
        if (string.Equals(fromFolder, toFolder, StringComparison.OrdinalIgnoreCase)) return;
        var srcDir = Path.Combine(rootDir, fromFolder);
        var srcJson = Path.Combine(srcDir, fileName);
        if (!File.Exists(srcJson)) return;
        var destDir = Path.Combine(rootDir, toFolder);
        Directory.CreateDirectory(destDir);
        var destJson = Path.Combine(destDir, fileName);
        if (File.Exists(destJson)) return;

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var srcSm2 = Path.Combine(srcDir, baseName + ".sm2");
        var destSm2 = Path.Combine(destDir, baseName + ".sm2");
        File.Move(srcJson, destJson);
        if (File.Exists(srcSm2) && !File.Exists(destSm2)) File.Move(srcSm2, destSm2);
    }

    public static void DeleteEntry(string rootDir, string folder, string fileName)
    {
        var dir = Path.Combine(rootDir, folder);
        var jsonPath = Path.Combine(dir, fileName);
        if (File.Exists(jsonPath)) File.Delete(jsonPath);
        var sm2Path = Path.Combine(dir, Path.GetFileNameWithoutExtension(fileName) + ".sm2");
        if (File.Exists(sm2Path)) File.Delete(sm2Path);
    }

    public static void CreateFolder(string rootDir, string folder)
    {
        Directory.CreateDirectory(Path.Combine(rootDir, SanitizeFileName(folder)));
    }


    public static List<string> ListAllFolders(string rootDir)
    {
        if (!Directory.Exists(rootDir)) return new List<string>();
        return Directory.GetDirectories(rootDir, "*", SearchOption.AllDirectories)
            .Select(d => Path.GetRelativePath(rootDir, d))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<string> ListSubFolders(string rootDir, string folder)
    {
        var dir = string.IsNullOrEmpty(folder) ? rootDir : Path.Combine(rootDir, folder);
        if (!Directory.Exists(dir)) return new List<string>();
        return Directory.GetDirectories(dir)
            .Select(Path.GetFileName)
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string CreateSubFolder(string rootDir, string parentFolder, string leafName)
    {
        var parentDir = string.IsNullOrEmpty(parentFolder) ? rootDir : Path.Combine(rootDir, parentFolder);
        Directory.CreateDirectory(parentDir);
        var safe = SanitizeFileName(leafName);
        var name = safe; int n = 1;
        while (Directory.Exists(Path.Combine(parentDir, name))) name = $"{safe} ({++n})";
        Directory.CreateDirectory(Path.Combine(parentDir, name));
        return string.IsNullOrEmpty(parentFolder) ? name : Path.Combine(parentFolder, name);
    }

    public static string UniqueNewFolderLeaf(string rootDir, string parentFolder, string baseName = "new folder")
    {
        var parentDir = string.IsNullOrEmpty(parentFolder) ? rootDir : Path.Combine(rootDir, parentFolder);
        var name = baseName; int n = 1;
        while (Directory.Exists(Path.Combine(parentDir, name))) name = $"{baseName} {++n}";
        return name;
    }

    public static void RenameEntry(string rootDir, string folder, string fileName, string newName)
    {
        var jsonPath = Path.Combine(rootDir, folder, fileName);
        if (!File.Exists(jsonPath)) return;
        try
        {
            var entry = JsonSerializer.Deserialize<CustomSceneryAssetEntry>(File.ReadAllText(jsonPath));
            if (entry is null) return;
            var updated = entry with { Name = string.IsNullOrWhiteSpace(newName) ? entry.Name : newName.Trim() };
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(updated, JsonOptions));
        }
        catch {  }
    }

    public static void RenameFolder(string rootDir, string folder, string newLeafName)
    {
        if (string.IsNullOrEmpty(folder)) return;
        var src = Path.Combine(rootDir, folder);
        if (!Directory.Exists(src)) return;
        var parent = Path.GetDirectoryName(folder) ?? "";
        var destRel = string.IsNullOrEmpty(parent) ? SanitizeFileName(newLeafName) : Path.Combine(parent, SanitizeFileName(newLeafName));
        var dest = Path.Combine(rootDir, destRel);
        if (Directory.Exists(dest) || string.Equals(src, dest, StringComparison.OrdinalIgnoreCase)) return;
        Directory.Move(src, dest);
    }

    public static void DeleteFolder(string rootDir, string folder)
    {
        if (string.IsNullOrEmpty(folder)) return;
        var dir = Path.Combine(rootDir, folder);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        name = name.Trim();
        return string.IsNullOrEmpty(name) ? "Custom" : name;
    }


    public sealed record CustomObjectInstanceConfig(
        long StateFlags, int RefListIndex, int OnSpawnHeaderScriptID,
        int InstancesRelated, int PositionsRelated, int PathsRelated,
        List<int> Positions, List<int> Paths,
        List<uint> ParamList1, List<float> ParamList2, List<uint> ParamList3);

    public sealed record CustomObjectAssetEntry(
        string Name, string SourceLevel, uint ObjectId, int ThumbSize, CustomObjectInstanceConfig Instance);

    public static void SaveObject(string rootDir, string folder, string name, CustomObjectAssetEntry entry, byte[]? thumbPixels)
    {
        var dir = string.IsNullOrEmpty(folder) ? rootDir : Path.Combine(rootDir, folder);
        Directory.CreateDirectory(dir);
        var safe = SanitizeFileName(name);
        var baseName = safe; int n = 1;
        while (File.Exists(Path.Combine(dir, baseName + ".objentry"))) baseName = $"{safe} ({++n})";
        File.WriteAllText(Path.Combine(dir, baseName + ".objentry"), JsonSerializer.Serialize(entry, JsonOptions));
        if (thumbPixels is not null) File.WriteAllBytes(Path.Combine(dir, baseName + ".objthumb"), thumbPixels);
    }

    public static List<(string FileName, CustomObjectAssetEntry Entry)> ListObjectEntries(string rootDir, string folder)
    {
        var dir = string.IsNullOrEmpty(folder) ? rootDir : Path.Combine(rootDir, folder);
        var result = new List<(string, CustomObjectAssetEntry)>();
        if (!Directory.Exists(dir)) return result;
        foreach (var f in Directory.GetFiles(dir, "*.objentry").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var entry = JsonSerializer.Deserialize<CustomObjectAssetEntry>(File.ReadAllText(f));
                if (entry is not null) result.Add((Path.GetFileName(f), entry));
            }
            catch {  }
        }
        return result;
    }

    public static byte[]? LoadObjectThumb(string rootDir, string folder, string objEntryFileName)
    {
        var dir = string.IsNullOrEmpty(folder) ? rootDir : Path.Combine(rootDir, folder);
        var thumb = Path.Combine(dir, Path.GetFileNameWithoutExtension(objEntryFileName) + ".objthumb");
        return File.Exists(thumb) ? File.ReadAllBytes(thumb) : null;
    }

    public static void DeleteObjectEntry(string rootDir, string folder, string fileName)
    {
        var dir = string.IsNullOrEmpty(folder) ? rootDir : Path.Combine(rootDir, folder);
        var j = Path.Combine(dir, fileName); if (File.Exists(j)) File.Delete(j);
        var t = Path.Combine(dir, Path.GetFileNameWithoutExtension(fileName) + ".objthumb"); if (File.Exists(t)) File.Delete(t);
    }

    public static void MoveObjectEntry(string rootDir, string fromFolder, string fileName, string toFolder)
    {
        if (string.Equals(fromFolder, toFolder, StringComparison.OrdinalIgnoreCase)) return;
        var srcDir = string.IsNullOrEmpty(fromFolder) ? rootDir : Path.Combine(rootDir, fromFolder);
        var srcJson = Path.Combine(srcDir, fileName);
        if (!File.Exists(srcJson)) return;
        var destDir = string.IsNullOrEmpty(toFolder) ? rootDir : Path.Combine(rootDir, toFolder);
        Directory.CreateDirectory(destDir);
        var destJson = Path.Combine(destDir, fileName);
        if (File.Exists(destJson)) return;
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        File.Move(srcJson, destJson);
        var srcT = Path.Combine(srcDir, baseName + ".objthumb");
        var destT = Path.Combine(destDir, baseName + ".objthumb");
        if (File.Exists(srcT) && !File.Exists(destT)) File.Move(srcT, destT);
    }

    public static void RenameObjectEntry(string rootDir, string folder, string fileName, string newName)
    {
        var dir = string.IsNullOrEmpty(folder) ? rootDir : Path.Combine(rootDir, folder);
        var jsonPath = Path.Combine(dir, fileName);
        if (!File.Exists(jsonPath)) return;
        try
        {
            var entry = JsonSerializer.Deserialize<CustomObjectAssetEntry>(File.ReadAllText(jsonPath));
            if (entry is null) return;
            var updated = entry with { Name = string.IsNullOrWhiteSpace(newName) ? entry.Name : newName.Trim() };
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(updated, JsonOptions));
        }
        catch {  }
    }
}
