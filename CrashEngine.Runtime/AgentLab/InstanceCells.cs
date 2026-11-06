using System.Numerics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.AgentLab;

// Amedo
public abstract class InstanceCell
{
    public const int CellCount = 0x301;
    public const int GlobalCell = 0x300;

    private static readonly float[] Size_2FBB30 = [4f, 8f, 20f, 500f];
    private static readonly float[] Margin_2FBB40 = [2f, 4f, 10f, 250f];
    private static readonly int[] Span_2FBB50 = [16, 16, 16, 1];
    private static readonly int[] Base_3B4640 = [0, 0x100, 0x200, 0x300];
    private static readonly int[] Mask_2FBB70 = [0xF, 0xF, 0xF, 0];

    internal InstanceCell? Prev_148;
    internal InstanceCell? Next_14C;
    internal int Index_24 = -1;
    internal InstanceCell?[]? Grid_20;
    internal Vector3 BoxMin_40;
    internal Vector3 BoxMax_50;
    internal bool Queued_4;
    internal bool Pinned_2C;

    public abstract uint Flags_4 { get; }
    public abstract uint Components_D4 { get; }
    public abstract Vector3 Position_30 { get; }
    public abstract WorldChunk? Chunk_A0 { get; }

    // Amedo
    public static int CellLevel_1E8C10(Vector3 min, Vector3 max)
    {
        float extent = MathF.Max(max.Z - min.Z, max.X - min.X);
        if (extent < Margin_2FBB40[0]) return 0;
        if (extent < Margin_2FBB40[1]) return 1;
        if (extent < Margin_2FBB40[2]) return 2;
        return 3;
    }

    // Amedo
    public int InstanceCell_1E8C88()
    {
        int l = CellLevel_1E8C10(BoxMin_40, BoxMax_50);
        var p = Position_30;
        int x = GameMath.Trunc(p.X / Size_2FBB30[l]) & Mask_2FBB70[l];
        int z = GameMath.Trunc(p.Z / Size_2FBB30[l]) & Mask_2FBB70[l];
        return Base_3B4640[l] + x + z * Span_2FBB50[l];
    }

    // Amedo
    public static int CellsOfVolume_1E8D50(Vector3 min, Vector3 max, int[] cells)
    {
        int n = 0;
        for (int l = 0; l < 3; l++)
        {
            float s = Size_2FBB30[l], h = Margin_2FBB40[l];
            int x0 = GameMath.Trunc((min.X - h) / s);
            int z0 = GameMath.Trunc((min.Z - h) / s);
            int x1 = GameMath.Trunc((max.X + h) / s);
            int z1 = GameMath.Trunc((max.Z + h) / s);
            int span = Span_2FBB50[l];
            if (!(x1 < x0 + span - 1)) x1 = x0 + span - 1;
            if (!(z1 < z0 + span - 1)) z1 = z0 + span - 1;
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                    cells[n++] = Base_3B4640[l] + (x & Mask_2FBB70[l]) + (z & Mask_2FBB70[l]) * span;
        }
        cells[n++] = GlobalCell;
        return n;
    }

    // Amedo
    public static void Insert_1999B0(InstanceCell?[] grid, int index, InstanceCell item)
    {
        var head = grid[index];
        if (head is null)
        {
            grid[index] = item;
            item.Prev_148 = null;
            item.Next_14C = null;
            return;
        }
        head.Prev_148 = item;
        item.Next_14C = head;
        grid[index] = item;
    }

    // Amedo
    public static void Remove_199938(InstanceCell?[] grid, int index, InstanceCell item)
    {
        var prev = item.Prev_148;
        var next = item.Next_14C;
        if (prev is null)
        {
            if (next is null) grid[index] = null;
            else
            {
                next.Prev_148 = null;
                grid[index] = next;
            }
        }
        else if (next is null) prev.Next_14C = null;
        else
        {
            prev.Next_14C = next;
            next.Prev_148 = prev;
        }
        item.Next_14C = null;
        item.Prev_148 = null;
    }

    // Amedo
    public bool Filter_1EA1E8(uint mask, uint exclude, uint require, bool requireAll)
    {
        uint flags = Flags_4;
        if ((flags & exclude) != 0 || (Components_D4 & mask) == 0) return false;
        return requireAll ? (flags & require) == require : (flags & require) != 0;
    }
}

// Amedo
public sealed class AgentCell : InstanceCell
{
    public readonly AgentLabAgent Agent;
    public AgentCell(AgentLabAgent agent) => Agent = agent;
    public override uint Flags_4 => Agent.AgentFlags;
    public override uint Components_D4 => Agent.ComponentMask;
    public override Vector3 Position_30 => Agent.GamePosition;
    public override WorldChunk? Chunk_A0 => Agent.InChunk_A0 ? Agent.Chunk : null;
}

// Amedo
public sealed class TriggerCell : InstanceCell
{
    public const uint TriggerComponent_80 = 0x80;
    public readonly LayoutTrigger Trigger;
    public TriggerCell(LayoutTrigger trigger) => Trigger = trigger;
    public override uint Flags_4 => 0;
    public override uint Components_D4 => TriggerComponent_80;
    public override Vector3 Position_30 => Trigger.Matrix.Translation;
    public override WorldChunk? Chunk_A0 => Trigger.Chunk;
}

// Amedo
public sealed class SceneryCell : InstanceCell
{
    public readonly DynamicSceneryInstance Scenery;
    public SceneryCell(DynamicSceneryInstance scenery) => Scenery = scenery;
    public override uint Flags_4 => Scenery.AgentFlags;
    public override uint Components_D4 => Scenery.ComponentMask;
    public override Vector3 Position_30 => Scenery.GameMatrix.Translation;
    public override WorldChunk? Chunk_A0 => Scenery.Chunk;
}
