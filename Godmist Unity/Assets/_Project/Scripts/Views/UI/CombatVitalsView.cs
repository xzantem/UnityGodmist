using Godmist.Core.Utilities;
using System.Collections.Generic;
using Godmist.Runtime.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Godmist.Views.UI
{
    internal static class CombatBarPresentation
    {
        public static void FitFrame(Slider slider)
        {
            // The authored fill area is inset 6px horizontally and 3px vertically.
            // slider_track has 12/6px slice borders: at multiplier 1 the frame
            // consumes a 12px bar's entire height and overlaps its fill.
            var frame = slider != null ? slider.transform.Find("GothicBarFrame")?.GetComponent<Image>() : null;
            if (frame?.sprite != null && frame.sprite.name == "slider_track")
                frame.pixelsPerUnitMultiplier = 2f;
        }
    }

    public static class CombatHealthPresentation
    {
        // Matches the authored shield fill in the combat vitals prefab.
        private static readonly Color ShieldColor = new(105f / 255f, 201f / 255f, 1f);

        public static string Format(float health, float maximum, int shield)
        {
            var value = $"{health:0}/{Godmist.Core.Utilities.HealthPoints.Maximum(maximum)}";
            return shield > 0 ? $"{value} (+{shield})" : value;
        }

        public static string Format(Godmist.Runtime.Characters.CharacterInstance character)
            => Format(character.CurrentHealth, character.MaximumHealth,
                character.ShieldPool?.TotalShield ?? 0);

        public static void UpdateShieldOverlay(ref Image overlay, Slider health, int shield, float maximum, float opacity = 1f)
            => UpdateShieldOverlay(ref overlay, health?.fillRect?.GetComponent<Image>(), shield, maximum, opacity);

        public static void UpdateShieldOverlay(ref Image overlay, Image healthFill, int shield, float maximum, float opacity = 1f)
        {
            if (healthFill == null || shield <= 0)
            {
                if (overlay != null) overlay.gameObject.SetActive(false);
                return;
            }

            // This is a visual layer of the explicitly bound HP fill, independent
            // of its current HP width. It introduces no prefab references.
            var area = healthFill.rectTransform.parent as RectTransform;
            if (area == null) return;
            if (overlay == null)
            {
                var node = new GameObject("ShieldOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                node.layer = healthFill.gameObject.layer;
                node.transform.SetParent(area, false);
                overlay = node.GetComponent<Image>();
                var layout = node.AddComponent<LayoutElement>();
                layout.ignoreLayout = true;
                overlay.raycastTarget = false;
            }

            overlay.gameObject.SetActive(true);
            overlay.sprite = healthFill.sprite;
            overlay.type = Image.Type.Simple;
            overlay.color = new Color(ShieldColor.r, ShieldColor.g, ShieldColor.b, opacity);
            overlay.maskable = healthFill.maskable;
            var rect = overlay.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(Mathf.Clamp01(shield / Mathf.Max(1f, Mathf.Max(maximum, shield))), 1f);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(0f, Mathf.Clamp(area.rect.height * .25f, 1f, 2f));
            rect.anchoredPosition = Vector2.zero;
            rect.SetAsLastSibling();
        }
    }

    public static class ActionPointPipPresentation
    {
        public static void Apply(Image image, int currentUnits, int index, Color available, Color spent, int remainingUnits = -1, Color? preview = null)
        {
            var fill = ActionPointUnits.PipFill(currentUnits, index);
            var meshFill = image.GetComponent<ActionPointPipFill>();
            if (meshFill != null)
            {
                image.type = Image.Type.Simple;
                image.color = Color.white;
                meshFill.SetFill(fill, remainingUnits < 0 ? fill : ActionPointUnits.PipFill(remainingUnits, index),
                    available, preview ?? available, spent);
                return;
            }
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            // Empty pips stay visible in the existing spent color.
            image.fillAmount = fill > 0f ? fill : 1f;
            image.color = fill > 0f ? available : spent;
        }
    }

    public sealed class CombatVitalsView : MonoBehaviour
    {
        [SerializeField] private Slider healthSlider, shieldSlider, resourceSlider;
        [SerializeField] private TextMeshProUGUI healthValueText, resourceValueText, actionValueText;
        [SerializeField] private Transform actionPointPipRoot;
        [SerializeField] private Image actionPointPipTemplate;
        [SerializeField] private Color availableColor = Color.white;
        [SerializeField] private Color spentColor = new(.12f,.15f,.19f,1);
        [SerializeField] private Color previewColor = new(1f,.65f,.15f,1);
        private readonly List<Image> pips = new();
        public static int RemainingAfterPreview(int current, int cost) => Mathf.Max(0, current - Mathf.Max(0,cost));
        public void Bind(BattleUnit unit, int previewCost = 0)
        {
            if (unit?.Character == null) return;
            var c=unit.Character;
            var maximum=c.MaximumHealth;
            var shield=c.ShieldPool?.TotalShield??0;
            Set(healthSlider,c.CurrentHealth,maximum);
            CombatBarPresentation.FitFrame(healthSlider);
            if (healthValueText!=null) healthValueText.text=CombatHealthPresentation.Format(c.CurrentHealth,maximum,shield);
            if(shieldSlider!=null){shieldSlider.gameObject.SetActive(shield>0);Set(shieldSlider,shield,Mathf.Max(maximum,shield));}
            var resource=c.ResourcePool;
            if(resourceSlider!=null){resourceSlider.gameObject.SetActive(resource!=null&&resource.MaxValue>0);Set(resourceSlider,resource?.CurrentValue??0,resource?.MaxValue??1);}
            if(resource!=null){CombatResourceColors.ApplyToFill(resourceSlider,resource.ResourceType);resourceSlider?.GetComponent<CombatSimpleTooltipTrigger>()?.InitializeResource(null, resource);}
            if(resourceValueText!=null)resourceValueText.text=resource==null?"":$"{resource.CurrentValue}/{resource.MaxValue}";
            var remaining=RemainingAfterPreview(unit.CurrentActionPoints,previewCost);
            if(actionValueText!=null)actionValueText.text=previewCost>unit.CurrentActionPoints?$"{ActionPointUnits.Format(unit.CurrentActionPoints)}/{ActionPointUnits.Format(unit.MaxActionPoints)} {UiTerms.ActionPointsShort} · potrzeba {ActionPointUnits.Format(previewCost)}":previewCost>0?$"{ActionPointUnits.Format(unit.CurrentActionPoints)} → {ActionPointUnits.Format(remaining)} {UiTerms.ActionPointsShort}":$"{ActionPointUnits.Format(unit.CurrentActionPoints)}/{ActionPointUnits.Format(unit.MaxActionPoints)} {UiTerms.ActionPointsShort}";
            if(actionPointPipTemplate==null||actionPointPipRoot==null)return;
            actionPointPipTemplate.gameObject.SetActive(false);
            var count = ActionPointUnits.PipCount(Mathf.Max(unit.MaxActionPoints, unit.CurrentActionPoints));
            while (pips.Count < count) pips.Add(Instantiate(actionPointPipTemplate, actionPointPipRoot));
            for (var i = 0; i < pips.Count; i++)
            {
                pips[i].gameObject.SetActive(i < count);
                ActionPointPipPresentation.Apply(pips[i], unit.CurrentActionPoints, i,
                    availableColor, spentColor, remaining, previewColor);
            }
        }
        private static void Set(Slider slider,float value,float maximum){if(slider==null)return;slider.minValue=0;slider.maxValue=Mathf.Max(1,maximum);slider.SetValueWithoutNotify(value);}
    }
}
