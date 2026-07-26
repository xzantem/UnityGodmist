using System.Collections.Generic;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using Godmist.Views.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class CombatSkillBarView : MonoBehaviour
    {
        [SerializeField] private CombatSandboxBootstrap sandboxBootstrap;
        [SerializeField] private RectTransform skillBarRoot;
        [SerializeField] private GameObject skillSlotTemplate;
        [SerializeField] private CombatTargetSelectionController targetSelectionController;

        private readonly List<GameObject> spawnedSlots = new();

        private CombatSession cachedSession;
        private BattleUnit cachedActiveUnit;
        private int cachedSkillCount = -1;
        private bool slotsInitialized;

        private void Update()
        {
            if (sandboxBootstrap == null
                || skillBarRoot == null
                || skillSlotTemplate == null)
            {
                return;
            }

            var session = sandboxBootstrap.Session;

            if (session == null || session.ActiveUnit == null)
            {
                targetSelectionController?.CancelTargetSelection();
                ClearSlots();
                ResetCache();
                return;
            }

            var activeUnit = session.ActiveUnit;
            var skillCount = activeUnit.Character.Skills?.Count ?? 0;

            var needsRebuild =
                !slotsInitialized
                || cachedSession != session
                || cachedActiveUnit != activeUnit
                || cachedSkillCount != skillCount;

            if (needsRebuild)
            {
                RefreshSkillBar(activeUnit);
            }
            else
            {
                RefreshSlotVisuals(activeUnit);
            }
        }

        private void RefreshSkillBar(BattleUnit activeUnit)
        {
            targetSelectionController?.CancelTargetSelection();
            ClearSlots();

            var skills = activeUnit.Character.Skills;

            if (skills != null)
            {
                for (var i = 0; i < skills.Count; i++)
                {
                    var skill = skills[i];

                    if (skill == null)
                    {
                        continue;
                    }

                    var slotObject =
                        Instantiate(skillSlotTemplate, skillBarRoot);

                    slotObject.SetActive(true);
                    spawnedSlots.Add(slotObject);

                    BindSlotVisuals(
                        slotObject,
                        skill,
                        i,
                        activeUnit);

                    var button =
                        slotObject.GetComponent<Button>();

                    if (button != null)
                    {
                        var selectedSkill = skill;

                        button.onClick.AddListener(() =>
                            targetSelectionController
                                ?.BeginTargetSelection(selectedSkill));
                    }
                }
            }

            cachedSession = sandboxBootstrap.Session;
            cachedActiveUnit = activeUnit;
            cachedSkillCount = skills?.Count ?? 0;
            slotsInitialized = true;
        }

        private void RefreshSlotVisuals(BattleUnit activeUnit)
        {
            var skills = activeUnit.Character.Skills;

            if (skills == null)
            {
                return;
            }

            for (var i = 0;
                i < spawnedSlots.Count && i < skills.Count;
                i++)
            {
                BindSlotVisuals(
                    spawnedSlots[i],
                    skills[i],
                    i,
                    activeUnit);
            }
        }

        private void BindSlotVisuals(
            GameObject slotObject,
            SkillData skill,
            int index,
            BattleUnit activeUnit)
        {
            var texts =
                slotObject.GetComponentsInChildren
                    <TextMeshProUGUI>(true);

            var background =
                slotObject.GetComponent<Image>();

            TextMeshProUGUI slotIndexText = null;
            TextMeshProUGUI skillNameText = null;
            TextMeshProUGUI costText = null;

            foreach (var text in texts)
            {
                if (text.name == "SlotIndexText")
                {
                    slotIndexText = text;
                }
                else if (text.name == "SkillNameText")
                {
                    skillNameText = text;
                }
                else if (text.name == "CostText")
                {
                    costText = text;
                }
            }

            if (slotIndexText != null)
            {
                slotIndexText.text = (index + 1).ToString();
            }

            if (skillNameText != null)
            {
                skillNameText.text = skill.DisplayName;
            }

            var actionCost =
                CombatActionCostCalculator
                    .CalculateSkillCost(activeUnit, skill);

            if (costText != null)
            {
                costText.text =
                    $"AP {actionCost} ({skill.ActionCost}%) " +
                    $"| RES {skill.ResourceCost}";
            }

            if (background != null)
            {
                var enoughAp =
                    activeUnit.CurrentActionPoints >= actionCost;

                var enoughResource =
                    activeUnit.Character.ResourcePool.CurrentValue
                    >= skill.ResourceCost;

                background.color =
                    enoughAp && enoughResource
                        ? new Color(0.72f, 0.96f, 1f, 0.9f)
                        : new Color(0.80f, 0.48f, 0.48f, 0.9f);
            }
        }

        private void ClearSlots()
        {
            foreach (var slot in spawnedSlots)
            {
                if (slot != null)
                {
                    Destroy(slot);
                }
            }

            spawnedSlots.Clear();
        }

        private void ResetCache()
        {
            cachedSession = null;
            cachedActiveUnit = null;
            cachedSkillCount = -1;
            slotsInitialized = false;
        }
    }
}