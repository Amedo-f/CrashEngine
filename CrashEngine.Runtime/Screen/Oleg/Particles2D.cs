using System.Numerics;
using CrashEngine.Core;
using CrashEngine.Runtime.Character;

namespace CrashEngine.Runtime.Screen.Oleg;

// Amedo
public struct Particle2D
{
    public Vector2 Position_00;
    public float Age_08;
    public short VelocityX_0C, VelocityY_0E;
}

// Amedo
public sealed class FloatCurve(int count)
{
    public readonly float[] Keys_04 = new float[count];
}

// Amedo
public sealed class SlotPool<T> where T : struct
{
    public short Capacity_00, Step_02, Used_04, Free_06;
    public short[] Links_08 = [];
    public T[] Data_0C = [];

    // Amedo
    public SlotPool()
    {
        Step_02 = 10;
        Free_06 = -1;
        Capacity_00 = 0;
        Used_04 = 0;
    }

    // Amedo
    public void Reserve_1ACB88(int count)
    {
        if (!((uint)Capacity_00 < (uint)count)) return;
        short step = Step_02;
        Step_02 = (short)(count - (ushort)Capacity_00);
        Grow_1AAAD8();
        Step_02 = step;
    }

    // Amedo
    public void Grow_1AAAD8()
    {
        if ((Capacity_00 & 1) != 0) Capacity_00++;
        if ((Step_02 & 1) != 0) Step_02++;
        int capacity = Capacity_00, total = capacity + Step_02;
        var data = new T[total];
        var links = new short[total];
        if (capacity != 0)
        {
            for (int i = 0; i < capacity && i < Links_08.Length; i++)
                if (Links_08[i] == -1) data[i] = Data_0C[i];
            Array.Fill(links, (short)-1, 0, capacity);
        }
        int k = capacity;
        for (; k < total; k++) links[k] = (short)(k + 1);
        if (0 < k) links[k - 1] = -2;
        Links_08 = links;
        Data_0C = data;
        Capacity_00 = (short)total;
        Free_06 = (short)capacity;
    }

    // Amedo
    public int Allocate_1AC878()
    {
        if (Used_04 < Capacity_00)
        {
            int i = Free_06;
            Free_06 = Links_08[i];
            Links_08[i] = -1;
            Used_04++;
            return i;
        }
        Grow_1AAAD8();
        return Allocate_1AC878();
    }

    // Amedo
    public void Free_1AC8F0(int i)
    {
        Links_08[i] = Free_06;
        Free_06 = (short)i;
        Used_04--;
    }

    // Amedo
    public void Clear_17C298()
    {
        if (Capacity_00 <= 0 || Used_04 == 0) return;
        int i = 0;
        for (; i < Capacity_00 - 1; i++) Links_08[i] = (short)(i + 1);
        Links_08[i] = -2;
        Free_06 = 0;
        Used_04 = 0;
    }
}

// Amedo
public struct SlotIterator<T> where T : struct
{
    public short Index_04, Visited_06;
    public SlotPool<T> Pool_08;

    // Amedo
    public static SlotIterator<T> First(SlotPool<T> pool)
    {
        var it = new SlotIterator<T> { Pool_08 = pool };
        while (it.Index_04 < pool.Capacity_00 - 1 && pool.Links_08[it.Index_04] != -1) it.Index_04++;
        return it;
    }

    // Amedo
    public readonly bool IsDone_1ACA18() => Visited_06 == Pool_08.Used_04;

    // Amedo
    public readonly ref T Current_1AB6E0() => ref Pool_08.Data_0C[Index_04];

    // Amedo
    public void Next_1AB6F8()
    {
        var pool = Pool_08;
        if (!(Visited_06 < pool.Used_04 - 1))
        {
            Visited_06 = pool.Used_04;
            return;
        }
        while (Visited_06 < pool.Used_04)
        {
            Index_04++;
            if (pool.Links_08[Index_04] != -1) continue;
            Visited_06++;
            return;
        }
    }
}

// Amedo
public class Emitter2D : IPlacedEmitter
{
    protected static readonly float Aspect43 = BitConverter.Int32BitsToSingle(0x3FAAAAAB);

    public readonly SlotPool<Particle2D> Pool_00 = new();
    public Vector2 Size_14, Spread_1C, Centre_24;
    public Sprite? Sprite_2C;
    public float Life_30, Chance_34;
    public int Count_38;
    public ColourCurve? Colours_3C;
    public Vector2Curve? Scales_40;
    public FloatCurve? Turns_44;

    // Amedo
    public Emitter2D() => Count_38 = 1;

