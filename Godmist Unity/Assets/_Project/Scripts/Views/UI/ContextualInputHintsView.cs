using System.Text;
using Godmist.Core.Enums;
using Godmist.Views.Combat;
using Godmist.Views.Exploration;
using Godmist.Views.Game;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEngine;

namespace Godmist.Views.UI
{
    /// <summary>
    /// A small, runtime-only list of controls which are available in the current
    /// world/combat input state. It deliberately has no background panel so that
    /// the hint never competes with the active HUD or world view.
    /// </summary>
    public sealed class ContextualInputHintsView : MonoBehaviour
    {
        private const string ObjectName = "Contextual Input Hints";
        private readonly StringBuilder builder = new();

        private TextMeshProUGUI text;
        private CombatSandboxBootstrap combatBootstrap;
        private CombatTargetSelectionController combatTargeting;
        private CombatMovementInputController combatMovement;
        private CombatCameraFramer combatCamera;
        private ExplorationPartyController explorationParty;
        private ExplorationCameraFollow explorationCamera;
        private Canvas canvas;

        public static ContextualInputHintsView Ensure(Canvas targetCanvas)
        {
            if (targetCanvas == null) return null;

            var existing = targetCanvas.GetComponent<ContextualInputHintsView>();
            if (existing != null) return existing;

            var view = targetCanvas.gameObject.AddComponent<ContextualInputHintsView>();
            view.canvas = targetCanvas;
            view.CreateText();
            return view;
        }

        public void BindCombat(
            CombatSandboxBootstrap bootstrap,
            CombatTargetSelectionController targeting,
            CombatMovementInputController movement,
            CombatCameraFramer camera)
        {
            combatBootstrap = bootstrap;
            combatTargeting = targeting;
            combatMovement = movement;
            combatCamera = camera;
        }

        public void BindExploration(
            ExplorationPartyController party,
            ExplorationCameraFollow camera)
        {
            explorationParty = party;
            explorationCamera = camera;
        }

        private void Awake()
        {
            canvas ??= GetComponent<Canvas>();
            CreateText();
        }

        private void CreateText()
        {
            if (text != null || canvas == null) return;

            var child = new GameObject(ObjectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            child.transform.SetParent(canvas.transform, false);
            text = child.GetComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 18f;
            text.fontStyle = FontStyles.Bold;
            text.color = new Color(0.93f, 0.88f, 0.72f, 0.92f);
            text.alignment = TextAlignmentOptions.BottomRight;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.lineSpacing = -5f;
            text.outlineWidth = 0.18f;
            text.outlineColor = new Color(0.05f, 0.04f, 0.03f, 0.95f);

            var rect = (RectTransform)child.transform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-28f, 24f);
            rect.sizeDelta = new Vector2(560f, 190f);

            // The HUD already owns the project's font/material choices. Reuse
            // one of its labels when available and retain the TMP default as a
            // fallback for canvases that are created before their HUD children.
            foreach (var candidate in canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (candidate == null || candidate == text || candidate.font == null) continue;
                text.font = candidate.font;
                text.fontSharedMaterial = candidate.fontSharedMaterial ?? candidate.font.material;
                break;
            }

            child.SetActive(false);
        }

        private void Update()
        {
            if (text == null) return;

            if (DevConsoleView.IsOpen
                || UIWorkspaceInputRouter.HasFocusedTextInput()
                || UIWorkspaceInputRouter.HasFocusedContextMenu()
                || UIWorkspaceWindowManager.Instance?.HasOpenWindows == true)
            {
                SetText(string.Empty);
                return;
            }

            var gameSession = GameBootstrap.Instance?.Session;
            if (gameSession?.CurrentMode == GameModeState.Combat || combatBootstrap?.IsOpeningPreview == true)
            {
                RefreshCombat();
                return;
            }

            if (gameSession?.CurrentMode == GameModeState.Exploration)
            {
                RefreshExploration();
                return;
            }

            SetText(string.Empty);
        }

        private void RefreshCombat()
        {
            var session = combatBootstrap?.Session;
            if (session == null || session.Outcome != CombatOutcomeState.Ongoing)
            {
                SetText(string.Empty);
                return;
            }

            builder.Clear();
            var pointerOverUi = UIWorkspaceInputRouter.IsPointerOverUi();
            var targetSelection = combatTargeting?.IsSelectingTarget == true;
            if (targetSelection)
            {
                if (combatTargeting.CanRotateCurrentSelection)
                    Append("R – obrót");
                if (combatTargeting.CanConfirmMultipleTargetSelection)
                    Append("Enter – zatwierdź");
                Append("Esc / PPM – anuluj");
            }
            if (!pointerOverUi && combatMovement?.IsMovementRangeInspectionAvailable == true)
                Append("Ctrl – zasięg ruchu");

            if (combatCamera?.CanAcceptManualInput == true)
            {
                Append("F – skup na aktywnej jednostce");
                Append("Home – arena");
                if (!pointerOverUi)
                {
                    Append("Scroll – zoom");
                    Append("MMB – przesuwanie");
                }
            }

            SetText(builder.ToString());
        }

        private void RefreshExploration()
        {
            if (explorationParty == null || !explorationParty.CanAcceptExplorationInput)
            {
                SetText(string.Empty);
                return;
            }

            builder.Clear();
            var pointerOverUi = UIWorkspaceInputRouter.IsPointerOverUi();
            if (explorationParty.CanCycleLeader)
                Append("Tab – zmiana lidera");
            if (GameBootstrap.Instance?.QuestService != null)
                Append("J – dziennik");
            if(explorationParty.DungeonMinimap!=null)Append("M – mapa odkrytego terenu");
            if (explorationCamera != null)
            {
                Append("F – tryb kamery");
                if (!pointerOverUi)
                {
                    Append("Scroll – zoom");
                    if (!explorationCamera.IsFollowingLeader)
                        Append("PPM – przesuwanie");
                }
            }
            if (explorationParty.HasAvailableInteraction)
                Append("E – interakcja");

            SetText(builder.ToString());
        }

        private void Append(string hint)
        {
            if (builder.Length > 0) builder.Append('\n');
            builder.Append(hint);
        }

        private void SetText(string value)
        {
            var visible = !string.IsNullOrEmpty(value);
            if (text.gameObject.activeSelf != visible) text.gameObject.SetActive(visible);
            if (visible && text.text != value) text.text = value;
        }
    }
}
