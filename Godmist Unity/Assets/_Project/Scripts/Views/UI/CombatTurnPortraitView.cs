using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class CombatTurnPortraitView : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        [SerializeField] private Image portrait;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Image activeHighlight;
        [SerializeField] private Color playerActiveColor = new(0.88f, 0.67f, 0.24f, 1f);
        [SerializeField] private Color enemyActiveColor = new(0.78f, 0.20f, 0.18f, 1f);

        [SerializeField] private Image[] activeEdges;
        [SerializeField] private CombatSimpleTooltipTrigger turnTooltip;
        [SerializeField] private TMPro.TextMeshProUGUI unknownLabel;

        private CombatHudView owner;
        private BattleUnit boundUnit;
        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData data)=>owner?.SetTimelineHover(this,boundUnit);
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData data)=>owner?.SetTimelineHover(this,null);
        private void OnDisable()=>owner?.SetTimelineHover(this,null);
        public void Bind(BattleUnit unit, bool isActive, int occurrence = 1, CombatHudView source = null, bool highlighted = false, bool known = true)
        {
            if (unit?.Character == null)
            {
                gameObject.SetActive(false);
                return;
            }

            if(boundUnit!=unit || !known)owner?.SetTimelineHover(this,null);
            owner=source;boundUnit=known ? unit : null;
            if (!known) { occurrence = 1; highlighted = false; }
            gameObject.SetActive(true);
            if (unknownLabel != null)
            {
                unknownLabel.text = "?";
                unknownLabel.gameObject.SetActive(!known);
            }
            turnTooltip?.Initialize(null, known ? unit.Character.Definition?.DisplayName ?? unit.Character.CharacterId : "Nieznany przeciwnik",
                isActive ? "Aktualna tura" : occurrence > 1 ? "Kolejna tura tej postaci w przewidywanej kolejce." : "Nadchodząca tura");

            if (portrait != null)
            {
                portrait.sprite = known ? unit.Character.Definition?.VisualSprite : null;
                portrait.color = known ? new Color(1, 1, 1, occurrence > 1 && !highlighted ? .35f : 1f) : new Color(.18f, .18f, .18f, 1f);
            }

            // The timeline communicates turn sequence, while the party rail
            // communicates current state. It deliberately remains portrait-only.
            if (healthSlider != null)
            {
                healthSlider.gameObject.SetActive(false);
            }

            foreach (var edge in activeEdges ?? System.Array.Empty<Image>())
            {
                if (edge == null) continue;
                edge.enabled = isActive || highlighted;
                edge.color = highlighted ? Color.white : unit.IsPlayerSide ? playerActiveColor : enemyActiveColor;
            }
            if (activeHighlight != null)
            {
                activeHighlight.enabled = isActive || highlighted;
                activeHighlight.color = highlighted ? Color.white : unit.IsPlayerSide
                    ? playerActiveColor
                    : enemyActiveColor;
            }
        }

        public void BindObject(SummonedCombatMapObject summonedObject, bool isActive,
            int occurrence, CombatHudView source)
        {
            if (summonedObject?.Definition == null)
            {
                gameObject.SetActive(false);
                return;
            }

            owner?.SetTimelineHover(this, null);
            owner = source;
            boundUnit = null;
            gameObject.SetActive(true);

            var turnsUntilExpiry = Mathf.Max(1, summonedObject.RemainingDurationTurns - occurrence + 1);
            var isRune = CatacombsBossSystem.IsRune(summonedObject);
            if (unknownLabel != null)
            {
                unknownLabel.text = isRune ? turnsUntilExpiry.ToString() : string.Empty;
                unknownLabel.gameObject.SetActive(isRune);
            }
            turnTooltip?.Initialize(null, summonedObject.Definition.DisplayName,
                isRune
                    ? turnsUntilExpiry == 1 ? "Wybuch w tej turze runy"
                        : $"Wybuch za {turnsUntilExpiry - 1} "
                          + (turnsUntilExpiry == 2 ? "turę runy" : "tury runy")
                    : $"Pozostałe tury obiektu: {turnsUntilExpiry}");
            if (portrait != null)
            {
                portrait.sprite = summonedObject.Definition.Icon;
                portrait.color = new Color(1f, 1f, 1f, occurrence > 1 ? .5f : 1f);
            }
            if (healthSlider != null) healthSlider.gameObject.SetActive(false);
            foreach (var edge in activeEdges ?? System.Array.Empty<Image>())
            {
                if (edge == null) continue;
                edge.enabled = isActive;
                edge.color = enemyActiveColor;
            }
            if (activeHighlight != null)
            {
                activeHighlight.enabled = isActive;
                activeHighlight.color = enemyActiveColor;
            }
        }

    }
}
