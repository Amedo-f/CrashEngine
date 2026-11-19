using System.Text;
using CrashEngine.Importer.AgentLabReadable;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;

namespace CrashLauncher;

// Amedo 2026-11-18
public static class BehaviourGraphs
{
    public static PS2BehaviourGraph Clone(PS2BehaviourGraph graph)
    {
        var copy = new PS2BehaviourGraph();
        CopyInto(graph, copy);
        return copy;
    }

    public static byte[] Bytes(PS2BehaviourGraph graph)
    {
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true)) graph.Write(writer);
        return ms.ToArray();
    }

    public static void CopyInto(PS2BehaviourGraph from, PS2BehaviourGraph into)
    {
        var bytes = Bytes(from);
        using var reader = new BinaryReader(new MemoryStream(bytes), Encoding.ASCII);
        into.Read(reader, bytes.Length);
    }

    public static string ToText(PS2BehaviourGraph graph)
    {
        using var ms = new MemoryStream();
        using (var writer = new StreamWriter(ms, Encoding.UTF8, 4096, leaveOpen: true)) graph.WriteText(writer);
        ms.Position = 0;
        using var reader = new StreamReader(ms);
        return reader.ReadToEnd();
    }

    public static PS2BehaviourGraph FromText(string raw, uint id)
    {
        var graph = new PS2BehaviourGraph();
        using var reader = new LineReader(new MemoryStream(Encoding.UTF8.GetBytes(raw)));
        try { graph.ReadText(reader); }
        catch (NullReferenceException) when (reader.EndOfStream) { throw new ScriptTextException(Math.Max(1, reader.Line), "the text ends before the script is closed (a '}' is missing)"); }
        catch (Exception ex) when (ex is not ScriptTextException) { throw new ScriptTextException(Math.Max(1, reader.Line), ex.Message); }
        graph.SetID(id);
        graph.Compile();
        return graph;
    }

    private sealed class LineReader(Stream stream) : StreamReader(stream)
    {
        public int Line { get; private set; }

        public override string? ReadLine()
        {
            var line = base.ReadLine();
            if (line is not null) Line++;
            return line;
        }
    }
}
