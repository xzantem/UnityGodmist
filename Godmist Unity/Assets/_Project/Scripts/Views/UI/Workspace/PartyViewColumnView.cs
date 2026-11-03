using System;
using System.Linq;
using Godmist.Core.Utilities;
using Godmist.Core.Enums;
using Godmist.Systems.Characters;
using Godmist.Runtime.Game;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Views.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
 public sealed class PartyViewColumnView : MonoBehaviour,IPointerDownHandler,IDropHandler,IPointerEnterHandler,IPointerExitHandler
 {
  [SerializeField] private CharacterInfoWindowView equipment;
  [SerializeField] private CombatUnitInspectWindowView stats;
  [SerializeField] private SkillLoadoutModalView skills;
  [SerializeField] private SkillTreeProgressionPanelView development;
  [SerializeField] private Button individualButton;
  [SerializeField] private Button[] modeButtons;
  [SerializeField] private TMP_Text identity,level,vitals,blockedReason,experience,honor,availability,resourceValue;
  private readonly CharacterProgressionSystem progression=new();
  [SerializeField] private Image portrait,selection,dropPreview,healthFill,resourceFill,experienceFill;
  [SerializeField] private GameObject resourceTrack;
  [SerializeField] private GameObject[] panels;
  private PartyViewWindowView owner;
  private GameSession session;
  private CharacterInstance character;
  private BattleUnit unit;
  private bool wasInCombat;
  private int shownLevel=-1;
  private CharacterInfoWindowView dropSource;
  public string CharacterId=>character?.CharacterId;
  public PartyViewMode Mode{get;private set;}
  public Button DefaultFocus=>modeButtons[(int)Mode];
  public CharacterInfoWindowView Equipment=>equipment;
  public void Bind(PartyViewWindowView parent,GameSession game,string id,UIWorkspaceWindow host,Action individual,Func<string,string,string> camp)
  {
   owner=parent;session=game;character=game.CharactersById[id];
   FitHeaderMetrics();
   equipment.ConfigureEmbedded(host);equipment.CampAction=camp;equipment.Initialize(session,id);
   stats.ConfigureEmbedded(host);skills.ConfigureEmbedded(host);development.ConfigureEmbedded(host);
   individualButton.onClick.AddListener(()=>individual?.Invoke());
   for(var i=0;i<modeButtons.Length;i++){var mode=(PartyViewMode)i;modeButtons[i].onClick.AddListener(()=>{owner.Select(CharacterId,false);Show(mode);});}
   Refresh();
  }
  private void FitHeaderMetrics()
  {
   const float halfGap = 10f;
   var healthBar = healthFill.GetComponentInParent<UIBarFillMask>();
   var experienceBar = experienceFill.GetComponentInParent<UIBarFillMask>();
   SetMetricHalf(vitals.rectTransform, false, halfGap);
   SetMetricHalf((RectTransform)healthBar.transform, false, halfGap);
   SetMetricHalf(resourceValue.rectTransform, true, halfGap);
   SetMetricHalf((RectTransform)resourceTrack.transform, true, halfGap);
   SetMetricHalf((RectTransform)experience.transform.parent, false, halfGap);
   SetMetricHalf((RectTransform)experienceBar.transform, false, halfGap);
   SetMetricHalf((RectTransform)honor.transform.parent, true, halfGap);
  }

  private static void SetMetricHalf(RectTransform rect, bool right, float halfGap)
  {
   if(right)
   {
    rect.anchorMin = new Vector2(.5f, rect.anchorMin.y);
    rect.offsetMin = new Vector2(halfGap, rect.offsetMin.y);
   }
   else
   {
    rect.anchorMax = new Vector2(.5f, rect.anchorMax.y);
    rect.offsetMax = new Vector2(-halfGap, rect.offsetMax.y);
   }
  }
  public void Refresh()
  {
   if(character==null)return;
   identity.text=character.Definition?.DisplayName??CharacterId;
   level.text=$"{UiTerms.CharacterClass(character.ClassData?.ClassName)} • {UiTerms.Level} {character.Level}";
   portrait.sprite=character.Definition?.UiPortraitSprite??character.Definition?.VisualSprite;
   unit=session.Combats.SelectMany(c=>c.Units).FirstOrDefault(u=>u.Character==character)??new BattleUnit{Character=character,IsPlayerSide=true};
   UpdateVitals();
   UpdateLevel();
  }

  private void UpdateLevel()
  {
   if(shownLevel!=character.Level){shownLevel=character.Level;level.text=$"{UiTerms.CharacterClass(character.ClassData?.ClassName)} • {UiTerms.Level} {character.Level}";}
  }
  private void UpdateVitals()
  {
   experience.text=CharacterOverviewPresentation.Experience(character,progression);
   honor.text=character.Honor.ToString();
   availability.text=$"{UiTerms.LifeState(character.LifeState)} • {UiTerms.Availability(character.AvailabilityState)}"
       +(character.ShieldPool?.TotalShield>0?$" • {UiTerms.Shield}: {character.ShieldPool.TotalShield}":"");
   healthFill.fillAmount=character.MaximumHealth>0?Mathf.Clamp01((float)character.CurrentHealth/character.MaximumHealth):0;
   var resource=character.ResourcePool;
   resourceTrack.SetActive(resource!=null);
   resourceValue.gameObject.SetActive(resource!=null);
   resourceFill.color=resource==null?Color.clear:CombatResourceColors.Get(resource.ResourceType);
   resourceFill.fillAmount=resource?.MaxValue>0?Mathf.Clamp01((float)resource.CurrentValue/resource.MaxValue):0;
   resourceValue.text=resource==null?"":$"{UiTerms.Resource(resource.ResourceType)} {resource.CurrentValue}/{resource.MaxValue}";
   var required=progression.GetExperienceRequiredForNextLevel(character.Level);
   experienceFill.fillAmount=character.Level>=CharacterProgressionSystem.MaximumLevel?1:required>0?Mathf.Clamp01((float)character.CurrentExperience/required):0;
   vitals.text=$"{UiTerms.HealthShort} {character.CurrentHealth}/{character.MaximumHealth}";
  }
  public void Show(PartyViewMode mode)
  {
   if(character==null)return;
   UIWorkspaceTooltipView.ActiveInstance?.HideForSourceWithin(transform);
   Mode=mode;wasInCombat=session.IsCharacterInCombat(CharacterId);
   var blocked=wasInCombat&&(mode==PartyViewMode.Skills||mode==PartyViewMode.Development);
   for(var i=0;i<panels.Length;i++)panels[i].SetActive(!blocked&&i==(int)mode);
   blockedReason.gameObject.SetActive(blocked);
   blockedReason.text=blocked?"Zmiana umiejętności i rozwój są niedostępne podczas walki.":"";
   for(var i=0;i<modeButtons.Length;i++)
   {
    var colors=modeButtons[i].colors;colors.normalColor=i==(int)mode?new Color(1.15f,1.08f,.78f):Color.white;modeButtons[i].colors=colors;
   }
   if(blocked)return;
   switch(mode){case PartyViewMode.Equipment:equipment.Initialize(session,CharacterId);break;case PartyViewMode.Stats:stats.Show(unit);break;case PartyViewMode.Skills:skills.Open(session,CharacterId);break;case PartyViewMode.Development:development.Open(session,CharacterId);break;}
  }
  public void MarkSelected(bool active){selection.enabled=active;}
  private void LateUpdate()
  {
   if(character==null)return;UpdateVitals();UpdateLevel();
   if(dropSource!=null)
   {
    var item=dropSource.DraggedItem;
    if(item==null){dropSource=null;dropPreview.enabled=false;}
    else {dropPreview.enabled=true;dropPreview.color=dropSource.CanPreviewRecipientDrop(session,CharacterId,item,out _)?HighlightStyle.Friendly:HighlightStyle.Hostile;}
   }
   if(wasInCombat!=session.IsCharacterInCombat(CharacterId)){Refresh();Show(Mode);}
  }
  public void OnPointerDown(PointerEventData e){owner?.Window.Focus();owner?.Select(CharacterId,false);}
  private static CharacterInfoWindowView Source(PointerEventData e)=>e?.pointerDrag!=null?e.pointerDrag.GetComponent<CharacterInfoItemView>()?.Owner??e.pointerDrag.GetComponent<CharacterInfoEquipmentSlotView>()?.Owner:null;
  public void OnDrop(PointerEventData e)
  {
   dropSource=null;dropPreview.enabled=false;if(e?.button!=PointerEventData.InputButton.Left)return;
   var source=Source(e);var item=e.pointerDrag?.GetComponent<CharacterInfoItemView>()?.Item??e.pointerDrag?.GetComponent<CharacterInfoEquipmentSlotView>()?.Item;
   if(source!=null&&item!=null)equipment.TryReceiveInventoryTransfer(source,item);
  }
  public void OnPointerEnter(PointerEventData e)
  {
   var source=Source(e);var item=e?.pointerDrag?.GetComponent<CharacterInfoItemView>()?.Item??e?.pointerDrag?.GetComponent<CharacterInfoEquipmentSlotView>()?.Item;
   if(source==null||item==null)return;
   dropSource=source;var valid=source.CanPreviewRecipientDrop(session,CharacterId,item,out _);dropPreview.enabled=true;dropPreview.color=valid?HighlightStyle.Friendly:HighlightStyle.Hostile;
  }
  public void OnPointerExit(PointerEventData e){dropSource=null;dropPreview.enabled=false;}
  private void OnDisable(){dropSource=null;if(dropPreview!=null)dropPreview.enabled=false;}
 }
}
