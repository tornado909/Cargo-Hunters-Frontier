using System.IO;
using System.Linq;

namespace Frontier.Reserve;

internal static class Content
{
	internal static LocationDefinition Map;

	internal static void Load()
	{
		Map = LocationDefinition.Load(Path.Combine(Identity.Folder, "reserve.location.json"));
		Identity.Hash = Map.CollisionFingerprint();
		Trace.Data["geometryFingerprint"] = Identity.Hash;
		Trace.Data["contentCounts"] = new
		{
			rooms = Map.Rooms.Length,
			solids = Map.Boxes.Count((BoxDef b) => b.Solid),
			decorations = Map.Boxes.Count((BoxDef b) => !b.Solid),
			anchors = Map.Anchors.Length
		};
		WalkGrid walkGrid = new WalkGrid(Map);
		foreach (Anchor item in Map.Anchors.Where((Anchor a) => a.Kind == "player"))
		{
			int[] array = walkGrid.Flood(walkGrid.Index(item.Position[0], item.Position[2]));
			Anchor[] anchors = Map.Anchors;
			foreach (Anchor anchor in anchors)
			{
				int num2 = walkGrid.Index(anchor.Position[0], anchor.Position[2]);
				if (num2 < 0 || array[num2] < 0)
				{
					throw new InvalidDataException("Unreachable placement: " + anchor.Id);
				}
			}
		}
		Trace.Data["offlineRoutesValidated"] = true;
	}
}
