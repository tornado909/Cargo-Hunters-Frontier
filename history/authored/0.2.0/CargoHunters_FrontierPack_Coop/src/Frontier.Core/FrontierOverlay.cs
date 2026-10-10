using UnityEngine;
namespace Frontier.Core;
/// <summary>Optional read-only diagnostic overlay. It is not a custom inventory or a world-space enemy label.</summary>
public sealed class FrontierOverlay : MonoBehaviour
{
    private bool visible = true;
    private bool inputDisabled;
    private float lastReport;
    public FrontierOverlay(IntPtr pointer) : base(pointer) { }
    public void Update()
    {
        // Some Unity builds disable legacy Input. Keep the overlay optional instead of flooding exceptions.
        if (!inputDisabled)
        {
            try { if (Input.GetKeyDown(KeyCode.F8)) visible = !visible; }
            catch { inputDisabled = true; visible = false; }
        }
        if (Time.unscaledTime - lastReport > 15f) { lastReport = Time.unscaledTime; Plugin.Current.WriteReport(); }
    }
    public void OnGUI()
    {
        if (!visible) return;
        var p = Plugin.Current;
        string text = "FRONTIER 0.2.0 CO-OP RC1 [F8]\n" + p.Mode + "\n" +
            "Repositories: " + p.Repositories.Count + "/9  |  Elite selections: " + p.EnemyChanges + "\n" +
            "Co-op: " + (p.Coop?.Status ?? "initializing") + " [" + (p.Coop?.ApprovedMembers ?? 0) + "/" + (p.Coop?.MemberCount ?? 0) + "]\n" +
            "Signature: " + (p.Coop?.Signature[..12] ?? "pending") + "\n" +
            "Text hits: " + p.LocalizationHits + "\n" + p.LastEnemy + "\n" +
            "Details: BepInEx/Frontier-report.json";
        GUI.Box(new Rect(12, 12, Math.Min(Screen.width - 24, 820), 190), text);
    }
}
