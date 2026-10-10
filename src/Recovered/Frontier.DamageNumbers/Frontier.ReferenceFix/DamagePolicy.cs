using System;

namespace Frontier.ReferenceFix;

internal static class DamagePolicy
{
	internal static bool Accept(int local, int attacker, int target, float damage, bool hidden, bool predicted)
	{
		if (local > 0 && attacker == local && target > 0 && target != local && float.IsFinite(damage) && damage > 0f && !hidden)
		{
			return !predicted;
		}
		return false;
	}

	internal static bool? Critical(float before, float after, float ordinaryFactor, float criticalFactor)
	{
		if (!float.IsFinite(before) || !float.IsFinite(after) || before <= 0f || after <= 0f || !float.IsFinite(ordinaryFactor) || ordinaryFactor <= 0f)
		{
			return null;
		}
		double num = (double)after / (double)before;
		double num2 = Math.Max(0.001, Math.Abs(num) * 0.0001);
		if (Math.Abs(num - (double)ordinaryFactor) <= num2)
		{
			return false;
		}
		if (criticalFactor > 1f && Math.Abs(num - (double)(ordinaryFactor * criticalFactor)) <= num2)
		{
			return true;
		}
		return null;
	}

	internal static ulong Projectile(uint tick, int owner, byte index, int data)
	{
		return tick | ((ulong)(ushort)owner << 32) | ((ulong)index << 48) | ((ulong)(byte)data << 56);
	}

	internal static int FontSize(bool critical, float scale)
	{
		return Math.Clamp((int)Math.Round((float)(critical ? 32 : 22) * scale), critical ? 24 : 16, critical ? 48 : 34);
	}

	internal static float Alpha(double age, double life)
	{
		if (!(age < 0.0) && !(age >= life))
		{
			if (!(age < life * 0.55))
			{
				return (float)((life - age) / (life * 0.45));
			}
			return 1f;
		}
		return 0f;
	}
}
