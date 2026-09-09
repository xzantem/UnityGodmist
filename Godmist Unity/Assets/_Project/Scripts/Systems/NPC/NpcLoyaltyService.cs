using System;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Configuration;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Runtime.NPC;
using UnityEngine;

namespace Godmist.Systems.NPC
{
    /// <summary>
    /// Applies cumulative gold spending at an NPC to its persistent loyalty.
    /// Thresholds are read from GameDefines so balance stays data-driven.
    /// </summary>
    public sealed class NpcLoyaltyService
    {
        public int InitialLevel
        {
            get
            {
                var maximum = MaximumLevel;
                var configured = GameDefinesRuntime.GetInt(GameDefineIds.NpcLoyaltyInitialLevel, 1);
                return Math.Max(1, Math.Min(maximum, configured));
            }
        }

        public int MaximumLevel => Math.Max(1,
            GameDefinesRuntime.GetInt(GameDefineIds.NpcLoyaltyMaximumLevel, 15));

        public int GetGoldCostForNextLevel(int currentLevel)
        {
            if (currentLevel < InitialLevel || currentLevel >= MaximumLevel) return 0;

            var baseValue = Math.Max(.0001d,
                GameDefinesRuntime.GetFloat(GameDefineIds.NpcLoyaltyThresholdBase, 4f));
            var exponentOffset = GameDefinesRuntime.GetFloat(
                GameDefineIds.NpcLoyaltyThresholdExponentOffset, 4f);
            var levelDivisor = Math.Max(.0001d,
                GameDefinesRuntime.GetFloat(GameDefineIds.NpcLoyaltyThresholdLevelDivisor, 3f));
            var exponent = currentLevel / levelDivisor + exponentOffset;
            var result = Math.Ceiling(Math.Pow(baseValue, exponent));

            if (double.IsNaN(result) || result <= 0d) return 1;
            if (double.IsInfinity(result) || result >= int.MaxValue) return int.MaxValue;
            return Math.Max(1, (int)result);
        }

        public long GetGoldRequiredToReachLevel(int targetLevel)
        {
            if (targetLevel <= InitialLevel) return 0;

            var clampedTarget = Math.Min(targetLevel, MaximumLevel);
            long total = 0;
            for (var level = InitialLevel; level < clampedTarget; level++)
            {
                total = Math.Min(int.MaxValue,
                    total + GetGoldCostForNextLevel(level));
            }

            return total;
        }

        public long GetGoldSpentTowardNextLevel(NpcInstance npc)
        {
            if (npc == null || npc.SpentGoldTotal < 0) return 0;

            var currentLevel = NormalizeLevel(npc.LoyaltyLevel);
            if (currentLevel >= MaximumLevel) return 0;

            var spentAtCurrentLevel = (long)npc.SpentGoldTotal
                - GetGoldRequiredToReachLevel(currentLevel);
            return Math.Max(0, spentAtCurrentLevel);
        }

        public int GetGoldRequiredForNextLevel(NpcInstance npc) =>
            npc == null ? 0 : GetGoldCostForNextLevel(NormalizeLevel(npc.LoyaltyLevel));

        public int GetDiscountPercent(NpcInstance npc) =>
            GetDiscountPercent(npc == null ? InitialLevel : npc.LoyaltyLevel);

