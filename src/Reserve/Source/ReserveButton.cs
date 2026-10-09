#nullable enable
using System;
using Game.GlobalMap.UI;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
namespace Frontier.Reserve;
// An independent menu control. It never takes over the Unknown map's callbacks.
internal sealed class ReserveButton:IDisposable
{
 private readonly GameObject root,captionRoot;private readonly TextMeshProUGUI text,caption;
 private readonly Button button;private readonly UnityAction click;private readonly NativeRoot clickRoot;
 private (bool,bool,bool,bool)? lastState;
 internal bool Alive=>root!=null&&root;
 internal ReserveButton(GlobalMapFakeController owner,Action clicked)
 {
  var parent=owner.View.transform;
  root=new GameObject("Frontier.Reserve.DirectDeploy",new Il2CppReferenceArray<Il2CppSystem.Type>(new[]{Il2CppType.Of<RectTransform>(),Il2CppType.Of<CanvasRenderer>(),Il2CppType.Of<Image>(),Il2CppType.Of<Button>()}));
  var rect=root.GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=Vector2.zero;rect.anchoredPosition=new Vector2(36,48);rect.sizeDelta=new Vector2(260,44);
  var bg=root.GetComponent<Image>();bg.color=new Color(.18f,.085f,.008f,.96f);bg.raycastTarget=true;
  var border=root.AddComponent<Outline>();border.effectColor=new Color(1,.56f,0,1);border.effectDistance=new Vector2(1,-1);
  button=root.GetComponent<Button>();button.targetGraphic=bg;
  var colors=button.colors;colors.m_NormalColor=Color.white;colors.m_HighlightedColor=new Color(1,.8f,.45f,1);colors.m_PressedColor=new Color(.72f,.52f,.18f,1);colors.m_DisabledColor=new Color(.55f,.55f,.55f,.75f);colors.colorMultiplier=1;colors.fadeDuration=.08f;button.colors=colors;
  text=Label(root.transform,"Label",owner,20);var tr=text.GetComponent<RectTransform>();tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=new Vector2(8,0);tr.offsetMax=new Vector2(-8,0);text.alignment=TextAlignmentOptions.Center;text.color=new Color(1,.62f,.12f,1);text.text="РЕЗЕРВ";
  captionRoot=new GameObject("Frontier.Reserve.MenuStatus",new Il2CppReferenceArray<Il2CppSystem.Type>(new[]{Il2CppType.Of<RectTransform>()}));
  var cr=captionRoot.GetComponent<RectTransform>();cr.SetParent(parent,false);cr.anchorMin=cr.anchorMax=Vector2.zero;cr.pivot=Vector2.zero;cr.anchoredPosition=new Vector2(36,96);cr.sizeDelta=new Vector2(500,30);
  caption=Label(captionRoot.transform,"Status",owner,14);var sr=caption.GetComponent<RectTransform>();sr.anchorMin=Vector2.zero;sr.anchorMax=Vector2.one;sr.offsetMin=sr.offsetMax=Vector2.zero;caption.alignment=TextAlignmentOptions.MidlineLeft;caption.color=new Color(.94f,.88f,.74f,1);caption.text="Подготовка локации…";
  click=(UnityAction)clicked;clickRoot=new NativeRoot(click);button.onClick=new Button.ButtonClickedEvent();button.onClick.AddListener(click);
 }
 private static TextMeshProUGUI Label(Transform parent,string name,GlobalMapFakeController owner,float size)
 {
  var go=new GameObject(name,new Il2CppReferenceArray<Il2CppSystem.Type>(new[]{Il2CppType.Of<RectTransform>(),Il2CppType.Of<CanvasRenderer>(),Il2CppType.Of<TextMeshProUGUI>()}));go.transform.SetParent(parent,false);
  var label=go.GetComponent<TextMeshProUGUI>();var font=owner._unitsCount?.font;
  if(font==null&&owner._closeButton?._nameLabels!=null&&owner._closeButton._nameLabels.Length>0)font=owner._closeButton._nameLabels[0]?.font;
  if(font!=null)label.font=font;label.fontSize=size;label.raycastTarget=false;label.enableWordWrapping=false;return label;
 }
 internal void State(bool visible,bool ready,bool deploying,bool blocked)
 {
  if(!Alive)return;var state=(visible,ready,deploying,blocked);if(lastState==state)return;lastState=state;root.SetActive(visible);captionRoot.SetActive(visible);button.interactable=!deploying;
  text.text=deploying?"ОТПРАВКА…":"РЕЗЕРВ";
  caption.text=deploying?"Подготовка к рейду…":blocked?"Не удалось подготовить локацию.":ready?"Подземный комплекс · 30 минут":"Подготовка локации…";
 }
 public void Dispose(){if(root!=null)UnityEngine.Object.Destroy(root);if(captionRoot!=null)UnityEngine.Object.Destroy(captionRoot);clickRoot.Dispose();}
}
