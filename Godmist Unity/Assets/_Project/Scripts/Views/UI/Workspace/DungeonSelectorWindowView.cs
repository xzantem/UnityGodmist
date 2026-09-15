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
            if(bootstrap==null) return;
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
            var pool=definitionPool.Length>0 ? definitionPool : new[]{fallback};
            if(session.ActiveDungeon==null) DungeonOfferSystem.EnsureOffers(session,pool);
            var board=session.DungeonOffers; var average=DungeonOfferSystem.PartyLevel(session);
            var active=session.ActiveDungeon;
            for(int i=0;i<offerButtons.Length;i++)
            {
                var exists=i<board.Offers.Count;
                offerButtons[i].gameObject.SetActive(active==null && exists);
                if(!exists) continue;
                var offer=board.Offers[i];var definition=Resolve(offer.DefinitionId);
                offerLabels[i].text=$"{(definition!=null ? definition.DisplayName : "Niedostepny loch")}\nPoziom {DungeonOfferSystem.OfferLevel(average,offer.LevelOffset)}";
                offerBorders[i].color=i==board.SelectedIndex ? selectedColor : normalColor;
                offerButtons[i].interactable=active==null && definition!=null && !busy;
            }
            var selected=board.Offers.Count>board.SelectedIndex ? board.Offers[board.SelectedIndex] : null;
            var chosen=active!=null ? active.Definition ?? Resolve(active.DungeonId) : selected==null ? null : Resolve(selected.DefinitionId);
            var level=active!=null ? active.DungeonLevel : selected==null ? 0 : DungeonOfferSystem.OfferLevel(average,selected.LevelOffset);
            var difference=level-average;
            var relativeLevel=difference>0 ? $"+{difference}" : difference.ToString();
            details.text=chosen==null ? "Brak dostepnych wypraw." : $"{chosen.DisplayName}\n\nRegion: {ThemeName(chosen.Theme)}\nPoziom {level} · {relativeLevel} względem drużyny"+
                (active!=null ? "\n\nWyprawa w toku. Mozesz ja wznowic lub porzucic. Porzucenie usuwa postep w tym lochu." : "");
            if(!string.IsNullOrEmpty(errorMessage)) details.text+="\n\n"+errorMessage;
            departLabel.text=active==null ? "Wyrusz" : "Wznow";
            depart.interactable=!busy && chosen!=null && session.PlayerParty.ActiveMemberIds.Count>0;
            abandon.gameObject.SetActive(active!=null);abandon.interactable=!busy;
            abandonLabel.text=confirmAbandon ? "Potwierdz porzucenie" : "Porzuc wyprawe";
            partySummary.text=$"Aktywna druzyna - sredni poziom: {average}";
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
        private static string ThemeName(Godmist.Core.Enums.DungeonTheme theme) => theme switch
        {
            Godmist.Core.Enums.DungeonTheme.Catacombs => "Katakumby",
            Godmist.Core.Enums.DungeonTheme.Forest => "Las",
            Godmist.Core.Enums.DungeonTheme.ElvenRuins => "Elfie ruiny",
            Godmist.Core.Enums.DungeonTheme.Docks => "Doki",
            Godmist.Core.Enums.DungeonTheme.Desert => "Pustynia",
            Godmist.Core.Enums.DungeonTheme.Temple => "Swiatynia",
            Godmist.Core.Enums.DungeonTheme.Mountains => "Gory",
            Godmist.Core.Enums.DungeonTheme.Swamp => "Bagna",
            _ => "Wyprawa fabularna"
        };
        private void Depart()
        {
            if(busy) return;busy=true;
            var session=bootstrap.Session;var active=session.ActiveDungeon;
            bool success=active!=null ? bootstrap.TryEnterDungeon(active.Definition ?? Resolve(active.DungeonId),active.Seed)
                : bootstrap.TryEnterDungeonOffer(session.DungeonOffers.SelectedIndex, Resolve(session.DungeonOffers.Offers[session.DungeonOffers.SelectedIndex].DefinitionId));
            if(success) window.Close();else { busy=false;errorMessage="Nie udalo sie rozpoczac wyprawy.";Refresh(); }
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
