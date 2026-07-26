using System.Linq;
using Godmist.Views.Combat;
using TMPro;
using UnityEngine;

namespace Godmist.Views.UI
{
    public class CombatHudView : MonoBehaviour
    {
        [SerializeField] private CombatSandboxBootstrap sandboxBootstrap;

        [Header("Top Bar")]
        [SerializeField] private TextMeshProUGUI encounterInfoText;
        [SerializeField] private TextMeshProUGUI turnOrderText;

        [Header("Bottom Panel")]
        [SerializeField] private TextMeshProUGUI activeUnitText;

        private void Update()
        {
            if (sandboxBootstrap == null)
            {
                return;
            }

            var session = sandboxBootstrap.Session;

            if (session == null)
            {
                SetNoSessionState();
                return;
            }

            RefreshEncounterInfo(session);
            RefreshTurnOrder(session);
            RefreshActiveUnit(session);
        }

        private void SetNoSessionState()
        {
            if (encounterInfoText != null)
            {
                encounterInfoText.text = "Stan walki: brak sesji";
            }

            if (turnOrderText != null)
            {
                turnOrderText.text = "Kolejka tur: -";
            }

            if (activeUnitText != null)
            {
                activeUnitText.text = "Brak aktywnej postaci";
            }
        }

        private void RefreshEncounterInfo(Runtime.Combat.CombatSession session)
        {
            if (encounterInfoText == null)
            {
                return;
            }

            encounterInfoText.text = $"Stan walki: {session.Outcome}";
        }

        private void RefreshTurnOrder(Runtime.Combat.CombatSession session)
        {
            if (turnOrderText == null)
            {
                return;
            }

            if (session.TurnOrder == null || session.TurnOrder.Count == 0)
            {
                turnOrderText.text = "Kolejka tur: -";
                return;
            }

            var names = session.TurnOrder
                .Select(unit => unit.Character.Definition.DisplayName);

            turnOrderText.text = $"Kolejka tur: {string.Join(" -> ", names)}";
        }

        private void RefreshActiveUnit(Runtime.Combat.CombatSession session)
        {
            if (activeUnitText == null)
            {
                return;
            }

            if (session.ActiveUnit == null)
            {
                activeUnitText.text = "Brak aktywnej postaci";
                return;
            }

            var unit = session.ActiveUnit;
            var character = unit.Character;
            var resource = character.ResourcePool;
            var shield = character.ShieldPool?.TotalShield ?? 0;
            var healthText = shield > 0 
                ? $"{character.CurrentHealth}(+{shield})/{character.Stats.MaxHealth.GetValue():0}"
                : $"{character.CurrentHealth}/{character.Stats.MaxHealth.GetValue():0}";

            activeUnitText.text =
                $"Aktywna postać: {character.Definition.DisplayName}\n" +
                $"Klasa: {character.ClassData.ClassName}\n" +
                $"HP: {healthText}\n" +
                $"AP: {unit.CurrentActionPoints}/{unit.MaxActionPoints}\n" +
                $"Surowiec: {resource.CurrentValue}/{resource.MaxValue} ({resource.ResourceType})\n" +
                $"Pozycja: [{unit.Position.X}, {unit.Position.Y}]";
        }
    }
}