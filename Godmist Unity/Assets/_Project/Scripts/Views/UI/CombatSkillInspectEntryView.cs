using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public sealed class CombatSkillInspectEntryView : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [SerializeField] private UnityEngine.UI.Image icon;
        [SerializeField] private TMPro.TextMeshProUGUI label;
        private SkillData skill;
        private BattleUnit caster;
        private UIWorkspaceTooltipView tooltip;
        private int actionCost;

        public void Initialize(
            SkillData sourceSkill,
            BattleUnit sourceCaster,
            UIWorkspaceTooltipView sourceTooltip)
        {
            skill = sourceSkill;
            if (icon != null) { icon.sprite = skill.Icon; icon.enabled = icon.sprite != null; }
            if (label != null) label.text = skill.DisplayName;
            caster = sourceCaster;
            tooltip = sourceTooltip;
            actionCost = CombatActionCostCalculator.CalculateSkillCost(caster, skill);
        }

        private void OnDisable() => tooltip?.NotifySourceExit(this);

        public void OnPointerEnter(PointerEventData eventData)
        {
            tooltip ??= UIWorkspaceTooltipView.ActiveInstance;
            if(Mathf.Max(caster?.MaxActionPoints??0,caster?.CurrentActionPoints??0)>0)
                tooltip?.ShowSkill(skill, actionCost, this, caster);
            else
                tooltip?.ShowLoadoutSkillDetails(skill,caster?.Character,this,null);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            tooltip?.NotifySourceExit(this);
        }
    }
}