        public int GetDiscountPercent(int loyaltyLevel)
        {
            var normalizedLevel = NormalizeLevel(loyaltyLevel);
            var discount = 0f;
            var thresholds = new[]
            {
                GameDefinesRuntime.GetInt(GameDefineIds.NpcLoyaltyDiscountLevel1, 2),
                GameDefinesRuntime.GetInt(GameDefineIds.NpcLoyaltyDiscountLevel2, 4),
                GameDefinesRuntime.GetInt(GameDefineIds.NpcLoyaltyDiscountLevel3, 7),
                GameDefinesRuntime.GetInt(GameDefineIds.NpcLoyaltyDiscountLevel4, 9),
                GameDefinesRuntime.GetInt(GameDefineIds.NpcLoyaltyDiscountLevel5, 12)
            };
            var discounts = new[]
            {
                GameDefinesRuntime.GetFloat(GameDefineIds.NpcLoyaltyDiscountPercent1, .01f),
                GameDefinesRuntime.GetFloat(GameDefineIds.NpcLoyaltyDiscountPercent2, .04f),
                GameDefinesRuntime.GetFloat(GameDefineIds.NpcLoyaltyDiscountPercent3, .09f),
                GameDefinesRuntime.GetFloat(GameDefineIds.NpcLoyaltyDiscountPercent4, .16f),
                GameDefinesRuntime.GetFloat(GameDefineIds.NpcLoyaltyDiscountPercent5, .25f)
            };

            for (var index = 0; index < thresholds.Length; index++)
            {
                if (normalizedLevel < thresholds[index]) continue;
                discount = Math.Max(discount, discounts[index]);
            }

            return Math.Max(0, Math.Min(100, (int)Math.Round(discount * 100f)));
        }

        public int GetDiscountedGoldCost(NpcInstance npc, int baseCost)
        {
            if (baseCost <= 0) return 0;

            var discount = GetDiscountPercent(npc) / 100d;
            var result = Math.Floor(baseCost * (1d - discount));
            return Math.Max(1, result >= int.MaxValue ? int.MaxValue : (int)result);
        }

        public bool IsMaterialTradeUnlocked(
            NpcInstance npc,
            ItemDefinition material,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (material == null || material.ItemType != ItemType.Material) return true;

            var requiredLevel = GetMaterialTradeUnlockLevel(material.BaseRarity);
            var currentLevel = NormalizeLevel(npc?.LoyaltyLevel ?? InitialLevel);
            if (currentLevel >= requiredLevel) return true;

            failureReason = $"Odblokuje się na lojalności {requiredLevel}. "
                + $"Obecna lojalność: {currentLevel}.";
            return false;
        }

        public int GetMaterialTradeUnlockLevel(RarityTier rarity)
        {
            if (rarity >= RarityTier.Legendary)
            {
                return Math.Max(1, GameDefinesRuntime.GetInt(
                    GameDefineIds.NpcLoyaltyMaterialLegendaryUnlockLevel, 13));
            }

            if (rarity >= RarityTier.Ancient)
            {
                return Math.Max(1, GameDefinesRuntime.GetInt(
                    GameDefineIds.NpcLoyaltyMaterialAncientUnlockLevel, 10));
            }

            if (rarity >= RarityTier.Rare)
            {
                return Math.Max(1, GameDefinesRuntime.GetInt(
                    GameDefineIds.NpcLoyaltyMaterialRareUnlockLevel, 8));
            }

            if (rarity >= RarityTier.Uncommon)
            {
                return Math.Max(1, GameDefinesRuntime.GetInt(
                    GameDefineIds.NpcLoyaltyMaterialUncommonUnlockLevel, 5));
            }

            return Math.Max(1, GameDefinesRuntime.GetInt(
                GameDefineIds.NpcLoyaltyMaterialCommonUnlockLevel, 1));
        }

        public bool TryRecordGoldSpent(NpcInstance npc, int amount)
        {
            if (npc == null || amount <= 0 || npc.SpentGoldTotal < 0) return false;

            var total = Math.Min(int.MaxValue, (long)npc.SpentGoldTotal + amount);
            npc.SpentGoldTotal = (int)total;

            var currentLevel = NormalizeLevel(npc.LoyaltyLevel);
            while (currentLevel < MaximumLevel
                && total >= GetGoldRequiredToReachLevel(currentLevel + 1))
            {
                currentLevel++;
            }

            npc.LoyaltyLevel = currentLevel;
            return true;
        }

        private int NormalizeLevel(int level) =>
            Math.Max(InitialLevel, Math.Min(MaximumLevel, level));
    }

    public sealed class PriestHealingPreview
    {
        public int HealableMembers;
        public int MissingHealth;
        public bool CanHeal;
        public string FailureReason = string.Empty;
    }

