using System;

namespace Frontier.ReferenceFix;

internal static class RasterUploadPolicy
{
	internal static bool Changed(float px, float pz, float ph, float x, float z, float h, double age)
	{
		if (float.IsFinite(px) && !(Math.Abs(x - px) > 0.04f) && !(Math.Abs(z - pz) > 0.04f) && !(Math.Abs(MapPolicy.Delta(ph, h)) > 0.35f))
		{
			return age > 5.0;
		}
		return true;
	}
}
