using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Data.Items;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Items;
using Godmist.Systems.Combat;
using Godmist.Systems.Items;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Godmist.Views.Combat
{
    public class CombatTargetSelectionController : MonoBehaviour
    {
        public System.Func<Vector3, GridPosition?> ResolvePointerPosition { get; set; }

        public void SetGridMapping(Vector2 origin, float size)
        {
            gridOrigin = origin;
            cellSize = Mathf.Max(0.01f, size);
        }
        [Header("References")]
        [SerializeField] private CombatSandboxBootstrap sandboxBootstrap;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private CombatGridFeedbackRenderer feedbackRenderer;
        [SerializeField] private CombatMovementInputController movementInputController;

        [Header("Grid Mapping")]
        [SerializeField] private Vector2 gridOrigin = Vector2.zero;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Color targetHighlightColor = new(1f, 0.72f, 0.2f, 0.65f);
        [SerializeField] private Color absoluteRangeHighlightColor = new(0.25f, 0.55f, 1f, 0.25f);

        [SerializeField] private Color relativeRangeHighlightColor = new(1f, 0.3f, 0.12f, 0.42f);

        [SerializeField] private Color affectedTargetHighlightColor = new(1f, 0.45f, 0.15f, 0.8f);

        private readonly HashSet<GridPosition> absoluteRangePositions = new();

        private readonly HashSet<GridPosition> selectablePositions = new();

        private readonly HashSet<GridPosition> eligibleTargetPositions = new();

        private GridPosition? previewedRelativeCenter;
        private int directionalRotationQuarterTurns;

        private string judgementFieldSignature;
        private SkillData selectedSkill;
        private ConsumableItemInstance selectedConsumable;
        private readonly ConsumableService consumableService = new();
        private readonly Dictionary<int, GridPosition?> selectedPositionsByModeIndex = new();
        private readonly Dictionary<int, int> rotationsByModeIndex = new();
        private readonly Dictionary<int, IReadOnlyList<GridPosition>> chosenPositionsByModeIndex = new();
        private int currentTargetModeIndex;
        [System.NonSerialized] private SkillEffectData selectedTeleportEffect;
        private GridPosition selectedTeleportSourcePosition;
        private bool isSelectingTeleportDestination;
        private SkillEffectData selectedCreateObjectEffect;
        private bool isSelectingObjectPlacement;
        private CombatTargetPreviewView targetPreview;
        private SkillTargetModeData CurrentTargetMode => PassiveEffectCombatExecutionSystem.ResolveMode(sandboxBootstrap.Session?.ActiveUnit, selectedSkill, selectedSkill.TargetModes[currentTargetModeIndex]);

        public bool IsSelectingTarget => selectedSkill != null;
        public bool ConsumedPrimaryClickThisFrame { get; private set; }
        public BattleUnit HoveredUnit { get; private set; }
        public SkillData SelectedSkill => selectedSkill;
        public int CurrentTargetModeIndex => currentTargetModeIndex;

        private void Awake()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (movementInputController == null)
            {
                movementInputController =
                    GetComponent<CombatMovementInputController>();
            }
        }

        public void BeginTargetSelection(SkillData skill)
        {
            if (skill != null && selectedSkill == skill)
            {
                CancelTargetSelection();
                return;
            }

            var session = sandboxBootstrap?.Session;

            if (skill == null || session == null || session.ActiveUnit == null
                || !session.ActiveUnit.IsPlayerSide
                || session.Outcome != CombatOutcomeState.Ongoing)
            {
                return;
            }

            if (skill.IsToggle
                && session.ActiveUnit.ActiveToggleSkillIds != null
                && session.ActiveUnit.ActiveToggleSkillIds.Contains(skill.SkillId))
            {
                sandboxBootstrap.TryExecuteSkill(
                    skill,
                    new Dictionary<int, GridPosition?>());
                return;
            }

            var isObjectPlacementOnlySkill =
                IsObjectPlacementOnlySkill(skill);

            if (!isObjectPlacementOnlySkill
                && (skill.TargetModes == null || skill.TargetModes.Count == 0))
            {
                Debug.LogWarning($"Skill '{skill.SkillId}' has no target modes.");
                return;
            }

            if (!isObjectPlacementOnlySkill)
            {
                foreach (var targetMode in skill.TargetModes)
                {
                    if (targetMode == null
                        || !IsSupportedTargetType(targetMode.TargetType))
                    {
                        Debug.LogWarning(
                            $"Skill '{skill.SkillId}' has an invalid target mode.");

                        return;
                    }
                }
            }

            if (IsSelectingTarget)
            {
                CancelTargetSelection();
            }

            selectedSkill = skill;
            currentTargetModeIndex = 0;
            selectedPositionsByModeIndex.Clear();
            chosenPositionsByModeIndex.Clear();
            rotationsByModeIndex.Clear();
            directionalRotationQuarterTurns = 0;

            if (movementInputController != null)
            {
                movementInputController.enabled = false;
            }

            BeginCurrentTargetMode();
        }
        private void BeginCurrentTargetMode()
        {
            if (selectedSkill == null)
            {
                return;
            }

            if (IsObjectPlacementOnlySkill(selectedSkill))
            {
                TryCompleteSelection();
                return;
            }

            while (currentTargetModeIndex < selectedSkill.TargetModes.Count)
            {
                var targetMode = CurrentTargetMode;

                if (targetMode.TargetType == TargetType.Caster)
                {
                    selectedPositionsByModeIndex[currentTargetModeIndex] = null;
                    currentTargetModeIndex++;
                    continue;
                }

                RebuildTargetHighlights();
                return;
            }

            TryCompleteSelection();
        }

        private void AdvanceTargetMode()
        {
            currentTargetModeIndex++;
            BeginCurrentTargetMode();
        }

        private void TryCompleteSelection()
        {
            if (TryBeginTeleportDestinationSelection())
            {
                return;
            }

            if (TryBeginObjectPlacementSelection())
            {
                return;
            }

            if (sandboxBootstrap.TryExecuteSkill(
                    selectedSkill,
                    selectedPositionsByModeIndex,
                    null,
                    null,
                    rotationsByModeIndex, chosenPositionsByModeIndex))
            {
                SpendSelectedConsumable();
                CancelTargetSelection();
                return;
            }

            CancelTargetSelection();
        }

        public void CancelTargetSelection()
        {
            selectedSkill = null;
            selectedConsumable = null;
            currentTargetModeIndex = 0;
            selectedPositionsByModeIndex.Clear();
            chosenPositionsByModeIndex.Clear();
            rotationsByModeIndex.Clear();
            directionalRotationQuarterTurns = 0;

            selectedTeleportEffect = null;
            selectedTeleportSourcePosition = default;
            isSelectingTeleportDestination = false;

            selectedCreateObjectEffect = null;
            isSelectingObjectPlacement = false;
            HoveredUnit = null;

            ClearAllHighlights();

            if (movementInputController != null)
            {
                movementInputController.enabled = true;
            }
        }

        private void Update()
        {
            ConsumedPrimaryClickThisFrame = false;
            RefreshJudgementField();

            if (DevConsoleView.IsOpen || !IsSelectingTarget)
            {
                return;
            }

            var session = sandboxBootstrap?.Session;

            if (session == null || session.ActiveUnit == null
                || !session.ActiveUnit.IsPlayerSide
                || session.Outcome != CombatOutcomeState.Ongoing)
            {
                CancelTargetSelection();
                return;
            }

            if (!isSelectingTeleportDestination
                && !isSelectingObjectPlacement)
            {
                if (Input.GetKeyDown(KeyCode.R)
                    && (IsDirectionalCasterArea(CurrentTargetMode)
                        || (CurrentTargetMode.UseRelativeRange
                            && CombatTargetingService.IsRotatableRelativeShape(
                                CurrentTargetMode.RelativeRange?.Shape
                                ?? RangeShape.None))))
                {
                    directionalRotationQuarterTurns =
                        (directionalRotationQuarterTurns + 1) % 4;
                    previewedRelativeCenter = null;
                }
                UpdateRelativeRangePreview();
                UpdateHoveredUnit(session);
                UpdateCombatTargetPreview(session);
            }
            else
            {
                HoveredUnit = null;
                targetPreview?.Hide();
            }

            if (Input.GetKeyDown(KeyCode.Escape)
                || Input.GetMouseButtonDown(1))
            {
                CancelTargetSelection();
                return;
            }

            if (!isSelectingTeleportDestination && !isSelectingObjectPlacement
                && Input.GetKeyDown(KeyCode.Return)
                && CurrentTargetMode.TargetType == TargetType.MultipleChosen
                && chosenPositionsByModeIndex.TryGetValue(currentTargetModeIndex, out var confirmed)
                && confirmed.Count > 0)
            {
                AdvanceTargetMode();
                return;
            }
            if (!Input.GetMouseButtonDown(0)
                || worldCamera == null)
            {
                return;
            }

            if (EventSystem.current != null
                && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            ConsumedPrimaryClickThisFrame = true;
            if (!TryGetGridPositionUnderMouse(out var targetPosition)) return;

            if (isSelectingTeleportDestination)
            {
                if (!selectablePositions.Contains(targetPosition))
                {
                    return;
                }

                if (sandboxBootstrap.TryExecuteSkill(
                        selectedSkill,
                        selectedPositionsByModeIndex,
                        targetPosition,
                        null))
                {
                    SpendSelectedConsumable();
                    CancelTargetSelection();
                }

                return;
            }

            if (isSelectingObjectPlacement)
            {
                if (!selectablePositions.Contains(targetPosition))
                {
                    return;
                }

                if (sandboxBootstrap.TryExecuteSkill(
                        selectedSkill,
                        selectedPositionsByModeIndex,
                        null,
                        targetPosition))
                {
                    SpendSelectedConsumable();
                    CancelTargetSelection();
                }

                return;
            }

            var targetMode = CurrentTargetMode;

            if (UsesGridClickOnlyAsConfirmation(targetMode))
            {
                if (session.Grid == null
                    || !session.Grid.IsWalkable(targetPosition))
                {
                    return;
                }

                selectedPositionsByModeIndex[currentTargetModeIndex] = null;
                AdvanceTargetMode();
                return;
            }

            if (!selectablePositions.Contains(targetPosition))
            {
                return;
            }

            if (targetMode.TargetType == TargetType.MultipleChosen)
            {
                var chosen = chosenPositionsByModeIndex.TryGetValue(currentTargetModeIndex, out var existing)
                    ? existing.ToList() : new List<GridPosition>();
                if (chosen.Contains(targetPosition)) return;
                chosen.Add(targetPosition);
                chosenPositionsByModeIndex[currentTargetModeIndex] = chosen;
                selectedPositionsByModeIndex[currentTargetModeIndex] = chosen[0];
                var available = sandboxBootstrap.GetEligibleSkillTargetPositions(selectedSkill, targetMode, currentTargetModeIndex).Count;
                if (chosen.Count < Math.Min(targetMode.ChainTargets, available))
                {
                    RebuildTargetHighlights();
                    return;
                }
                AdvanceTargetMode();
                return;
            }
            selectedPositionsByModeIndex[currentTargetModeIndex] =
                IsCursorDirectedCasterArea(targetMode)
                    ? RotateDirectionReference(targetPosition)
                    : targetPosition;
            if (IsRotatableShape(targetMode))
            {
                rotationsByModeIndex[currentTargetModeIndex] =
                    directionalRotationQuarterTurns;
            }

            AdvanceTargetMode();
        }

        private void RebuildTargetHighlights()
        {
            ClearAllHighlights();

            if (feedbackRenderer == null
                || selectedSkill == null)
            {
                return;
            }

            var absolutePositions =
                sandboxBootstrap.GetAbsoluteSkillRangePositions(
                    selectedSkill,
                    CurrentTargetMode);

            foreach (var position in absolutePositions)
            {
                absoluteRangePositions.Add(position);
            }

            var usesRedDirectionalFootprintOnly =
                IsCursorDirectedCasterArea(CurrentTargetMode);
            if (!usesRedDirectionalFootprintOnly)
            {
                SpawnHighlights(
                    absolutePositions,
                    absoluteRangeHighlightColor,
                    CombatGridFeedbackLayer.SkillRange);
                feedbackRenderer.ShowFill(
                    CombatGridFeedbackLayer.SkillRange,
                    absolutePositions,
                    WithAlpha(absoluteRangeHighlightColor, .24f));
            }

            var selectable =
                sandboxBootstrap.GetSelectableSkillPositions(
                    selectedSkill,
                    CurrentTargetMode,
                    currentTargetModeIndex);

            foreach (var position in selectable)
            {
                if (!chosenPositionsByModeIndex.TryGetValue(currentTargetModeIndex, out var chosen) || !chosen.Contains(position))
                    selectablePositions.Add(position);
            }

            if (CurrentTargetMode.TargetType is TargetType.Single or TargetType.MultipleChosen or TargetType.Chain)
            {
                foreach (var position in sandboxBootstrap.GetEligibleSkillTargetPositions(
                             selectedSkill, CurrentTargetMode, currentTargetModeIndex))
                    eligibleTargetPositions.Add(position);
            }

            if (chosenPositionsByModeIndex.TryGetValue(currentTargetModeIndex, out var selectedCells))
            {
                feedbackRenderer.ShowOutline(CombatGridFeedbackLayer.ChosenSkillTargets,selectedCells,new Color(.3f,1f,.5f,.9f));
                feedbackRenderer.ShowFill(CombatGridFeedbackLayer.ChosenSkillTargets,selectedCells,new Color(.3f,1f,.5f,.4f));
            }
            if (!usesRedDirectionalFootprintOnly)
            {
                SpawnHighlights(
                    selectablePositions.ToList(),
                    targetHighlightColor,
                    CombatGridFeedbackLayer.SkillTarget);
                feedbackRenderer.ShowFill(
                    CombatGridFeedbackLayer.SkillTarget,
                    selectablePositions,
                    WithAlpha(targetHighlightColor, .28f));
            }
        }

        private void RefreshJudgementField()
        {
            if(feedbackRenderer==null)return;
            var session=sandboxBootstrap?.Session;
            var field=session?.SummonedObjects.FirstOrDefault(PaladinCombatSystem.IsField);
            var source=field==null?null:session.Units.FirstOrDefault(u=>u.Character.CharacterId==field.CreatorCharacterId);
            var radius=source==null?0:2+PaladinCombatSystem.C(source,"field_range");
            var signature=field==null?string.Empty:$"{session.CombatId}:{field.ObjectId}:{field.Position}:{radius}";
            if(signature==judgementFieldSignature)return;
            judgementFieldSignature=signature;
            feedbackRenderer.ClearOutline(CombatGridFeedbackLayer.JudgementField);
            feedbackRenderer.ClearFill(CombatGridFeedbackLayer.JudgementField);
            if(field==null||session.Grid==null)return;
            var cells=new List<GridPosition>();var sight=new CombatLineOfSightService();
            for(var x=Math.Max(0,field.Position.X-radius);x<=Math.Min(session.Grid.Width-1,field.Position.X+radius);x++)
                for(var y=Math.Max(0,field.Position.Y-radius);y<=Math.Min(session.Grid.Height-1,field.Position.Y+radius);y++)
                    foreach(var cell in session.Grid.GetPositionsAt(x,y))
                        if(PaladinCombatSystem.FieldContains(session,field,cell))cells.Add(cell);
            feedbackRenderer.ShowOutline(CombatGridFeedbackLayer.JudgementField,cells,new Color(1f,.85f,.3f,.8f));
            feedbackRenderer.ShowFill(CombatGridFeedbackLayer.JudgementField,cells,new Color(1f,.85f,.3f,.12f));
        }
        private void UpdateRelativeRangePreview()
        {
            if (selectedSkill == null || !HasRelativeRangePreview())
            {
                ClearRelativeRangeHighlights();
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                ClearRelativeRangeHighlights();
                return;
            }

            if (!TryGetGridPositionUnderMouse(out var hoveredPosition)) return;

            if (previewedRelativeCenter.HasValue && previewedRelativeCenter.Value == hoveredPosition) return;

            ClearRelativeRangeHighlights();
            previewedRelativeCenter = hoveredPosition;

            if (!IsRelativePreviewCenter(hoveredPosition)) return;

            var directionReference = IsDirectionalCasterArea(CurrentTargetMode)
                ? RotateDirectionReference(hoveredPosition)
                : hoveredPosition;
            var relativePositions = IsDirectionalCasterArea(CurrentTargetMode)
                ? sandboxBootstrap.GetDirectionalSkillRangePositions(
                    selectedSkill,
                    CurrentTargetMode,
                    directionReference,
                    directionalRotationQuarterTurns)
                : sandboxBootstrap.GetRelativeSkillRangePositions(
                    selectedSkill,
                    CurrentTargetMode,
                    directionReference,
                    directionalRotationQuarterTurns);

            SpawnHighlights(relativePositions, relativeRangeHighlightColor, CombatGridFeedbackLayer.RelativeRange);
            feedbackRenderer.ShowFill(
                CombatGridFeedbackLayer.RelativeRange,
                relativePositions,
                WithAlpha(relativeRangeHighlightColor, .24f));

            var affectedPositions = new List<GridPosition>();

            foreach (var target in sandboxBootstrap.GetSkillTargets(
                selectedSkill,
                CurrentTargetMode,
                currentTargetModeIndex,
                directionReference,
                IsRotatableShape(CurrentTargetMode)
                    ? directionalRotationQuarterTurns
                    : 0)) affectedPositions.Add(target.Position);

            SpawnHighlights(affectedPositions, affectedTargetHighlightColor, CombatGridFeedbackLayer.AffectedArea);
            feedbackRenderer.ShowFill(
                CombatGridFeedbackLayer.AffectedArea,
                affectedPositions,
                WithAlpha(affectedTargetHighlightColor, .28f));
        }

        private bool HasRelativeRangePreview()
        {
            if (selectedSkill?.TargetModes == null
                || selectedSkill.TargetModes.Count == 0)
            {
                return false;
            }

            if (isSelectingTeleportDestination
                || isSelectingObjectPlacement)
            {
                return false;
            }

            return CurrentTargetMode.UseRelativeRange
                || IsDirectionalCasterArea(CurrentTargetMode);
        }

        private bool TryBeginTeleportDestinationSelection()
        {
            if (selectedSkill?.Effects == null || sandboxBootstrap?.Session?.ActiveUnit == null)
            {
                return false;
            }

            foreach (var effect in selectedSkill.Effects)
            {
                if (effect?.EffectType != EffectType.Teleport)
                {
                    continue;
                }

                if (effect.TargetModeIndex < 0
                    || effect.TargetModeIndex >= selectedSkill.TargetModes.Count)
                {
                    return false;
                }

                var sourceTargetMode =
                    selectedSkill.TargetModes[effect.TargetModeIndex];

                if (sourceTargetMode.TargetType == TargetType.Caster)
                {
                    selectedTeleportSourcePosition = sandboxBootstrap.Session.ActiveUnit.Position;
                }
                else if (!selectedPositionsByModeIndex.TryGetValue(
                             effect.TargetModeIndex,
                             out var selectedPosition)
                         || !selectedPosition.HasValue)
                {
                    return false;
                }
                else
                {
                    selectedTeleportSourcePosition = selectedPosition.Value;
                }

                selectedTeleportEffect = effect;
                isSelectingTeleportDestination = true;

                ClearAllHighlights();

                var destinations = sandboxBootstrap.GetTeleportDestinationPositions(
                    selectedSkill,
                    selectedTeleportEffect,
                    selectedTeleportSourcePosition);

                foreach (var position in destinations)
                {
                    if (!chosenPositionsByModeIndex.TryGetValue(currentTargetModeIndex, out var chosen) || !chosen.Contains(position))
                    selectablePositions.Add(position);
                }

                SpawnHighlights(
                    destinations,
                    relativeRangeHighlightColor,
                    CombatGridFeedbackLayer.RelativeRange);
                feedbackRenderer.ShowFill(
                    CombatGridFeedbackLayer.RelativeRange,
                    destinations,
                    WithAlpha(relativeRangeHighlightColor, .24f));

                return true;
            }

            return false;
        }

        private bool TryBeginObjectPlacementSelection()
        {
            if (selectedSkill?.Effects == null
                || sandboxBootstrap?.Session?.ActiveUnit == null
                || !IsObjectPlacementOnlySkill(selectedSkill))
            {
                return false;
            }

            foreach (var effect in selectedSkill.Effects)
            {
                if (effect?.EffectType != EffectType.CreateObject
                    && effect?.EffectType != EffectType.Summon)
                {
                    continue;
                }

                if (effect.EffectType == EffectType.CreateObject
                    && effect.SummonedObjectDefinition == null)
                {
                    return false;
                }

                if (effect.EffectType == EffectType.Summon
                    && (effect.SummonedCharacterDefinition == null
                        || effect.SummonedClassData == null))
                {
                    return false;
                }

                selectedCreateObjectEffect = effect;
                isSelectingObjectPlacement = true;

                ClearAllHighlights();

                var placements =
                    sandboxBootstrap.GetObjectPlacementPositions(
                        selectedSkill,
                        selectedCreateObjectEffect);

                foreach (var position in placements)
                {
                    if (!chosenPositionsByModeIndex.TryGetValue(currentTargetModeIndex, out var chosen) || !chosen.Contains(position))
                    selectablePositions.Add(position);
                }

                SpawnHighlights(
                    placements,
                    relativeRangeHighlightColor,
                    CombatGridFeedbackLayer.RelativeRange);
                feedbackRenderer.ShowFill(
                    CombatGridFeedbackLayer.RelativeRange,
                    placements,
                    WithAlpha(relativeRangeHighlightColor, .24f));

                return true;
            }

            return false;
        }

        private void SpawnHighlights(
            IReadOnlyList<GridPosition> positions,
            Color color,
            CombatGridFeedbackLayer layer)
        {
            feedbackRenderer?.ShowOutline(layer, positions, color);
        }
        private bool TryGetGridPositionUnderMouse(out GridPosition position)
        {
            var screenPosition = Input.mousePosition;
            screenPosition.z =
                Mathf.Abs(worldCamera.transform.position.z);

            var worldPosition =
                worldCamera.ScreenToWorldPoint(screenPosition);

            var gridX = Mathf.RoundToInt(
                (worldPosition.x - gridOrigin.x) / cellSize);

            var gridY = Mathf.RoundToInt(
                (worldPosition.y - gridOrigin.y) / cellSize);

            var resolved = ResolvePointerPosition != null
                ? ResolvePointerPosition(worldPosition) : new GridPosition(gridX, gridY);
            position = resolved.GetValueOrDefault();
            return resolved.HasValue;
        }

        private void SpendSelectedConsumable()
        {
            var session = sandboxBootstrap?.Session;
            var consumable = selectedConsumable;
            var definition = consumable?.ConsumableDefinition;
            if (session?.ActiveUnit?.Character == null || definition == null) return;
            if (!consumableService.TrySpendCharge(session.ActiveUnit.Character, consumable)) return;
            session.ActiveUnit.ConsumableUseTypesUsedThisTurn.Add(definition.UseType);
        }

        public void BeginConsumableTargetSelection(ConsumableItemInstance consumable)
        {
            var session = sandboxBootstrap?.Session;
            var definition = consumable?.ConsumableDefinition;
            var skill = ConsumableRuntimeActionBuilder.Build(consumable);
            if (definition == null || skill == null || !consumable.HasCharges
                || session?.ActiveUnit?.Character == null
                || session.ActiveUnit.ConsumableUseTypesUsedThisTurn.Contains(definition.UseType))
            {
                return;
            }

            selectedConsumable = consumable;
            BeginTargetSelection(skill);
        }

        private void UpdateHoveredUnit(CombatSession session)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                HoveredUnit = null;
                return;
            }

            HoveredUnit = null;
            if (!TryGetGridPositionUnderMouse(out var position)) return;
            foreach (var unit in session.Units)
            {
                if (unit != null && unit.Position == position)
                {
                    HoveredUnit = unit;
                    return;
                }
            }
        }

        private void UpdateCombatTargetPreview(CombatSession session)
        {
            if (selectedSkill == null
                || worldCamera == null
                || HoveredUnit == null
                || currentTargetModeIndex < 0
                || currentTargetModeIndex >= selectedSkill.TargetModes.Count)
            {
                targetPreview?.Hide();
                return;
            }

            var mode = CurrentTargetMode;
            var selectedPosition = GetPreviewSelectedPosition(mode, HoveredUnit);
            var rotation = IsRotatableShape(mode)
                ? directionalRotationQuarterTurns
                : 0;
            var targets = sandboxBootstrap.GetSkillTargets(
                selectedSkill,
                mode,
                currentTargetModeIndex,
                selectedPosition,
                rotation);
            if (targets == null || !targets.Contains(HoveredUnit))
            {
                targetPreview?.Hide();
                return;
            }

            var preview = CombatSkillPreviewCalculator.CalculateTargetPreview(
                session.ActiveUnit,
                HoveredUnit,
                selectedSkill,
                currentTargetModeIndex);
            if (!preview.IsValid)
            {
                targetPreview?.Hide();
                return;
            }

            targetPreview ??= CombatTargetPreviewView.Create(
                UIWorkspaceTooltipView.ActiveInstance?.transform.parent);
            if (targetPreview == null)
            {
                return;
            }

            var worldPosition = new Vector3(
                gridOrigin.x + HoveredUnit.Position.X * cellSize,
                gridOrigin.y + HoveredUnit.Position.Y * cellSize,
                0f);
            var screenPosition = worldCamera.WorldToScreenPoint(worldPosition);
            var hitText = preview.HasHitChance && preview.Damage.HasDamage
                ? $"Trafienie: {preview.HitChance * 100f:0.#}%\n"
                : string.Empty;
            var damageText = preview.Damage.HasDamage
                ? $"{hitText}Obrażenia: {preview.Damage.Minimum}–{preview.Damage.Maximum}"
                : string.Empty;
            var statusSprite = preview.HasStatusChance
                ? UIWorkspaceTooltipView.ActiveInstance?.GetGlossaryIcon(preview.StatusId)
                    ?? UIWorkspaceTooltipView.ActiveInstance?.GetGlossaryIcon("debuff")
                : null;
            targetPreview.Show(
                screenPosition,
                damageText,
                new Color(1f, 0.86f, 0.45f),
                statusSprite,
                preview.StatusChance);
        }

        private GridPosition? GetPreviewSelectedPosition(
            SkillTargetModeData mode,
            BattleUnit target)
        {
            if (mode == null || target == null)
            {
                return null;
            }

            if (IsDirectionalCasterArea(mode))
            {
                return RotateDirectionReference(target.Position);
            }

            return mode.TargetType == TargetType.Single || mode.TargetType == TargetType.MultipleChosen || mode.TargetType == TargetType.Chain
                || mode.TargetType == TargetType.AreaAroundTile
                ? target.Position
                : null;
        }

        private void ClearAllHighlights()
        {
            feedbackRenderer?.ClearOutline(CombatGridFeedbackLayer.ChosenSkillTargets);
            feedbackRenderer?.ClearFill(CombatGridFeedbackLayer.ChosenSkillTargets);
            targetPreview?.Hide();
            feedbackRenderer?.ClearOutline(CombatGridFeedbackLayer.SkillRange);
            feedbackRenderer?.ClearOutline(CombatGridFeedbackLayer.SkillTarget);
            feedbackRenderer?.ClearFill(CombatGridFeedbackLayer.SkillRange);
            feedbackRenderer?.ClearFill(CombatGridFeedbackLayer.SkillTarget);
            ClearRelativeRangeHighlights();
            selectablePositions.Clear();
            absoluteRangePositions.Clear();
            eligibleTargetPositions.Clear();
        }

        private void ClearRelativeRangeHighlights()
        {
            feedbackRenderer?.ClearOutline(CombatGridFeedbackLayer.RelativeRange);
            feedbackRenderer?.ClearFill(CombatGridFeedbackLayer.RelativeRange);
            previewedRelativeCenter = null;
            feedbackRenderer?.ClearOutline(CombatGridFeedbackLayer.AffectedArea);
            feedbackRenderer?.ClearFill(CombatGridFeedbackLayer.AffectedArea);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private void OnDisable()
        {
            judgementFieldSignature=null;
            feedbackRenderer?.ClearOutline(CombatGridFeedbackLayer.JudgementField);
            feedbackRenderer?.ClearFill(CombatGridFeedbackLayer.JudgementField);
            ClearAllHighlights();
        }

        private static bool UsesGridClickOnlyAsConfirmation(SkillTargetModeData targetMode)
        {
            return targetMode.TargetType == TargetType.AreaAroundCaster
                    && !IsDirectionalCasterArea(targetMode)
                || targetMode.TargetType == TargetType.All;
        }

        private static bool IsObjectPlacementOnlySkill(SkillData skill)
        {
            if (skill?.Effects == null || skill.Effects.Count == 0)
            {
                return false;
            }

            foreach (var effect in skill.Effects)
            {
                if (effect?.EffectType != EffectType.CreateObject
                    && effect?.EffectType != EffectType.Summon)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsSupportedTargetType(
            TargetType targetType)
        {
            return targetType == TargetType.Caster
                || targetType == TargetType.Chain
                || targetType == TargetType.MultipleChosen
                || targetType == TargetType.Single
                || targetType == TargetType.All
                || targetType == TargetType.AreaAroundCaster
                || targetType == TargetType.AreaAroundTile;
        }
        private bool IsRelativePreviewCenter(GridPosition position)
        {
            var mode = CurrentTargetMode;

            if (mode.TargetType == TargetType.Single || mode.TargetType == TargetType.MultipleChosen || mode.TargetType == TargetType.Chain) return eligibleTargetPositions.Contains(position);

            if (mode.TargetType == TargetType.AreaAroundTile) return absoluteRangePositions.Contains(position);

            if (IsDirectionalCasterArea(mode)) return selectablePositions.Contains(position);

            return false;
        }

        private static bool IsDirectionalCasterArea(SkillTargetModeData mode)
        {
            if (mode?.TargetType != TargetType.AreaAroundCaster)
            {
                return false;
            }

            return CombatTargetingService.IsDirectionalShape(
                mode.AbsoluteRange?.Shape ?? RangeShape.None);
        }

        private static bool IsCursorDirectedCasterArea(SkillTargetModeData mode)
        {
            if (mode?.TargetType != TargetType.AreaAroundCaster)
            {
                return false;
            }

            var shape = mode.AbsoluteRange?.Shape ?? RangeShape.None;
            return shape == RangeShape.Line
                || shape == RangeShape.Diagonal
                || shape == RangeShape.ThreeRays
                || shape == RangeShape.TwoSidedLine;
        }

        private static bool IsRotatableShape(SkillTargetModeData mode)
        {
            var range = mode?.UseRelativeRange == true
                ? mode.RelativeRange
                : mode?.AbsoluteRange;
            return CombatTargetingService.IsRotatableRelativeShape(
                range?.Shape ?? RangeShape.None);
        }

        private GridPosition RotateDirectionReference(GridPosition reference)
        {
            var casterPosition = sandboxBootstrap?.Session?.ActiveUnit?.Position ?? reference;
            var directionX = Mathf.Clamp(reference.X - casterPosition.X, -1, 1);
            var directionY = Mathf.Clamp(reference.Y - casterPosition.Y, -1, 1);
            for (var index = 0; index < directionalRotationQuarterTurns; index++)
            {
                (directionX, directionY) = (-directionY, directionX);
            }

            return new GridPosition(
                casterPosition.X + directionX,
                casterPosition.Y + directionY);
        }
    }
}