    // Amedo
    public void SetSprite_1AB5E8(Sprite? sprite, int capacity)
    {
        Sprite_2C = sprite;
        Pool_00.Reserve_1ACB88(capacity);
    }

    // Amedo
    public int Step_1AB778(float dt, Vector2 p0, Vector2 p1)
    {
        Update(dt);
        for (int n = Count_38; n != 0; n--)
            if (Pool_00.Capacity_00 != Pool_00.Used_04) Spawn(p0, p1);
        return Pool_00.Used_04;
    }

    public void Step(Vector2 p0, Vector2 p1, float dt) => Step_1AB778(dt, p0, p1);

    public void DrawPlaced(OverlayQueue overlay, Matrix3x2 m) => Draw(overlay, m);

    protected virtual void Spawn(Vector2 p0, Vector2 p1) => Spawn_1A6C88(p0, p1);

    protected virtual void Update(float dt) => Update_1A6D98(dt);

    public virtual void Draw(OverlayQueue overlay, Matrix3x2 placed) => Draw_1A6F60(overlay, placed);

    // Amedo
    private void Spawn_1A6C88(Vector2 p0, Vector2 p1)
    {
        var rng = GameRandom.Shared;
        if (!(rng.GetRandFloat_18C0B8() < Chance_34)) return;
        float rx = rng.RandomSigned_18C0F0();
        float ry = rng.RandomSigned_18C0F0();
        var half = p1;
        half.X = (half.X - p0.X) * 0.5f * Spread_1C.X;
        half.Y = (half.Y - p0.Y) * 0.5f * Spread_1C.Y;
        float x = rx * half.X, y = ry * half.Y;
        int i = Pool_00.Allocate_1AC878();
        ref var p = ref Pool_00.Data_0C[i];
        p.Position_00.X = x + Centre_24.X;
        p.Position_00.Y = y + Centre_24.Y;
        p.Age_08 = 0f;
    }

    // Amedo
    private void Update_1A6D98(float dt)
    {
        if (Pool_00.Used_04 == 0) return;
        var it = SlotIterator<Particle2D>.First(Pool_00);
        while (!it.IsDone_1ACA18())
        {
            ref var p = ref it.Current_1AB6E0();
            float age = p.Age_08 + dt;
            p.Age_08 = age;
            if (Life_30 < age)
            {
                Pool_00.Free_1AC8F0(it.Index_04);
                it.Visited_06--;
            }
            it.Next_1AB6F8();
        }
    }

    // Amedo
    protected static float TurnAt(FloatCurve turns, float t)
    {
        var k = turns.Keys_04;
        if (!(t < 1f)) return k[^1];
        float f = (k.Length - 1) * t;
        int i = CharacterController.Cvt(f);
        return k[i] + (k[i + 1] - k[i]) * (f - i);
    }

    // Amedo
    private void Draw_1A6F60(OverlayQueue overlay, Matrix3x2 placed)
    {
        if (Pool_00.Used_04 == 0) return;
        float aspect = OverlayQueue.ScreenAspect * Aspect43;
        var it = SlotIterator<Particle2D>.First(Pool_00);
        while (!it.IsDone_1ACA18())
        {
            var p = it.Current_1AB6E0();
            float t = p.Age_08 / Life_30;
            var scale = Vector2.One;
            if (Scales_40 is { } scales) scale = scales.SampleVector2Curve_17C4E8(t);
            var m = Matrix3x2.Identity;
            m.M11 = Size_14.X * aspect * scale.X;
            m.M22 = Size_14.Y * scale.Y;
            if (Turns_44 is { } turns) m *= WidgetEffect.Rotation_18BC98(WidgetEffect.AngleFrom_11BFA0(TurnAt(turns, t)));
            m.M31 = p.Position_00.X;
            m.M32 = p.Position_00.Y;
            m *= placed;
            if (Colours_3C is { } colours)
            {
                var c = colours.Sample_1AB980(t);
                Sprite_2C?.Draw(overlay, m, Colour.Set_101218(c.X, c.Y, c.Z, c.W));
            }
            else Sprite_2C?.Draw(overlay, m, null);
            it.Next_1AB6F8();
        }
    }
}

// Amedo
public sealed class RadialEmitter2D : Emitter2D
{
    private static readonly float TwoPi = BitConverter.Int32BitsToSingle(0x40C90FDB), Speed = 8192f, Inv32768 = BitConverter.Int32BitsToSingle(0x38000000);

    public Vector4 Colour_50;

    protected override void Spawn(Vector2 p0, Vector2 p1) => Spawn_1A72F8(p0, p1);

    protected override void Update(float dt) => Update_1A7490(dt);

