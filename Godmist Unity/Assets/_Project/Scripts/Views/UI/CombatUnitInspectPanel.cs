using Godmist.Runtime.Combat;
using TMPro;
using UnityEngine;

namespace Godmist.Views.UI
{
    public class CombatUnitInspectPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI infoText;

        private void Awake()
        {
            Hide();
        }

        public void Show(BattleUnit unit)
        {
            if (unit == null
                || unit.Character == null
                || unit.Character.Definition == null)
            {
                Hide();
                return;
            }

            var character = unit.Character;
            var side = unit.IsPlayerSide
                ? "Sojusznik"
                : "Przeciwnik";

            var maxHealth = character.Stats.MaxHealth.GetValue();

            var shield = character.ShieldPool?.TotalShield ?? 0;
            var healthText = shield > 0 
                ? $"{character.CurrentHealth}(+{shield})/{maxHealth:0}"
                : $"{character.CurrentHealth}/{maxHealth:0}";

            infoText.text =
                $"{character.Definition.DisplayName}\n" +
                $"Typ: {side}\n" +
                $"Klasa: {character.ClassData.ClassName}\n" +
                $"Poziom: {character.Level}\n" +
                $"HP: {healthText}\n" +
                $"Pozycja: [{unit.Position.X}, {unit.Position.Y}]";

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}