using System.Collections.Generic;
using Godmist.Runtime.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Godmist.Views.UI
{
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
            var maximum=Mathf.Max(1,Mathf.RoundToInt(c.Stats?.MaxHealth.GetValue()??1));
            var shield=c.ShieldPool?.TotalShield??0;
            Set(healthSlider,c.CurrentHealth,maximum);
            if (healthValueText!=null) healthValueText.text=$"{c.CurrentHealth}/{maximum}"+(shield>0?$" (+{shield})":"");
            if(shieldSlider!=null){shieldSlider.gameObject.SetActive(shield>0);Set(shieldSlider,shield,Mathf.Max(maximum,shield));}
            var resource=c.ResourcePool;
            if(resourceSlider!=null){resourceSlider.gameObject.SetActive(resource!=null&&resource.MaxValue>0);Set(resourceSlider,resource?.CurrentValue??0,resource?.MaxValue??1);}
            if(resource!=null){CombatResourceColors.ApplyToFill(resourceSlider,resource.ResourceType);resourceSlider?.GetComponent<CombatSimpleTooltipTrigger>()?.Initialize(null,CombatResourceColors.ResourceName(resource.ResourceType),$"{resource.CurrentValue}/{resource.MaxValue}");}
            if(resourceValueText!=null)resourceValueText.text=resource==null?"":$"{resource.CurrentValue}/{resource.MaxValue}";
            var remaining=RemainingAfterPreview(unit.CurrentActionPoints,previewCost);
            if(actionValueText!=null)actionValueText.text=previewCost>unit.CurrentActionPoints?$"{ActionPointUnits.Format(unit.CurrentActionPoints)}/{ActionPointUnits.Format(unit.MaxActionPoints)} AP · potrzeba {ActionPointUnits.Format(previewCost)}":previewCost>0?$"{ActionPointUnits.Format(unit.CurrentActionPoints)} → {ActionPointUnits.Format(remaining)} AP":$"{ActionPointUnits.Format(unit.CurrentActionPoints)}/{ActionPointUnits.Format(unit.MaxActionPoints)} AP";
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
