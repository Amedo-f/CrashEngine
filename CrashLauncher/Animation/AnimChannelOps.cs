using System.Numerics;
using TwinAnimation = Twinsanity.TwinsanityInterchange.Common.Animation.TwinAnimation;
using TwinJointSettings = Twinsanity.TwinsanityInterchange.Common.Animation.JointSettings;
using TwinTransformation = Twinsanity.TwinsanityInterchange.Common.Animation.Transformation;
using TwinAnimatedTransformation = Twinsanity.TwinsanityInterchange.Common.Animation.AnimatedTransformation;
using TransformType = Twinsanity.TwinsanityInterchange.Common.Animation.Enums.TransformType;

namespace CrashLauncher;

// Amedo 2026-10-04
internal static class AnimChannelOps
{
	internal const int RotTurnRaw = 4096, RotHalfRaw = 2048;

	internal static TransformType[] TypesOf(TwinJointSettings js) =>
		new[] { js.TranslateX, js.TranslateY, js.TranslateZ, js.RotateX, js.RotateY, js.RotateZ, js.ScaleX, js.ScaleY, js.ScaleZ };

	internal static void SetType(TwinJointSettings js, int ch, TransformType t)
	{
		switch (ch)
		{
			case 0: js.TranslateX = t; break;
			case 1: js.TranslateY = t; break;
			case 2: js.TranslateZ = t; break;
			case 3: js.RotateX = t; break;
			case 4: js.RotateY = t; break;
			case 5: js.RotateZ = t; break;
			case 6: js.ScaleX = t; break;
			case 7: js.ScaleY = t; break;
			default: js.ScaleZ = t; break;
		}
	}

	internal static TwinTransformation Raw(short v) => new() { Value = v / 4096f };

	internal static TwinAnimation CloneTwinAnimation(TwinAnimation a)
	{
		var c = new TwinAnimation { TotalFrames = a.TotalFrames, PackerFlags = a.PackerFlags };
		foreach (var js in a.JointSettings)
		{
			var n = new TwinJointSettings
			{
				FacialShapesAmount = js.FacialShapesAmount,
				UseParentJointScale = js.UseParentJointScale,
				UseAdditionalRotation = js.UseAdditionalRotation,
				ChannelCounts = js.ChannelCounts,
				TransformationIndex = js.TransformationIndex,
				AnimationTransformationIndex = js.AnimationTransformationIndex,
			};
			var t = TypesOf(js);
			for (int ch = 0; ch < 9; ch++) SetType(n, ch, t[ch]);
			c.JointSettings.Add(n);
		}
		foreach (var s in a.StaticTransformations) c.StaticTransformations.Add(Raw(s.PureValue));
		foreach (var f in a.AnimatedTransformations)
		{
			var nf = new TwinAnimatedTransformation((ushort)f.Count);
			foreach (var v in f.Transforms) nf.Transforms.Add(Raw(v.PureValue));
			c.AnimatedTransformations.Add(nf);
		}
		return c;
	}

	internal const int MaxJoints = 0x7F, MaxStatic = 0xFFE / 2, MaxAnimatedPerFrame = 0x3FF;
	internal const ushort NoBlock = 0xFFFF;

	internal static byte CountsOf(TwinJointSettings js)
	{
		int animated = TypesOf(js).Count(t => t == TransformType.Animated);
		return (byte)((animated << 4) | (9 - animated));
	}

	internal static TwinAnimation Compact(TwinAnimation a)
	{
		int frames = a.AnimatedTransformations.Count;
		var c = new TwinAnimation { TotalFrames = a.TotalFrames, PackerFlags = a.PackerFlags };
		for (int f = 0; f < frames; f++) c.AnimatedTransformations.Add(new TwinAnimatedTransformation(0));
		foreach (var js in a.JointSettings)
		{
			var t = TypesOf(js);
			var n = new TwinJointSettings
			{
				FacialShapesAmount = js.FacialShapesAmount,
				UseParentJointScale = js.UseParentJointScale,
				UseAdditionalRotation = js.UseAdditionalRotation,
				ChannelCounts = js.ChannelCounts,
				TransformationIndex = (ushort)c.StaticTransformations.Count,
				AnimationTransformationIndex = (ushort)(frames > 0 ? c.AnimatedTransformations[0].Count : 0),
			};
			for (int ch = 0; ch < 9; ch++) SetType(n, ch, t[ch]);
			if (!t.Contains(TransformType.Animated)) n.AnimationTransformationIndex = NoBlock;
			if (t.All(x => x == TransformType.Animated)) n.TransformationIndex = NoBlock;
			int s = js.TransformationIndex, slot = js.AnimationTransformationIndex;
			for (int ch = 0; ch < 9; ch++)
			{
				if (t[ch] == TransformType.Animated)
				{
					for (int f = 0; f < frames; f++) c.AnimatedTransformations[f].Transforms.Add(Raw(a.AnimatedTransformations[f].Transforms[slot].PureValue));
					slot++;
				}
				else
				{
					c.StaticTransformations.Add(Raw(a.StaticTransformations[s].PureValue));
					s++;
				}
			}
			c.JointSettings.Add(n);
		}
		return c;
	}

