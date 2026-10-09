#nullable enable
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using Frontier.Loot;
namespace Frontier.Survivor;

internal static class SurvivorHud
{
 static GameObject? root,choices;
 static TextMeshProUGUI? status,heading;
 static readonly TextMeshProUGUI?[] labels=new TextMeshProUGUI?[3];
 static Image? xpFill;
 static readonly LeaseBag leases=new();
 static string run="",last="";
 static GameObject Node(string name,Transform parent,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size)
 {
  var go=new GameObject(name);var r=go.AddComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=anchor;r.pivot=pivot;r.anchoredPosition=position;r.sizeDelta=size;return go;
 }
 static TextMeshProUGUI Text(string name,Transform parent,Vector2 pos,Vector2 size,float fontSize)
 {
  var go=Node(name,parent,new Vector2(0,1),new Vector2(0,1),pos,size);var text=go.AddComponent<TextMeshProUGUI>();text.font=Menu.Font;text.fontSize=fontSize;text.color=new Color(1,.65f,.16f);text.enableWordWrapping=true;text.raycastTarget=false;return text;
 }
 static void Build()
 {
  run=SurvivorRun.RunId;root=new GameObject("Frontier.Survivor.HUD");root.AddComponent<RectTransform>();var canvas=root.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=75;
  var scaler=root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;root.AddComponent<GraphicRaycaster>();
  UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,Loader.Scene);
  var panel=Node("Run status",root.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(40,-98),new Vector2(490,105));
  var bg=panel.AddComponent<Image>();bg.color=new Color(.02f,.025f,.03f,.8f);bg.raycastTarget=false;
  status=Text("Run level XP",panel.transform,new Vector2(12,-6),new Vector2(466,80),19);
  var bar=Node("XP bar",panel.transform,new Vector2(0,0),Vector2.zero,new Vector2(12,8),new Vector2(466,5));var empty=bar.AddComponent<Image>();empty.color=new Color(.18f,.20f,.22f,.95f);empty.raycastTarget=false;
  var fill=Node("XP",bar.transform,Vector2.zero,Vector2.zero,Vector2.zero,new Vector2(466,5));xpFill=fill.AddComponent<Image>();xpFill.color=new Color(.98f,.57f,.04f);xpFill.raycastTarget=false;
  choices=Node("Upgrade selection",root.transform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,205),new Vector2(960,132));
  heading=Text("Choose upgrade",choices.transform,new Vector2(0,0),new Vector2(960,30),20);heading.alignment=TextAlignmentOptions.Center;
  for(int i=0;i<3;i++)
  {
   int index=i;var card=Node("Upgrade "+i,choices.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(i*320,-34),new Vector2(310,94));var image=card.AddComponent<Image>();image.color=new Color(.055f,.065f,.07f,.95f);
   var button=card.AddComponent<Button>();button.targetGraphic=image;
   var action=leases.Keep((UnityAction)(Action)(()=>{if(run==SurvivorRun.RunId)SurvivorRun.Choose(index);}));button.onClick.AddListener(action);
   labels[i]=Text("Bonus",card.transform,new Vector2(12,-8),new Vector2(286,80),18);
  }
  last="";
 }
 internal static void Tick()
 {
  if(!SurvivorRun.Active||!Loader.Ready){if(root!=null)Clear();return;}
  if(root==null){if(Menu.Font==null)return;Build();}
  if(run!=SurvivorRun.RunId){Clear();return;}
  var p=SurvivorRun.Progress;
  if(p.PendingChoices>0)
  {
   var keyboard=Keyboard.current;
   if(keyboard!=null){
    if(keyboard.f1Key.wasPressedThisFrame)SurvivorRun.Choose(0);
    else if(keyboard.f2Key.wasPressedThisFrame)SurvivorRun.Choose(1);
    else if(keyboard.f3Key.wasPressedThisFrame)SurvivorRun.Choose(2);
   }
  }
  int elapsed=(int)SurvivorRun.Seconds;
  string text=$"ВЫЖИВАНИЕ   {elapsed/60:00}:{elapsed%60:00}   ВРАГИ: {RankPolicy.Name(SurvivorRun.Rank)}\nУРОВЕНЬ {p.Level}   ОПЫТ {p.Experience}/{p.Required}   УБИЙСТВА {p.Kills}\nБОЕЗАПАС НЕ РАСХОДУЕТСЯ";
  if(text!=last){status!.text=text;last=text;xpFill!.rectTransform.sizeDelta=new Vector2(466f*p.Experience/p.Required,5);}
  choices!.SetActive(p.PendingChoices>0);
  if(p.PendingChoices>0)
  {
   heading!.text="ПОВЫШЕНИЕ УРОВНЯ — ВЫБЕРИТЕ БОНУС  [F1–F3]";
   for(int i=0;i<3;i++)
   {
    labels[i]!.transform.parent.gameObject.SetActive(i<p.Offer.Length);
    if(i<p.Offer.Length){int id=p.Offer[i];labels[i]!.text=$"[F{i+1}]  {RunProgress.Names[id]}\n{RunProgress.Descriptions[id]}\nТолько этот забег · {p.Upgrades[id]+1}/{RunProgress.Caps[id]}";}
   }
  }
 }
 internal static void Clear(){if(root!=null)UnityEngine.Object.Destroy(root);root=choices=null;status=heading=null;xpFill=null;for(int i=0;i<3;i++)labels[i]=null;leases.Dispose();run=last="";}
}
