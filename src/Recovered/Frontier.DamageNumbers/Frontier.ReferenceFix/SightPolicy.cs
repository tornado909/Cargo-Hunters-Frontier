using System;

namespace Frontier.ReferenceFix;

internal static class SightPolicy
{
	internal static bool InViewport(float x, float y, float depth)
	{
		if (float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(depth) && depth > 0f && x >= 0f && x <= 1f && y >= 0f)
		{
			return y <= 1f;
		}
		return false;
	}

	internal static bool EnemyInRange(bool human, int id, int local, float dx, float dz)
	{
		if (!human && id != local && float.IsFinite(dx) && float.IsFinite(dz))
		{
			return dx * dx + dz * dz <= 10000f;
		}
		return false;
	}

	internal static float CameraYaw(float x, float z, float fallback)
	{
		if (!float.IsFinite(x) || !float.IsFinite(z) || !(x * x + z * z > 1E-05f))
		{
			return fallback;
		}
		return MapPolicy.Normalize((float)(Math.Atan2(x, z) * 180.0 / Math.PI));
	}

	internal static bool ClearHit(bool hit, int rootIndex, int rootVersion, int colliderIndex, int colliderVersion, int targetIndex, int targetVersion)
	{
		if (hit && (rootIndex != targetIndex || rootVersion != targetVersion))
		{
			if (colliderIndex == targetIndex)
			{
				return colliderVersion == targetVersion;
			}
			return false;
		}
		return true;
	}
}