	internal static string? FitsGameLimits(TwinAnimation a)
	{
		if (a.JointSettings.Count > MaxJoints) return $"{a.JointSettings.Count} bones (the game keeps at most {MaxJoints})";
		if (a.StaticTransformations.Count > MaxStatic) return $"{a.StaticTransformations.Count} fixed values (at most {MaxStatic})";
		if (a.AnimatedTransformations.Count > 0 && a.AnimatedTransformations[0].Count > MaxAnimatedPerFrame)
			return $"{a.AnimatedTransformations[0].Count} animated values per frame (at most {MaxAnimatedPerFrame})";
		if (a.AnimatedTransformations.Count != a.TotalFrames) return $"{a.AnimatedTransformations.Count} frames but the header says {a.TotalFrames}";
		return null;
	}

	internal static bool SameChannels(TwinAnimation a, TwinAnimation b)
	{
		if (a.JointSettings.Count != b.JointSettings.Count || a.AnimatedTransformations.Count != b.AnimatedTransformations.Count) return false;
		for (int j = 0; j < a.JointSettings.Count; j++)
		{
			var ta = TypesOf(a.JointSettings[j]);
			var tb = TypesOf(b.JointSettings[j]);
			for (int ch = 0; ch < 9; ch++)
				if (ta[ch] != tb[ch] || !RawChannel(a, j, ch).AsSpan().SequenceEqual(RawChannel(b, j, ch))) return false;
		}
		return true;
	}

	internal static int ChannelSlot(TwinJointSettings js, int ch)
	{
		var t = TypesOf(js);
		int slot = js.AnimationTransformationIndex;
		for (int k = 0; k < ch; k++) if (t[k] == TransformType.Animated) slot++;
		return slot;
	}

	internal static int[] RawChannel(TwinAnimation a, int joint, int ch)
	{
		var js = a.JointSettings[joint];
		var t = TypesOf(js);
		int frames = a.AnimatedTransformations.Count;
		int animatedBefore = 0, staticBefore = 0;
		for (int k = 0; k < ch; k++) if (t[k] == TransformType.Animated) animatedBefore++; else staticBefore++;
		var v = new int[Math.Max(1, frames)];
		if (t[ch] != TransformType.Animated)
		{
			int s = js.TransformationIndex + staticBefore;
			Array.Fill(v, s < a.StaticTransformations.Count ? a.StaticTransformations[s].PureValue : 0);
			return v;
		}
		int slot = js.AnimationTransformationIndex + animatedBefore;
		for (int f = 0; f < frames; f++)
		{
			var tr = a.AnimatedTransformations[f].Transforms;
			v[f] = slot < tr.Count ? tr[slot].PureValue : 0;
		}
		if (ch is >= 3 and <= 5)
			for (int f = 1; f < v.Length; f++)
			{
				while (v[f] - v[f - 1] > RotHalfRaw) v[f] -= RotTurnRaw;
				while (v[f] - v[f - 1] < -RotHalfRaw) v[f] += RotTurnRaw;
			}
		return v;
	}

	internal static int Lerp(int a, int b, int k, int n) => (int)Math.Round(a + (b - a) * (double)k / n, MidpointRounding.AwayFromZero);

	internal static bool ExactBetween(int[] v, int a, int b)
	{
		for (int k = a + 1; k < b; k++) if (Lerp(v[a], v[b], k - a, b - a) != v[k]) return false;
		return true;
	}

