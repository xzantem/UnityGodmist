using Godmist.Views.Combat;
using TMPro;
using UnityEngine;

namespace Godmist.Views.UI
{
    public class CombatDebugPanel : MonoBehaviour
    {
        [SerializeField] private CombatSandboxBootstrap sandboxBootstrap;
        [SerializeField] private TextMeshProUGUI debugText;

        private void Update()
        {
            if (sandboxBootstrap == null || debugText == null)
            {
                return;
            }

            var session = sandboxBootstrap.Session;

            if (session == null || session.ActiveUnit == null)
            {
                debugText.text = "Brak aktywnej sesji walki.";
                return;
            }

            var unit = session.ActiveUnit;
            var character = unit.Character;
            var resource = character.ResourcePool;

            debugText.text =
                $"Stan walki: {session.Outcome}\n" +
                $"Aktywna postać: {character.Definition.DisplayName}\n" +
                $"Klasa: {character.ClassData.ClassName}\n" +
                $"HP: {character.CurrentHealth}/{character.Stats.MaxHealth.GetValue():0}\n" +
                $"AP: {unit.CurrentActionPoints}/{unit.MaxActionPoints}\n" +
                $"Surowiec: {resource.CurrentValue}/{resource.MaxValue} ({resource.ResourceType})\n" +
                $"Pozycja: [{unit.Position.X}, {unit.Position.Y}]";
        }
    }
}