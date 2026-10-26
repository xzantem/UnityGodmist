using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using Godmist.Views.UI.Workspace;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class CombatTurnPortraitView : MonoBehaviour,
        UnityEngine.EventSystems.IPointerEnterHandler,
        UnityEngine.EventSystems.IPointerExitHandler,
        UnityEngine.EventSystems.IPointerClickHandler
    {
        [SerializeField] private Image portrait;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Image activeHighlight;
        [SerializeField] private Graphic hoverHighlight;
        [SerializeField] private Sprite playerHighlightSprite, enemyHighlightSprite;
        [SerializeField] private Color playerActiveColor = new(0.88f, 0.67f, 0.24f, 1f);
        [SerializeField] private Color enemyActiveColor = new(0.78f, 0.20f, 0.18f, 1f);

        [SerializeField] private Image[] activeEdges;
        [SerializeField] private CombatSimpleTooltipTrigger turnTooltip;
        [SerializeField] private TMPro.TextMeshProUGUI unknownLabel;

        private CombatHudView owner;
        private BattleUnit boundUnit;
        private BattleUnit boundInputUnit;
        private SummonedCombatMapObject boundObject;
        private bool boundKnown;
        private string tooltipTitle, tooltipDescription;
        private Image shieldOverlay;

        public BattleUnit BoundInputUnit => boundKnown ? boundInputUnit : null;

        private void Awake()
        {
            // The authored portrait prefab leaves every child graphic
            // raycast-disabled (and its portrait Image disabled), so no
            // PointerEnter can reach this owner in production. The root Image
            // is already the full 68x68 portrait surface; enabling only its
            // raycast participation preserves the authored visual and gives
            // the owner a stable EventSystem target.
            var pointerSurface = GetComponent<Graphic>();
            if (pointerSurface != null && pointerSurface.enabled)
                pointerSurface.raycastTarget = true;

            // The portrait owner is the single pointer source for both the
            // portrait graphic and its child graphics. The legacy trigger is
            // retained for serialized compatibility/data binding, but must not
            // also emit a second hover with its own source identity.
            if (turnTooltip != null) turnTooltip.enabled = false;
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData data)
        {
            owner?.SetTimelineHover(this, boundUnit);
            var tooltip = UIWorkspaceTooltipView.ActiveInstance;
            if (boundKnown && boundUnit?.Character != null)
                tooltip?.ShowUnit(boundUnit, this, true);
            else
                tooltip?.ShowText(tooltipTitle, tooltipDescription, this);
        }

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData data)
        {
            owner?.SetTimelineHover(this, null);
            UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit(this);
        }

        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData data)
        {
            if (data == null) return;
            if (UIWorkspaceTooltipView.HandlePointerClick(
                data.position,
                data.button == UnityEngine.EventSystems.PointerEventData.InputButton.Right))
            {
                return;
            }

            owner?.HandleTurnPortraitClick(this, data);
        }

        private void OnDisable()
        {
            owner?.SetTimelineHover(this, null);
            UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit(this);
        }
        public void Bind(BattleUnit unit, bool isActive, int occurrence = 1, CombatHudView source = null, bool highlighted = false, bool known = true)
        {
            if (unit?.Character == null)
            {
                gameObject.SetActive(false);
                return;
            }

            var bindingChanged = !ReferenceEquals(boundInputUnit, unit)
                || boundObject != null
                || boundKnown != known;
            if (bindingChanged)
                UIWorkspaceTooltipView.ActiveInstance?.InvalidateSource(this);
            if(boundUnit!=unit || !known)owner?.SetTimelineHover(this,null);
            owner=source;boundInputUnit=unit;boundObject=null;boundUnit=known ? unit : null;boundKnown=known;
            if (!known) { occurrence = 1; highlighted = false; }
            gameObject.SetActive(true);
            tooltipTitle = known ? unit.Character.Definition?.DisplayName ?? unit.Character.CharacterId : "Nieznany przeciwnik";
            tooltipDescription = isActive ? "Aktualna tura" : occurrence > 1 ? "Kolejna tura tej postaci w przewidywanej kolejce." : "Nadchodząca tura";
            if (unknownLabel != null)
            {
                unknownLabel.text = "?";
                unknownLabel.gameObject.SetActive(!known);
            }
            turnTooltip?.InitializeUnitTooltip(null, known ? unit : null,
                tooltipTitle,
                tooltipDescription,
                known);

            if (portrait != null)
            {
                portrait.sprite = known ? unit.Character.Definition?.UiPortraitSprite : null;
                portrait.color = known ? new Color(1, 1, 1, occurrence > 1 && !highlighted ? .35f : 1f) : new Color(.18f, .18f, .18f, 1f);
            }

            if (healthSlider != null)
            {
                healthSlider.gameObject.SetActive(known);
                CombatHealthPresentation.UpdateShieldOverlay(ref shieldOverlay, healthSlider,
                    known ? unit.Character.ShieldPool?.TotalShield ?? 0 : 0,
                    unit.Character.Stats?.MaxHealth.GetValue() ?? 1f,
                    occurrence > 1 && !highlighted ? .35f : 1f);
                if (known)
                {
                    healthSlider.minValue = 0f;
                    healthSlider.maxValue = Mathf.Max(1f, unit.Character.Stats?.MaxHealth.GetValue() ?? 1f);
                    healthSlider.SetValueWithoutNotify(Mathf.Clamp(unit.Character.CurrentHealth, 0f, healthSlider.maxValue));
                    var fill = healthSlider.fillRect != null ? healthSlider.fillRect.GetComponent<Image>() : null;
                    if (fill != null)
                    {
                        // Use the bottom combat panel's health tint.
                        var color = unit.Character.LifeState == Godmist.Core.Enums.CharacterLifeState.Dead
                            ? new Color(.4f, .4f, .4f)
                            : healthSlider.normalizedValue <= .25f ? new Color(1f, .4f, .23f)
                            : new Color(.7f, .12f, .1f);
                        color.a = occurrence > 1 && !highlighted ? .35f : 1f;
                        fill.color = color;
                    }
                }
            }

            foreach (var edge in activeEdges ?? System.Array.Empty<Image>())
            {
                if (edge == null) continue;
                edge.enabled = isActive;
                edge.color = unit.IsPlayerSide ? playerActiveColor : enemyActiveColor;
            }
            if (activeHighlight != null)
            {
                activeHighlight.enabled = isActive;
                ApplyActiveFrame(unit.IsPlayerSide);
            }
            if (hoverHighlight != null) hoverHighlight.enabled = highlighted;
        }

        public void BindObject(SummonedCombatMapObject summonedObject, bool isActive,
            int occurrence, CombatHudView source)
        {
            if (summonedObject?.Definition == null)
            {
                gameObject.SetActive(false);
                return;
            }

            var bindingChanged = boundObject != summonedObject
                || boundInputUnit != null
                || boundKnown;
            if (bindingChanged)
                UIWorkspaceTooltipView.ActiveInstance?.InvalidateSource(this);
            owner?.SetTimelineHover(this, null);
            owner = source;
            boundInputUnit = null;
            boundObject = summonedObject;
            boundUnit = null;
            boundKnown = false;
            tooltipTitle = summonedObject.Definition.DisplayName;
            if (hoverHighlight != null) hoverHighlight.enabled = false;
            gameObject.SetActive(true);

            var turnsUntilExpiry = Mathf.Max(1, summonedObject.RemainingDurationTurns - occurrence + 1);
            var isRune = CatacombsBossSystem.IsRune(summonedObject);
            if (unknownLabel != null)
            {
                unknownLabel.text = isRune ? turnsUntilExpiry.ToString() : string.Empty;
                unknownLabel.gameObject.SetActive(isRune);
            }
            tooltipDescription = isRune
                    ? turnsUntilExpiry == 1 ? "Wybuch w tej turze runy"
                        : $"Wybuch za {turnsUntilExpiry - 1} "
                          + (turnsUntilExpiry == 2 ? "turę runy" : "tury runy")
                    : $"Pozostałe tury obiektu: {turnsUntilExpiry}";
            turnTooltip?.Initialize(null, tooltipTitle, tooltipDescription);
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
                ApplyActiveFrame(false);
            }
        }

        private void ApplyActiveFrame(bool player)
        {
            // Both sides use the same selection frame. The legacy enemy sprite
            // is slot_invalid, whose baked-in X is not an active-turn marker.
            if (playerHighlightSprite != null) activeHighlight.sprite = playerHighlightSprite;
            activeHighlight.color = player ? Color.white : enemyActiveColor;
        }

    }
}