	internal static List<int> KeysOf(int[] v)
	{
		var keys = new List<int> { 0 };
		int n = v.Length, last = 0;
		while (last < n - 1)
		{
			int best = last + 1;
			double lo = double.NegativeInfinity, hi = double.PositiveInfinity;
			for (int m = last + 1; m < n; m++)
			{
				int dk = m - last;
				double s = (double)(v[m] - v[last]) / dk;
				if (s >= lo - 1e-9 && s <= hi + 1e-9 && ExactBetween(v, last, m)) best = m;
				lo = Math.Max(lo, (v[m] - 0.5 - v[last]) / dk);
				hi = Math.Min(hi, (v[m] + 0.5 - v[last]) / dk);
				if (lo > hi + 1e-9) break;
			}
			keys.Add(best);
			last = best;
		}
		return keys;
	}

	internal static void MakeAnimated(TwinAnimation a, int joint, int ch)
	{
		var js = a.JointSettings[joint];
		var t = TypesOf(js);
		if (t[ch] == TransformType.Animated) return;
		int frames = a.AnimatedTransformations.Count;
		var values = new int[9][];
		for (int k = 0; k < 9; k++) values[k] = RawChannel(a, joint, k);
		SetType(js, ch, TransformType.Animated);
		if (js.ChannelCounts != 0) js.ChannelCounts = CountsOf(js);
		t[ch] = TransformType.Animated;
		ushort staticStart = (ushort)a.StaticTransformations.Count;
		bool anyStatic = false;
		for (int k = 0; k < 9; k++)
			if (t[k] != TransformType.Animated) { a.StaticTransformations.Add(Raw((short)values[k][0])); anyStatic = true; }
		if (anyStatic) js.TransformationIndex = staticStart;
		ushort slotStart = (ushort)(frames > 0 ? a.AnimatedTransformations[0].Count : 0);
		for (int f = 0; f < frames; f++)
			for (int k = 0; k < 9; k++)
				if (t[k] == TransformType.Animated) a.AnimatedTransformations[f].Transforms.Add(Raw(Wrap(values[k][f], k)));
		js.AnimationTransformationIndex = slotStart;
	}

	internal static short Wrap(int raw, int ch)
	{
		if (ch is >= 3 and <= 5)
		{
			while (raw > short.MaxValue) raw -= RotTurnRaw;
			while (raw < short.MinValue) raw += RotTurnRaw;
		}
		return (short)Math.Clamp(raw, short.MinValue, short.MaxValue);
	}

	internal static void WriteChannel(TwinAnimation a, int joint, int ch, int[] v, int from, int to)
	{
		int slot = ChannelSlot(a.JointSettings[joint], ch);
		for (int f = Math.Max(0, from); f <= to && f < a.AnimatedTransformations.Count; f++)
			a.AnimatedTransformations[f].Transforms[slot] = Raw(Wrap(v[f], ch));
	}

	internal static float RawToValue(int raw, int ch) => ch is >= 3 and <= 5 ? raw * (360f * 16f / 65536f) : raw / 4096f;

	internal static int ValueToRaw(float value, int ch) =>
		(int)Math.Round(ch is >= 3 and <= 5 ? value * 65536f / (360f * 16f) : value * 4096f, MidpointRounding.AwayFromZero);

	internal static int EulerRaw(float rad) => (int)MathF.Round(rad * RotTurnRaw / MathF.Tau, MidpointRounding.AwayFromZero);

	internal static float NearAngle(float v, float near) => v + MathF.Tau * MathF.Round((near - v) / MathF.Tau);

	internal static Vector3 GameEulerFrom(Quaternion q, Vector3 near)
	{
		q = Quaternion.Normalize(q);
		var m = Matrix4x4.CreateFromQuaternion(q);
		float cy = MathF.Sqrt(m.M11 * m.M11 + m.M12 * m.M12);
		float x = MathF.Atan2(m.M23, m.M33);
		if (cy * MathF.Abs(NearAngle(x, near.X) - near.X) < 1e-5f) x = near.X;
		var r = Matrix4x4.CreateFromQuaternion(q * Quaternion.Conjugate(Quaternion.CreateFromAxisAngle(Vector3.UnitX, x)));
		float y = MathF.Atan2(-r.M13, r.M33), z = MathF.Atan2(-r.M21, r.M22);
		var a = new Vector3(NearAngle(x, near.X), NearAngle(y, near.Y), NearAngle(z, near.Z));
		var b = new Vector3(NearAngle(x + MathF.PI, near.X), NearAngle(MathF.PI - y, near.Y), NearAngle(z + MathF.PI, near.Z));
		return Vector3.DistanceSquared(a, near) <= Vector3.DistanceSquared(b, near) ? a : b;
	}
}
