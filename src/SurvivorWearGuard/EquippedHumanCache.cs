using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Core.Utils;
using Game.GameModes;
using Game.ItemsSystem;
using Game.ItemsSystem.ItemComponents;
using Game.WeaponSystem.ItemComponents;
using Il2CppInterop.Runtime.InteropTypes;

namespace Frontier.SurvivorWearGuard;

// Build membership exclusively from the native CURRENT SESSION's live humans
// and their installed equipment slots. Does not guess owner by distance or
// accept the broken FindOwnerPlayerId result (bot 0 vs real human 1).
internal static class EquippedHumanCache
{
    private static readonly object Gate=new();
    private static IntPtr sessionPointer;
    private static long nextRefreshAt;
    private static HashSet<string> equipped=new(StringComparer.Ordinal);
    private static int activeHumans;
    private static long refreshes, failures;
    private static string observations="";

    internal static bool Contains(BattleSession battle, UniqueId id,
      Func<BattleSession,int,bool> isHuman, Func<IItem,bool> combatGear,
      ManualLogSource? log, out string details)
    {
      lock(Gate)
      {
        var now=Environment.TickCount64;
        var pointer=((Il2CppObjectBase)battle).Pointer;
        if(pointer!=sessionPointer || now>=nextRefreshAt || now<nextRefreshAt-10000)
        {
            sessionPointer=pointer;
            nextRefreshAt=now+900;
            Rebuild(battle,isHuman,combatGear,log);
        }
        var ok=equipped.Contains(id.ToString());
        details=$"humanEquipment={(ok?"yes":"no")} humans={activeHumans} items={equipped.Count}";
        return ok;
      }
    }

    private static void Rebuild(BattleSession battle,
       Func<BattleSession,int,bool> isHuman,Func<IItem,bool> combatGear,
       ManualLogSource? log)
    {
      var next=new HashSet<string>(StringComparer.Ordinal);
      activeHumans=0;
      var note=new System.Text.StringBuilder();
      try
      {
        var players=new Il2CppSystem.Collections.Generic.List<int>();
        battle.GetHumanPlayerIds(BattleSession.Player.Statuses.Normal,players);
        if(players.Count>24)throw new InvalidOperationException("Human roster bound exceeded");
        for(int i=0;i<players.Count;i++)
        {
            int playerId=players[i];
            if(!isHuman(battle,playerId))continue;
            var equipment=battle.FindEquipmentByPlayerId(playerId);
            if(equipment==null)continue;
            activeHumans++;
            var slots=equipment._slots;
            if(slots==null||slots.Count>128)continue;
            for(int k=0;k<slots.Count;k++)
            {
                try
                {
                    var slot=slots[k];
                    var item=slot?.ContainedItem;
                    if(item==null || !combatGear(item))continue;
                    next.Add(item.Id.ToString());
                    var firearm=item.GetComponent<FirearmComponent>();
                    if(firearm!=null)
                    {
                        var condition=item.GetComponent<ConditionComponent>();
                        if(condition!=null)
                        {
                            string id=item.Id.ToString();
                            note.Append($" player={playerId} gun={id.Substring(0,Math.Min(8,id.Length))} condition={condition.Value:0.###}/{condition.MaxValue:0.###};");
                        }
                    }
                }
                catch(Exception e)
                {
                    if(++failures<=3)log?.LogWarning($"Equipped slot read failed: {e.GetType().Name}: {e.Message}");
                }
            }
        }
      }
      catch(Exception e)
      {
        if(++failures<=3)log?.LogWarning($"Human equipment cache fail-open: {e.GetType().Name}: {e.Message}");
        next.Clear();
      }
      equipped=next;
      observations=note.ToString();
      refreshes++;
      if(refreshes<=3||refreshes%5==0)
        log?.LogInfo($"WearGuard human-slot cache refresh={refreshes} humans={activeHumans} tracked={equipped.Count}; {observations}");
    }
}