    /// <summary>
    /// Free city service offered by a priest. It heals every living active party
    /// member to maximum health; dead and downed characters are deliberately not
    /// revived by a healing-only service.
    /// </summary>
    public sealed class PriestHealingService
    {
        public PriestHealingPreview CreatePreview(GameSession session, NpcInstance priest)
        {
            var preview = new PriestHealingPreview();
            if (priest?.Definition?.GetRole<PriestRoleData>() == null)
            {
                preview.FailureReason = "Wybrany NPC nie oferuje posługi kapłańskiej.";
                return preview;
            }

            if (session == null)
            {
                preview.FailureReason = "Brak aktywnej drużyny.";
                return preview;
            }

            foreach (var character in GetHealableMembers(session))
            {
                var maxHealth = GetMaxHealth(character);
                if (maxHealth < 1) continue;

                preview.HealableMembers++;
                preview.MissingHealth += Mathf.Max(0, maxHealth - character.CurrentHealth);
            }

            preview.CanHeal = preview.MissingHealth > 0;
            if (!preview.CanHeal)
            {
                preview.FailureReason = preview.HealableMembers > 0
                    ? "Drużyna jest już w pełni zdrowa."
                    : "Brak żywych członków aktywnej drużyny do uzdrowienia.";
            }

            return preview;
        }

        public bool TryHealActiveParty(
            GameSession session,
            NpcInstance priest,
            out int restoredHealth,
            out string failureReason)
        {
            restoredHealth = 0;
            failureReason = string.Empty;

            if (priest?.Definition?.GetRole<PriestRoleData>() == null)
            {
                failureReason = "Wybrany NPC nie oferuje posługi kapłańskiej.";
                return false;
            }

            return TryHealActiveParty(session, out restoredHealth, out failureReason);
        }

        public bool TryHealActiveParty(
            GameSession session,
            out int restoredHealth,
            out string failureReason)
        {
            restoredHealth = 0;
            failureReason = string.Empty;
            if (session == null)
            {
                failureReason = "Brak aktywnej drużyny.";
                return false;
            }

            foreach (var character in GetHealableMembers(session))
            {
                var maxHealth = GetMaxHealth(character);
                if (maxHealth < 1) continue;

                var previousHealth = character.CurrentHealth;
                character.CurrentHealth = maxHealth;
                restoredHealth += Mathf.Max(0, maxHealth - previousHealth);
            }

            return restoredHealth > 0;
        }

        private static CharacterInstance[] GetHealableMembers(GameSession session)
        {
            return session.PlayerParty.ActiveMemberIds
                .Where(session.CharactersById.ContainsKey)
                .Select(characterId => session.CharactersById[characterId])
                .Where(character => character != null
                    && character.LifeState == CharacterLifeState.Alive)
                .ToArray();
        }

        private static int GetMaxHealth(CharacterInstance character)
        {
            return character?.Stats == null
                ? 0
                : Mathf.Max(0, Mathf.RoundToInt(character.Stats.MaxHealth.GetValue()));
        }
    }

    /// <summary>Runs explicitly authored party actions selected in dialogue.</summary>
    public sealed class DialoguePartyActionSystem : IDisposable
    {
        private readonly DialogueService dialogueService;
        private readonly GameSession session;
        private readonly PriestHealingService healingService = new();

        public DialoguePartyActionSystem(
            DialogueService nextDialogueService,
            GameSession nextSession)
        {
            dialogueService = nextDialogueService
                ?? throw new ArgumentNullException(nameof(nextDialogueService));
            session = nextSession ?? throw new ArgumentNullException(nameof(nextSession));
            dialogueService.DialogueChoiceSelected += HandleDialogueChoiceSelected;
        }

        public void Dispose() =>
            dialogueService.DialogueChoiceSelected -= HandleDialogueChoiceSelected;

        private void HandleDialogueChoiceSelected(
            DialogueSession dialogue,
            DialogueChoiceData choice)
        {
            if (choice?.PartyAction == DialoguePartyAction.HealActiveParty)
            {
                healingService.TryHealActiveParty(session, out _, out _);
            }
        }
    }

}
