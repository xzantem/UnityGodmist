using Godmist.Runtime.Combat;
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

        private CombatHudView owner;
        private BattleUnit boundUnit;
        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData data)=>owner?.SetTimelineHover(this,boundUnit);
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData data)=>owner?.SetTimelineHover(this,null);
        private void OnDisable()=>owner?.SetTimelineHover(this,null);
        public void Bind(BattleUnit unit, bool isActive, int occurrence = 1, CombatHudView source = null, bool highlighted = false)
        {
            if (unit?.Character == null)
            {
                gameObject.SetActive(false);
                return;
            }

            if(boundUnit!=unit)owner?.SetTimelineHover(this,null);
            owner=source;boundUnit=unit;
            gameObject.SetActive(true);
            turnTooltip?.Initialize(null, unit.Character.Definition?.DisplayName ?? unit.Character.CharacterId,
                isActive ? "Aktualna tura" : occurrence > 1 ? "Kolejna tura tej postaci w przewidywanej kolejce." : "Nadchodząca tura");

            if (portrait != null)
            {
                portrait.sprite = unit.Character.Definition?.VisualSprite;
                var tint=portrait.color;tint.a=occurrence>1&&!highlighted?.35f:1f;portrait.color=tint;
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

    }
}
