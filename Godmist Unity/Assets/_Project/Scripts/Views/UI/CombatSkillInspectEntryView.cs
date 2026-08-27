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
            caster = sourceCaster;
            tooltip = sourceTooltip;
            actionCost = CombatActionCostCalculator.CalculateSkillCost(caster, skill);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            tooltip ??= UIWorkspaceTooltipView.ActiveInstance;
            tooltip?.ShowSkill(skill, actionCost, this, caster);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            tooltip?.NotifySourceExit(this);
        }
    }
}
