using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Godmist.Runtime.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
 public enum PartyViewMode { Equipment, Stats, Skills, Development }
 /// <summary>Presentation selection never changes the campaign leader or combat actor.</summary>
 public sealed class PartyViewWindowView : MonoBehaviour
 {
  public const float MinimumColumnWidth=440;
  [SerializeField] private UIWorkspaceWindow window;
  [SerializeField] private ScrollRect memberScroll;
  [SerializeField] private PartyViewColumnView columnTemplate;
  [SerializeField] private TMP_Text goldText;
  private int shownGold=int.MinValue;
  private readonly List<PartyViewColumnView> columns=new();
  private readonly Dictionary<string,PartyViewMode> modes=new();
  private GameSession session;
  private Action<string> openIndividual;
  private Func<string,string,string> camp;
  private string selectedId;
  private Vector2 workspaceSize;
  public IReadOnlyList<PartyViewColumnView> Columns=>columns;
  public string SelectedCharacterId=>selectedId;
  public UIWorkspaceWindow Window=>window;
  private void Awake()
  {
   if(goldText==null)return;
   var metric=goldText.transform.parent;
   if(metric.TryGetComponent<Image>(out var background))background.color=Color.clear;
   var frame=metric.Find("GoldFrame");
   if(frame!=null)frame.gameObject.SetActive(false);
  }
  public void Open(GameSession value,Action<string> individual,Func<string,string,string> campAction)
  {
   if(session!=value){Unsubscribe();Clear();modes.Clear();session=value;if(session!=null)session.PartyRosterChanged+=OnRosterChanged;}
   openIndividual=individual;camp=campAction;
   gameObject.SetActive(true);
   workspaceSize=Vector2.zero;ResizeToWorkspace();
   Rebuild();window.Focus();Fit();if(ControllerInput.Active)Select(selectedId,true);
  }
  public void Close()=>window.Close();
  private void OnRosterChanged(){if(isActiveAndEnabled)Rebuild();}
  private void Unsubscribe(){if(session!=null)session.PartyRosterChanged-=OnRosterChanged;}
  private void OnDestroy(){Unsubscribe();}
  private void Clear(){foreach(var c in columns)if(c!=null){c.gameObject.SetActive(false);Destroy(c.gameObject);}columns.Clear();}
  private void Rebuild()
  {
   if(session==null)return;
   var ids=session.PlayerParty.ActiveMemberIds.Where(id=>session.CharactersById.ContainsKey(id)).ToArray();
   foreach(var c in columns)modes[c.CharacterId]=c.Mode;
   if(!columns.Select(c=>c.CharacterId).SequenceEqual(ids))
   {
    Clear();
    foreach(var id in ids){var c=Instantiate(columnTemplate,memberScroll.content);c.gameObject.SetActive(true);c.Bind(this,session,id,window,()=>openIndividual?.Invoke(id),camp);columns.Add(c);c.Show(modes.GetValueOrDefault(id));}
   }
   else foreach(var c in columns){c.Refresh();c.Show(c.Mode);}
   if(!ids.Contains(selectedId))selectedId=ids.FirstOrDefault();
   Select(selectedId,false);Fit();
  }
  private void ResizeToWorkspace()
  {
   if(window==null||transform.parent is not RectTransform area||area.rect.size==workspaceSize)return;
   workspaceSize=area.rect.size;
   window.RectTransform.sizeDelta=new Vector2(Mathf.Max(680,workspaceSize.x-32),Mathf.Max(600,workspaceSize.y-32));
   window.RectTransform.anchoredPosition=Vector2.zero;
  }
  private void OnRectTransformDimensionsChange(){ResizeToWorkspace();if(memberScroll!=null)Fit();}
  private void Fit()
  {
   if(memberScroll==null||columns.Count==0)return;
   var width=Mathf.Max(memberScroll.viewport.rect.width,columns.Count*MinimumColumnWidth+(columns.Count-1)*10);
   memberScroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,width);
  }
  public void Select(string id,bool focus)
  {
   if(!columns.Any(c=>c.CharacterId==id))return;selectedId=id;
   foreach(var c in columns)c.MarkSelected(c.CharacterId==id);
   var column=columns.First(c=>c.CharacterId==id);
   if(focus){EventSystem.current?.SetSelectedGameObject(column.DefaultFocus.gameObject);Reveal(column);}
  }
  public void CycleColumn(int direction)
  {
   if(columns.Count==0)return;var i=columns.FindIndex(c=>c.CharacterId==selectedId);
   Select(columns[(i+Math.Sign(direction)+columns.Count)%columns.Count].CharacterId,true);
  }
  private void Reveal(PartyViewColumnView column)
  {
   Canvas.ForceUpdateCanvases();
   var viewport=memberScroll.viewport;var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(viewport,column.transform);
   var delta=bounds.min.x<viewport.rect.xMin?viewport.rect.xMin-bounds.min.x:bounds.max.x>viewport.rect.xMax?viewport.rect.xMax-bounds.max.x:0;
   memberScroll.content.anchoredPosition+=new Vector2(delta,0);
  }
  private void LateUpdate()
  {
   ResizeToWorkspace();
   if(session!=null&&goldText!=null&&shownGold!=session.Gold){shownGold=session.Gold;goldText.text=shownGold.ToString();}
   var selected=EventSystem.current?.currentSelectedGameObject;
   var column=selected!=null?selected.GetComponentInParent<PartyViewColumnView>():null;
   if(column!=null&&columns.Contains(column)&&selectedId!=column.CharacterId)Select(column.CharacterId,false);
  }
 }
}
