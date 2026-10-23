namespace CrashEngine.Runtime.Physics;

// Amedo
public sealed class ChunkRigidBodies
{
    public const int Capacity = 0xFF;

    public readonly PhysicsController?[] First = new PhysicsController?[Capacity + 1];
    public readonly PhysicsController?[] Second = new PhysicsController?[Capacity + 1];
    public int FirstCount;
    public int SecondCount;

    // Amedo
    public bool PutFirst_252430(PhysicsController c)
    {
        if (FirstCount >= Capacity) return false;
        c.Lists_D8 ??= this;
        c.First_DC = FirstCount;
        First[FirstCount++] = c;
        return true;
    }

    // Amedo
    public bool TakeFirst_252488(PhysicsController c)
    {
        int i = c.First_DC;
        if (i == 0xFFFF) return false;
        var last = First[--FirstCount]!;
        First[i] = last;
        last.First_DC = i;
        ForgetFirstIndex_2538F8(c);
        return true;
    }

    // Amedo
    public bool PutSecond_2525B8(PhysicsController c)
    {
        if (SecondCount >= Capacity) return false;
        c.Lists_D8 ??= this;
        c.Second_DE = SecondCount;
        Second[SecondCount++] = c;
        return true;
    }

    // Amedo
    public bool TakeSecond_252610(PhysicsController c)
    {
        int i = c.Second_DE;
        if (i == 0xFF) return false;
        var last = Second[--SecondCount]!;
        Second[i] = last;
        last.Second_DE = i;
        ForgetSecondIndex_253938(c);
        return true;
    }

    // Amedo
    public void Release_2524F8()
    {
        for (int i = 0; i < FirstCount; i++)
        {
            var c = First[i]!;
            c.First_DC = 0xFFFF;
            c.Flags &= ~PhysicsController.InPairList;
        }
        for (int i = 0; i < SecondCount; i++) ForgetSecondIndex_253938(Second[i]!);
        Array.Clear(First);
        Array.Clear(Second);
        FirstCount = 0;
        SecondCount = 0;
    }

    // Amedo
    public static void ForgetFirstIndex_2538F8(PhysicsController c)
    {
        c.Flags &= ~PhysicsController.InPairList;
        c.First_DC = 0xFFFF;
        if (c.Second_DE == 0xFF) c.Lists_D8 = null;
    }

    // Amedo
    public static void ForgetSecondIndex_253938(PhysicsController c)
    {
        c.Second_DE = 0xFF;
        if (c.First_DC == 0xFFFF) c.Lists_D8 = null;
    }
}
