using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Frontier.Core;

internal sealed record EliteWorldDiagnostic(int ObservedModels, int RankedModels, int MaterialsConfigured, int HaloRings, int LabelsDrawnLastFrame, bool CameraAvailable, string[] Shaders)
{
	[CompilerGenerated]
	private System.Type EqualityContract
	{
		[CompilerGenerated]
		get
		{
			return typeof(EliteWorldDiagnostic);
		}
	}

	[CompilerGenerated]
	public override string ToString()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Expected O, but got Unknown
		StringBuilder val = new StringBuilder();
		val.Append("EliteWorldDiagnostic");
		val.Append(" { ");
		if (PrintMembers(val))
		{
			val.Append(' ');
		}
		val.Append('}');
		return ((object)val).ToString();
	}

	[CompilerGenerated]
	private bool PrintMembers(StringBuilder builder)
	{
		RuntimeHelpers.EnsureSufficientExecutionStack();
		builder.Append("ObservedModels = ");
		builder.Append(((object)ObservedModels/*cast due to .constrained prefix*/).ToString());
		builder.Append(", RankedModels = ");
		builder.Append(((object)RankedModels/*cast due to .constrained prefix*/).ToString());
		builder.Append(", MaterialsConfigured = ");
		builder.Append(((object)MaterialsConfigured/*cast due to .constrained prefix*/).ToString());
		builder.Append(", HaloRings = ");
		builder.Append(((object)HaloRings/*cast due to .constrained prefix*/).ToString());
		builder.Append(", LabelsDrawnLastFrame = ");
		builder.Append(((object)LabelsDrawnLastFrame/*cast due to .constrained prefix*/).ToString());
		builder.Append(", CameraAvailable = ");
		builder.Append(((object)CameraAvailable/*cast due to .constrained prefix*/).ToString());
		builder.Append(", Shaders = ");
		builder.Append((object)Shaders);
		return true;
	}

	[CompilerGenerated]
	public bool Equals(EliteWorldDiagnostic? other)
	{
		if ((object)this != other)
		{
			if ((object)other != null && EqualityContract == other.EqualityContract && EqualityComparer<int>.Default.Equals(ObservedModels, other.ObservedModels) && EqualityComparer<int>.Default.Equals(RankedModels, other.RankedModels) && EqualityComparer<int>.Default.Equals(MaterialsConfigured, other.MaterialsConfigured) && EqualityComparer<int>.Default.Equals(HaloRings, other.HaloRings) && EqualityComparer<int>.Default.Equals(LabelsDrawnLastFrame, other.LabelsDrawnLastFrame) && EqualityComparer<bool>.Default.Equals(CameraAvailable, other.CameraAvailable))
			{
				return EqualityComparer<string[]>.Default.Equals(Shaders, other.Shaders);
			}
			return false;
		}
		return true;
	}
}
