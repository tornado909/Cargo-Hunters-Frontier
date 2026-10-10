#if FRONTIER_OVERLAY
using UnityEngine;

namespace Frontier.Core;

public sealed class EliteWorldOverlay : MonoBehaviour
{
    private sealed record Mark(GameObject Root, ElitePresentation.RankStyle Rank);
    private static readonly Dictionary<int, Mark> Marks = new();
    private GUIStyle? style;
    internal static int TrackedCount => Marks.Count;

    internal static void Register(GameObject root, ElitePresentation.RankStyle rank)
    {
        if (root == null) return;
        Marks[root.GetInstanceID()] = new Mark(root, rank);
    }

    public void OnGUI()
    {
        Camera? cam = Camera.main;
        if (cam == null) return;
        style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 15, fontStyle = FontStyle.Bold };
        foreach (int key in Marks.Keys.ToArray())
        {
            Mark mark = Marks[key];
            if (mark.Root == null) { Marks.Remove(key); continue; }
            Vector3 world = mark.Root.transform.position + Vector3.up * 2.25f;
            if (Vector3.Distance(cam.transform.position, world) > 55f) continue;
            Vector3 p = cam.WorldToScreenPoint(world);
            if (p.z <= 0f || p.x < -100f || p.x > Screen.width + 100f || p.y < -50f || p.y > Screen.height + 100f) continue;
            if (!ColorUtility.TryParseHtmlString(mark.Rank.Hex, out var c)) c = Color.white;
            style.normal.textColor = c;
            GUI.Label(new Rect(p.x - 90f, Screen.height - p.y - 18f, 180f, 24f), mark.Rank.Name, style);
        }
    }
}
#endif
