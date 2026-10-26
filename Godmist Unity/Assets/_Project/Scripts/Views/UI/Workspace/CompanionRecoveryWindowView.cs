using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Game;
using Godmist.Data.Configuration;
using Godmist.Runtime.NPC;
using Godmist.Systems.NPC;
using Godmist.Views.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Godmist.Views.UI.Workspace
{
    public sealed class CompanionRecoveryWindowView : MonoBehaviour
    {
        [SerializeField] private GameObject serviceRoot;
        [SerializeField] private Transform rowsRoot;
        [SerializeField] private CompanionRecoveryRowView rowTemplate;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private TextMeshProUGUI walletText;
        [SerializeField] private TextMeshProUGUI countText;
        [SerializeField] private ScrollRect rowsScroll;
        private readonly List<CompanionRecoveryRowView> rows = new();
        private readonly CompanionRecoveryService service = new();
        private GameSession game;
        private NpcInstance priest;
        private string speakerId;
        private int lastGold, lastHonor;
        public bool Show(NpcInstance npc, string recipient)
        {
            game = GameBootstrap.Instance?.Session;
            if (game == null || npc == null || !game.CharactersById.ContainsKey(recipient)) return false;
            priest = npc; speakerId = recipient;
            serviceRoot.SetActive(true); rowTemplate.gameObject.SetActive(false);
            Refresh();
            if (rowsScroll != null) rowsScroll.verticalNormalizedPosition = 1;
            return true;
        }
        public void Hide() { Clear(); serviceRoot.SetActive(false); }
        private void OnDisable() => Clear();
        private void Update()
        {
            if (game != null && serviceRoot.activeSelf && (game.Gold != lastGold || game.CharactersById[speakerId].Honor != lastHonor)) Refresh();
        }
        private void Clear()
        {
            foreach (var row in rows)
            {
                if (row == null) continue;
                row.gameObject.SetActive(false);
                Destroy(row.gameObject);
            }
            rows.Clear();
        }
        private void Refresh()
        {
            Clear();
            var speaker = game.CharactersById[speakerId];
            lastGold = game.Gold; lastHonor = speaker.Honor;
            walletText.text = $"{game.Gold:N0}";
            foreach (var id in game.PlayerParty.ActiveMemberIds.Concat(game.PlayerParty.BenchMemberIds).ToArray())
            {
                if (!game.CharactersById.TryGetValue(id, out var character) || !service.CanRecover(character)) continue;
                var cost = service.GetCost(character, speaker);
                var breakdown = $"Cena bazowa (poziom 1): {GameDefinesRuntime.GetInt(GameDefineIds.CompanionRecoveryBase, 300):N0}\n"
                    + $"Cena rośnie {GameDefinesRuntime.GetFloat(GameDefineIds.CombatEconomyGrowthBase, 4):0.##}× co {GameDefinesRuntime.GetFloat(GameDefineIds.CombatEconomyLevelSpan, 10):0.##} poziomów\n"
                    + (character.Level > 1 ? $"Cena dla poziomu {character.Level}: {service.GetBaseCost(character):N0}\n" : string.Empty)
                    + $"Honor rozmówcy: {speaker.Honor} · {HonorPriceSystem.GetTierName(speaker.Honor)}\n"
                    + $"Rabat honoru: {HonorPriceSystem.GetDiscountPercent(speaker.Honor):0.#}%\n"
                    + $"Do zapłaty: {cost:N0}";
                var row = Instantiate(rowTemplate, rowsRoot); row.gameObject.SetActive(true);
                row.Bind(character, cost, game.Gold, game.PlayerParty.IsActiveMember(id) || game.PlayerParty.ActiveMemberIds.Count < game.PlayerParty.MaxActiveMembers, breakdown, () => Recover(id));
                rows.Add(row);
            }
            if (countText != null) countText.text = rows.Count.ToString();
            messageText.gameObject.SetActive(rows.Count == 0);
            messageText.text = rows.Count == 0 ? "Brak towarzyszy do odzyskania." : string.Empty;
        }
        private void Recover(string id)
        {
            var recovered = service.TryRecover(game, priest, speakerId, id, out var message);
            Refresh();
            if (!recovered)
            {
                messageText.gameObject.SetActive(true);
                messageText.text = message;
            }
            else CharacterInfoWindowView.RefreshOpenWindows(game);
        }
    }
}
