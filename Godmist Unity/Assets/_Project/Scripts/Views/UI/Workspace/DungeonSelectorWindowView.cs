using System.Linq;
using Godmist.Data.Dungeons;
using Godmist.Systems.Dungeons;
using Godmist.Views.Game;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace Godmist.Views.UI.Workspace
{
    public sealed class DungeonSelectorWindowView : MonoBehaviour
    {
        [SerializeField] private UIWorkspaceWindow window;
        [SerializeField] private DungeonDefinition[] definitionPool = new DungeonDefinition[0];
        [SerializeField] private Button[] offerButtons;
        [SerializeField] private TMP_Text[] offerLabels;
        [SerializeField] private Image[] offerBorders;
        [SerializeField] private TMP_Text details, partySummary, departLabel, abandonLabel;
        [SerializeField] private TMP_Text selectionHeading, selectionState;
        [SerializeField] private Button depart, abandon;
        [SerializeField] private Transform partyRoot;
        [SerializeField] private Godmist.Views.UI.CombatPartyMemberView partyTemplate;
        [SerializeField] private Color selectedColor = new(.85f,.65f,.25f);
        [SerializeField] private Color normalColor = new(.24f,.32f,.36f);
        private readonly System.Collections.Generic.List<Godmist.Views.UI.CombatPartyMemberView> portraits = new();
        private GameBootstrap bootstrap;
        private DungeonDefinition fallback;
        private bool confirmAbandon, busy;
        private string errorMessage = "";
        private float nextRefresh;
        public bool IsOpen => window != null && window.IsOpen;
        private void Awake()
        {
            for(int i=0;i<offerButtons.Length;i++) { var slot=i; offerButtons[i].onClick.AddListener(()=>Select(slot)); }
            depart.onClick.AddListener(Depart); abandon.onClick.AddListener(Abandon);
        }
        public void Open(GameBootstrap owner, DungeonDefinition entryDefinition)
        {
            bootstrap=owner; fallback=entryDefinition; confirmAbandon=false; busy=false; errorMessage="";
            window.Open(); Refresh();
        }
        private void Update()
        {
            if(bootstrap==null || !IsOpen) return;
            if(bootstrap.Session==null || bootstrap.Session.HasCombat) { window.Close(); return; }
            if(Time.unscaledTime>=nextRefresh) { nextRefresh=Time.unscaledTime+.2f; Refresh(); }
        }
        private void Select(int index)
        {
            if(bootstrap.Session.ActiveDungeon!=null || busy) return;
            bootstrap.Session.DungeonOffers.TrySelect(index); confirmAbandon=false; errorMessage=""; Refresh();
        }
        private void Refresh()
        {
            var session=bootstrap.Session;
            // The entrance's serialized definition may be a legacy non-offer dungeon.
            // Production expeditions are registered in the shared content catalog.
            System.Collections.Generic.IEnumerable<DungeonDefinition> pool=
                bootstrap.ContentCatalog!=null
                    ? bootstrap.ContentCatalog.DungeonDefinitions
                    : Enumerable.Empty<DungeonDefinition>();
            if(definitionPool!=null) pool=pool.Concat(definitionPool);
            if(fallback!=null) pool=pool.Append(fallback);
            if(session.ActiveDungeon==null) DungeonOfferSystem.EnsureOffers(session,pool);
            var board=session.DungeonOffers; var average=DungeonOfferSystem.PartyLevel(session);
            var active=session.ActiveDungeon;
            for(int i=0;i<offerButtons.Length;i++)
            {
                var exists=i<board.Offers.Count;
                offerButtons[i].gameObject.SetActive(active==null && exists);
                if(!exists) continue;
                var offer=board.Offers[i];var definition=Resolve(offer.DefinitionId);
                offerLabels[i].text=OfferCaption(definition, offer.Length, DungeonOfferSystem.OfferLevel(average,offer));
                var palette=offerButtons[i].GetComponent<UIWorkspaceStyleApplier>()?.Palette;
                if(palette!=null && palette.IsArtwork(offerBorders[i].sprite))
                {
                    palette.ApplyFrameAccent(offerBorders[i],i==board.SelectedIndex ? UIWorkspaceAccentStyle.Selected : UIWorkspaceAccentStyle.Neutral);
                    var selectedMarker = i == board.SelectedIndex;
                    if (offerButtons[i].targetGraphic is Image background)
                        background.sprite = palette.GetArtwork(selectedMarker ? "button_selected" : "button_normal") ?? background.sprite;
                    offerLabels[i].color = selectedMarker ? palette.GetAccentColor(UIWorkspaceAccentStyle.Selected)
                        : palette.GetTextColor(UIWorkspaceTextStyle.Body);
                }
                else offerBorders[i].color=i==board.SelectedIndex ? selectedColor : normalColor;
                offerButtons[i].interactable=active==null && definition!=null && !busy;
            }
            var selected=board.Offers.Count>board.SelectedIndex ? board.Offers[board.SelectedIndex] : null;
            var chosen=active!=null ? active.Definition ?? Resolve(active.DungeonId) : selected==null ? null : Resolve(selected.DefinitionId);
            var level=active!=null ? active.DungeonLevel : selected==null ? 0 : DungeonOfferSystem.OfferLevel(average,selected);
            var difference=level-average;
            var relativeLevel=difference>0 ? $"+{difference}" : difference.ToString();
            var heading = chosen == null ? "Brak dostępnych wypraw" : chosen.DisplayName;
            details.text = chosen == null ? "" : $"Poziom: {level}\nDrużyna: {average} · różnica {relativeLevel}";
            var length = active?.Length ?? selected?.Length ?? Godmist.Core.Enums.DungeonLength.Unspecified;
            if (chosen != null && length != Godmist.Core.Enums.DungeonLength.Unspecified)
                details.text += $"\n{DungeonLengthPresentation.Name(length)} · Zestawy obozowe: {(int)length-1} · Quest ukończenia ×{(int)length}";
            if (selectionHeading != null) selectionHeading.text = heading;
            else details.text = heading + "\n\n" + details.text;
            if(chosen!=null && chosen.Theme!=Godmist.Core.Enums.DungeonTheme.Story)
            {
                var danger=board.GetDanger(chosen.Theme);
                details.text+=$"\n\nZagrożenie: {danger}% · {DangerLabel(danger)}";
            }
            var state = !string.IsNullOrEmpty(errorMessage) ? errorMessage : active != null ? "Wyprawa w toku" : "";
            if (selectionState != null) selectionState.text = state;
            else if (!string.IsNullOrEmpty(state)) details.text += "\n\n" + state;
            departLabel.text=active==null ? "Wyrusz" : "Wznów";
            depart.interactable=!busy && chosen!=null && session.PlayerParty.ActiveMemberIds.Count>0;
            abandon.gameObject.SetActive(active!=null);abandon.interactable=!busy;
            abandonLabel.text=confirmAbandon ? "Potwierdź porzucenie" : "Porzuć wyprawę";
            partySummary.text=$"Aktywna drużyna · średni poziom: {average}";
            int count=0;
            foreach(var id in session.PlayerParty.ActiveMemberIds)
            {
                if(!session.CharactersById.TryGetValue(id,out var character)) continue;
                if(count==portraits.Count) portraits.Add(Instantiate(partyTemplate,partyRoot));
                portraits[count].gameObject.SetActive(true);portraits[count].BindCharacter(character,id==session.PlayerParty.ActiveLeaderId);count++;
            }
            for(int i=count;i<portraits.Count;i++) portraits[i].gameObject.SetActive(false);
        }
        private DungeonDefinition Resolve(string id)
        {
            var configured=definitionPool.FirstOrDefault(d=>d!=null && d.Id==id);
            if(configured!=null) return configured;
            if(bootstrap.ContentCatalog!=null && bootstrap.ContentCatalog.TryGetDungeon(id,out var found)) return found;
            return fallback!=null && fallback.Id==id ? fallback : null;
        }
        public static string OfferCaption(DungeonDefinition definition, Godmist.Core.Enums.DungeonLength length, int level)
            => $"{(definition != null ? MarkerName(definition) : "Niedostępny loch")}\n{DungeonLengthPresentation.Name(length)} · Poz. {level}";

        private static string MarkerName(DungeonDefinition definition) => definition.IsStoryDungeon
            ? definition.DisplayName : ThemeName(definition.Theme) + (definition.BossEncounterDefinition != null ? " · Boss" : "");
        private static string DangerLabel(int danger) => DungeonOfferSystem.DangerBand(danger) switch
        {
            "Low" => "Niskie", "Medium" => "Średnie", "High" => "Wysokie", _ => "Maksymalne"
        };
        private static string ThemeName(Godmist.Core.Enums.DungeonTheme theme) => theme switch
        {
            Godmist.Core.Enums.DungeonTheme.Catacombs => "Katakumby",
            Godmist.Core.Enums.DungeonTheme.Forest => "Las",
            Godmist.Core.Enums.DungeonTheme.ElvenRuins => "Elfie ruiny",
            Godmist.Core.Enums.DungeonTheme.Docks => "Doki",
            Godmist.Core.Enums.DungeonTheme.Desert => "Pustynia",
            Godmist.Core.Enums.DungeonTheme.Temple => "Świątynia",
            Godmist.Core.Enums.DungeonTheme.Mountains => "Góry",
            Godmist.Core.Enums.DungeonTheme.Swamp => "Bagna",
            _ => "Wyprawa fabularna"
        };
        private void Depart()
        {
            if(busy) return;busy=true;
            var session=bootstrap.Session;var active=session.ActiveDungeon;
            bool success=active!=null ? bootstrap.TryEnterDungeon(active.Definition ?? Resolve(active.DungeonId),active.Seed)
                : bootstrap.TryEnterDungeonOffer(session.DungeonOffers.SelectedIndex, Resolve(session.DungeonOffers.Offers[session.DungeonOffers.SelectedIndex].DefinitionId));
            if(success) window.Close();else { busy=false;errorMessage="Nie udało się rozpocząć wyprawy.";Refresh(); }
        }
        private void Abandon()
        {
            if(busy) return;
            if(!confirmAbandon) { confirmAbandon=true;Refresh();return; }
            if(bootstrap.TryAbandonDungeon()) { confirmAbandon=false;Refresh(); }
        }
        private void OnDisable() { confirmAbandon=false; }
        private void OnDestroy()
        {
            foreach(var button in offerButtons) if(button!=null) button.onClick.RemoveAllListeners();
            if(depart!=null) depart.onClick.RemoveListener(Depart);
            if(abandon!=null) abandon.onClick.RemoveListener(Abandon);
        }
    }
}
