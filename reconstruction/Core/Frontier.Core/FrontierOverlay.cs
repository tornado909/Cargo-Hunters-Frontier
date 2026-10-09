using System;
using UnityEngine;

namespace Frontier.Core;

public sealed class FrontierOverlay : MonoBehaviour
{
	private bool visible = true;

	private bool inputDisabled;

	private float lastReport;

	public FrontierOverlay(System.IntPtr pointer)
		: base(pointer)
	{
	}

	public void Update()
	{
		if (!inputDisabled)
		{
			try
			{
				if (Input.GetKeyDown((KeyCode)289))
				{
					visible = !visible;
				}
			}
			catch
			{
				inputDisabled = true;
				visible = false;
			}
		}
		if (Time.unscaledTime - lastReport > 15f)
		{
			lastReport = Time.unscaledTime;
			Plugin.Current.WriteReport();
		}
	}

	public void OnGUI()
	{
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		if (visible)
		{
			Plugin current = Plugin.Current;
			string[] obj = new string[21]
			{
				"FRONTIER 0.3.0 RUNTIME RC1 [F8]\n", current.Mode, "\nNative read-back: ", null, null, null, null, null, null, null,
				null, null, null, null, null, null, null, null, null, null,
				null
			};
			RuntimeAudit audit = current.Audit;
			obj[3] = ((audit != null && audit.CatalogReady) ? "VERIFIED" : "PENDING/BROKEN");
			obj[4] = " / repositories ";
			obj[5] = current.Repositories.Count.ToString();
			obj[6] = "/9  |  Elite selections: ";
			obj[7] = current.EnemyChanges.ToString();
			obj[8] = "\nCo-op: ";
			obj[9] = current.Coop?.Status ?? "initializing";
			obj[10] = " [";
			obj[11] = (current.Coop?.ApprovedMembers ?? 0).ToString();
			obj[12] = "/";
			obj[13] = (current.Coop?.MemberCount ?? 0).ToString();
			obj[14] = "]\nSignature: ";
			obj[15] = current.Coop?.Signature.Substring(0, 12) ?? "pending";
			obj[16] = "\nText hits: ";
			obj[17] = current.LocalizationHits.ToString();
			obj[18] = "\n";
			obj[19] = current.LastEnemy;
			obj[20] = "\nDetails: BepInEx/Frontier-report.json";
			string text = string.Concat(obj);
			GUI.Box(new Rect(12f, 12f, (float)Math.Min(Screen.width - 24, 820), 190f), text);
		}
	}
}