    public override void Draw(OverlayQueue overlay, Matrix3x2 placed) => Draw_1A76B0(overlay, placed);

    // Amedo
    private void Spawn_1A72F8(Vector2 p0, Vector2 p1)
    {
        var rng = GameRandom.Shared;
        if (!(rng.GetRandFloat_18C0B8() < Chance_34)) return;
        var (sin, cos) = GameMath.SinCosVu0_2E4C30(rng.GetRandFloat_18C0B8() * TwoPi);
        var half = p1;
        half.X = (half.X - p0.X) * 0.5f * Spread_1C.X;
        half.Y = (half.Y - p0.Y) * 0.5f * Spread_1C.Y;
        float r = MathF.Sqrt(half.X * half.X + half.Y * half.Y);
        int i = Pool_00.Allocate_1AC878();
        ref var p = ref Pool_00.Data_0C[i];
        p.Position_00.X = cos * r + Centre_24.X;
        p.Position_00.Y = sin * r + Centre_24.Y;
        p.Age_08 = 0f;
        p.VelocityX_0C = (short)CharacterController.Cvt(cos * Speed);
        p.VelocityY_0E = (short)CharacterController.Cvt(sin * Speed);
    }

    // Amedo
    private void Update_1A7490(float dt)
    {
        if (Pool_00.Used_04 == 0) return;
        var it = SlotIterator<Particle2D>.First(Pool_00);
        while (!it.IsDone_1ACA18())
        {
            ref var p = ref it.Current_1AB6E0();
            float age = p.Age_08 + dt;
            p.Age_08 = age;
            if (Life_30 < age)
            {
                Pool_00.Free_1AC8F0(it.Index_04);
                it.Visited_06--;
            }
            else
            {
                p.Position_00.X += p.VelocityX_0C * Inv32768 * dt;
                p.Position_00.Y += p.VelocityY_0E * Inv32768 * dt;
            }
            it.Next_1AB6F8();
        }
    }

    // Amedo
    private void Draw_1A76B0(OverlayQueue overlay, Matrix3x2 placed)
    {
        if (Pool_00.Used_04 == 0) return;
        float aspect = OverlayQueue.ScreenAspect * Aspect43;
        var it = SlotIterator<Particle2D>.First(Pool_00);
        while (!it.IsDone_1ACA18())
        {
            var p = it.Current_1AB6E0();
            float t = p.Age_08 / Life_30;
            var scale = Scales_40!.SampleVector2Curve_17C4E8(t);
            var m = Matrix3x2.Identity;
            m.M11 = Size_14.X * scale.X;
            m.M22 = Size_14.Y * aspect * scale.Y;
            m *= WidgetEffect.Rotation_18BC98(WidgetEffect.AngleFrom_11BFA0(TurnAt(Turns_44!, t)));
            m.M31 = p.Position_00.X;
            m.M32 = p.Position_00.Y;
            m *= placed;
            Sprite_2C?.Draw(overlay, m, Colour.Set_101218(Colour_50.X, Colour_50.Y, Colour_50.Z, Colour_50.W));
            it.Next_1AB6F8();
        }
    }
}

// Amedo
public sealed class SparkleEffect : WidgetEffect
{
    public readonly RadialEmitter2D Emitter_20 = new();

    public override WidgetEffect? Step(Vector2 p0, Vector2 p1, float dt, bool shown) => Step_178F68(dt, p0, p1);

    // Amedo
    private WidgetEffect? Step_178F68(float dt, Vector2 p0, Vector2 p1)
    {
        float elapsed = Elapsed_04;
        Elapsed_04 = elapsed + dt;
        float progress = elapsed / Duration_00;
        Progress_08 = progress;
        WidgetEffect? next = this;
        if (1f <= progress)
        {
            Elapsed_04 = 0f;
            if (--Count_0C <= 0) next = Next_10;
        }
        int used = Emitter_20.Step_1AB778(dt, p0, p1);
        if (next is not null) return next;
        Emitter_20.Count_38 = 0;
        return used == 0 ? null : this;
    }

    // Amedo
    public override void DrawPlaced(OverlayQueue overlay, Matrix3x2 m) => Emitter_20.Draw(overlay, m);

    // Amedo
    public override void Reset_178DE0()
    {
        Emitter_20.Pool_00.Clear_17C298();
        Emitter_20.Count_38 = 4;
        Emitter_20.Life_30 = 0.5f;
        Emitter_20.Chance_34 = BitConverter.Int32BitsToSingle(0x3FF33333);
        Count_0C = 0;
        Elapsed_04 = 0f;
        Progress_08 = 0f;
    }
}
