using System.Reflection;
using System.Text.Json;
using Frontier.Engine;
using HarmonyLib;

namespace Frontier.Core;

internal static class ElitePresentation
{
    internal sealed record RankStyle(string Id, string Name, string Hex, HashSet<string> BodyTemplates);
    private static readonly Dictionary<string, RankStyle> ByBody = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, RankStyle> ByEntity = new(StringComparer.Ordinal);
    internal static int RankedEntities { get; private set; }
    internal static int VisualsTinted { get; private set; }
    internal static object Snapshot => new { rankedEntities = RankedEntities, visualsTinted = VisualsTinted, trackedLabels = LabelsTracked };
#if FRONTIER_OVERLAY
    private static int LabelsTracked => EliteWorldOverlay.TrackedCount;
#else
    private static int LabelsTracked => 0;
#endif

    internal static void Install(Harmony harmony, string packPath)
    {
        var root = PatchEngine.ReadObject(Path.Combine(packPath, "elite-presentation.json"));
        foreach (var pair in root["ranks"]!.AsObject())
        {
            var o = pair.Value!.AsObject();
            string name = o["nameRu"]!.GetValue<string>();
            string hex = o["color"]!.GetValue<string>();
            var ids = o["bodyPartTemplateIds"]!.AsArray().Select(PatchEngine.Scalar).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var style = new RankStyle(pair.Key, name, hex, ids);
            foreach (string id in ids) ByBody[id] = style;
        }
        Plugin.Current.Log.LogInfo("Elite presentation adapter: rank-visuals-v1");
        Type spawn = Reflect.FindType("Game.Character.CharacterSpawnHandlerSystem");
        harmony.Patch(Reflect.UniqueMethod(spawn, "SetupBodyParts", 7), postfix: Hook(nameof(BodyPartsPostfix)));
        harmony.Patch(Reflect.UniqueMethod(spawn, "SetupSurfaceMaterial", 3), postfix: Hook(nameof(SurfacePostfix)));
    }

    private static HarmonyMethod Hook(string name) => new(typeof(ElitePresentation).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!);

    private static void BodyPartsPostfix(object[] __args)
    {
        if (!Plugin.Current.Active || __args.Length < 3 || __args[2] is null) return;
        try
        {
            RankStyle? rank = RankFromEquipment(__args[2]);
            if (rank is null) return;
            string entity = __args[0]?.ToString() ?? "";
            if (entity.Length == 0) return;
            ByEntity[entity] = rank;
            RankedEntities++;
        }
        catch (Exception e) { Plugin.Current.Log.LogWarning("Elite rank bind failed: " + e.GetBaseException().Message); }
    }

    private static RankStyle? RankFromEquipment(object equipment)
    {
        int slots = Convert.ToInt32(Reflect.Read(equipment, "SlotsCount"));
        for (int i = 0; i < slots; i++)
        {
            object? item;
            try { item = Reflect.Call(equipment, "GetItemFromSlot", i); } catch { continue; }
            if (item is null) continue;
            string template = Reflect.Read(item, "TemplateId")?.ToString() ?? "";
            if (ByBody.TryGetValue(template, out var rank)) return rank;
        }
        return null;
    }

    private static void SurfacePostfix(object[] __args)
    {
        if (!Plugin.Current.Active || __args.Length < 2 || __args[1] is null) return;
        try
        {
            string entity = __args[0]?.ToString() ?? "";
            if (!ByEntity.TryGetValue(entity, out var rank)) return;
#if FRONTIER_OVERLAY
            object? companion = Reflect.Call(__args[1], "GetCompanion");
            if (companion is UnityEngine.GameObject root)
            {
                Tint(root, rank);
                EliteWorldOverlay.Register(root, rank);
                VisualsTinted++;
                Plugin.Current.Log.LogInfo($"Elite visual: {rank.Name} [{rank.Hex}] entity={entity}");
            }
#endif
        }
        catch (Exception e) { Plugin.Current.Log.LogWarning("Elite visual bind failed: " + e.GetBaseException().Message); }
    }

#if FRONTIER_OVERLAY
    private static void Tint(UnityEngine.GameObject root, RankStyle rank)
    {
        if (!UnityEngine.ColorUtility.TryParseHtmlString(rank.Hex, out var color)) return;
        foreach (var renderer in root.GetComponentsInChildren<UnityEngine.Renderer>(true))
        {
            var block = new UnityEngine.MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            // Blend rather than replace the whole material identity.
            var tint = UnityEngine.Color.Lerp(UnityEngine.Color.white, color, 0.38f);
            block.SetColor("_Color", tint);
            block.SetColor("_BaseColor", tint);
            renderer.SetPropertyBlock(block);
        }
    }
#endif
}
