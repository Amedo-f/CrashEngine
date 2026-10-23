namespace CrashEngine.Runtime.Screen;

// Amedo 2026-10-01
public static class Bidi
{
    public static bool IsRtl(char c) =>
        c is >= '֐' and <= 'ࣿ' or >= 'יִ' and <= '﷿' or >= 'ﹰ' and <= '﻿'
        && !IsArabicDigit(c);

    private static bool IsArabicDigit(char c) => c is >= '٠' and <= '٩' or >= '۰' and <= '۹';

    private static bool IsLtr(char c) => !IsRtl(c) && char.IsLetterOrDigit(c);

    private static sbyte Strong(char c) => IsRtl(c) ? (sbyte)1 : IsLtr(c) ? (sbyte)0 : (sbyte)-1;

    public static bool Runs(string text, int start, int length, List<(int Start, int Length, bool Rtl)> runs)
    {
        runs.Clear();
        bool paragraph = false;
        for (int k = start; k < start + length; k++)
        {
            sbyte d = Strong(text[k]);
            if (d < 0) continue;
            paragraph = d == 1;
            break;
        }
        Span<sbyte> next = length <= 512 ? stackalloc sbyte[length] : new sbyte[length];
        sbyte after = paragraph ? (sbyte)1 : (sbyte)0;
        for (int k = length - 1; k >= 0; k--)
        {
            sbyte d = Strong(text[start + k]);
            if (d >= 0) after = d;
            next[k] = after;
        }
        sbyte before = paragraph ? (sbyte)1 : (sbyte)0;
        int runStart = start;
        bool runRtl = false;
        for (int k = 0; k < length; k++)
        {
            sbyte d = Strong(text[start + k]);
            bool rtl;
            if (d >= 0) { rtl = d == 1; before = d; }
            else rtl = before == next[k] ? before == 1 : paragraph;
            if (k == 0) runRtl = rtl;
            else if (rtl != runRtl)
            {
                runs.Add((runStart, start + k - runStart, runRtl));
                runStart = start + k;
                runRtl = rtl;
            }
        }
        if (length > 0) runs.Add((runStart, start + length - runStart, runRtl));
        return paragraph;
    }
}
