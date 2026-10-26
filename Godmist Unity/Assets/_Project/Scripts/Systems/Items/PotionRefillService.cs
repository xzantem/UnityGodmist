using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;

namespace Godmist.Systems.Items
{
    public sealed class PotionRefillRequest
    {
        public string PotionInstanceId = string.Empty;
        public string ConsumableInstanceId = string.Empty;
        public string RecipientCharacterId = string.Empty;
        public string MediumDefinitionId = string.Empty;
    }

    public sealed class RefillMediumOption
    {
        public ItemDefinition Medium;
        public int Tier;
        public AlchemyMaterialCost Cost;
    }

    public sealed class PotionRefillPreview
    {
        public ConsumableItemInstance Consumable;
        public ItemDefinition Medium;
        public IReadOnlyList<RefillMediumOption> MediumOptions = new List<RefillMediumOption>();
        public int CurrentCharges;
        public int MaxCharges;
        public bool CanRefill;
        public string FailureReason = string.Empty;

        // Compatibility aliases for the initial potion-only prototype.
        public ConsumableItemInstance Potion => Consumable;
        public ItemDefinition Alcohol => Medium;
    }

    public sealed class PotionRefillService
    {
        public PotionRefillPreview CreatePreview(
            GameSession session,
            NpcInstance alchemist,
            PotionRefillRequest request)
        {
            var preview = new PotionRefillPreview();
            var role = alchemist?.Definition?.GetRole<AlchemistRoleData>();
            if (role == null)
            {
                preview.FailureReason = "Wybrany NPC nie oferuje napełniania consumabli.";
                return preview;
            }

            if (!TryFindConsumable(session, request, out var consumable, out _))
            {
                preview.FailureReason = "Wybierz potion albo bombę należącą do aktywnej drużyny.";
                return preview;
            }

            preview.Consumable = consumable;
            preview.CurrentCharges = consumable.CurrentCharges;
            preview.MaxCharges = consumable.MaxCharges;

            if (consumable.CurrentCharges >= consumable.MaxCharges)
            {
                preview.FailureReason = consumable.IsBomb ? "Ta bomba jest już pełna." : "Ta mikstura jest już pełna.";
                return preview;
            }

            preview.MediumOptions = GetCompatibleMediumOptions(session, alchemist, consumable);
            preview.Medium = ResolveSelectedMedium(preview.MediumOptions, request?.MediumDefinitionId);
            if (preview.Medium == null)
            {
                preview.FailureReason = consumable.IsBomb
                    ? "Wybierz dostępny proch strzelniczy."
                    : "Wybierz alkohol lub składnik alchemiczny.";
                return preview;
            }

            var selectedOption = preview.MediumOptions.FirstOrDefault(option => option.Medium == preview.Medium);
            if (selectedOption?.Cost?.MissingAmount > 0)
            {
                preview.FailureReason = consumable.IsBomb
                    ? "Brakuje wybranego prochu strzelniczego."
                    : "Brakuje wybranego materiału do napełnienia.";
                return preview;
            }

            preview.CanRefill = true;
            return preview;
        }

        public bool TryRefill(
            GameSession session,
            NpcInstance alchemist,
            PotionRefillRequest request,
            out string failureReason)
        {
            failureReason = string.Empty;
            var preview = CreatePreview(session, alchemist, request);
            if (!preview.CanRefill)
            {
                failureReason = preview.FailureReason;
                return false;
            }

            if (!TryRemovePartyItem(session, preview.Medium.Id, out _))
            {
                failureReason = preview.Consumable.IsBomb
                    ? "Nie udało się zużyć prochu strzelniczego do napełnienia."
                    : "Nie udało się zużyć materiału do napełnienia.";
                return false;
            }

            preview.Consumable.CurrentCharges = preview.Consumable.MaxCharges;
            return true;
        }

        public IReadOnlyList<ConsumableItemInstance> GetPartyPotions(GameSession session) =>
            GetPartyConsumables(session, ConsumableUseType.Potion);

        public IReadOnlyList<ConsumableItemInstance> GetPartyBombs(GameSession session) =>
            GetPartyConsumables(session, ConsumableUseType.Bomb);

        public IReadOnlyList<ConsumableItemInstance> GetPartyConsumables(
            GameSession session,
            ConsumableUseType useType)
        {
            if (session == null) return new List<ConsumableItemInstance>();
            return session.PlayerParty.ActiveMemberIds
                .Where(session.CharactersById.ContainsKey)
                .SelectMany(characterId => session.CharactersById[characterId].Inventory?.Items
                    ?? new List<ItemInstance>())
                .OfType<ConsumableItemInstance>()
                .Where(item => item.ConsumableDefinition?.UseType == useType)
                .ToList();
        }

        public IReadOnlyList<RefillMediumOption> GetCompatibleMediumOptions(
            GameSession session,
            NpcInstance alchemist,
            ConsumableItemInstance consumable)
        {
            var role = alchemist?.Definition?.GetRole<AlchemistRoleData>();
            if (role == null || consumable == null) return new List<RefillMediumOption>();

            var candidates = consumable.IsBomb ? role.BombGunpowders : role.PotionAlcohols;
            var requiredTier = AlchemyRuntimeRules.GetRequiredMediumTier(consumable.ItemLevel);
            var costService = new AlchemyCraftingService();
            var options = candidates?
                .Where(option => option?.Material != null && option.Tier >= requiredTier)
                .OrderBy(option => option.Tier)
                .Select(option => new RefillMediumOption
                {
                    Medium = option.Material,
                    Tier = option.Tier,
                    Cost = costService.GetDraftMaterialCosts(session, alchemist, new AlchemyCraftRequest
                    {
                        Medium = option.Material
                    }).FirstOrDefault()
                })
                .ToList()
                ?? new List<RefillMediumOption>();
            if (!consumable.IsBomb)
                foreach (var ingredient in costService.GetIngredientOptions(alchemist))
                {
                    if (options.Any(option => option.Medium.Id == ingredient.Material.Id)) continue;
                    options.Add(new RefillMediumOption
                    {
                        Medium = ingredient.Material,
                        Cost = costService.GetDraftMaterialCosts(session, alchemist, new AlchemyCraftRequest
                        { Ingredients = new List<ItemDefinition> { ingredient.Material } }).FirstOrDefault()
                    });
                }
            return options;
        }

        public bool TryBuyMedium(
            GameSession session,
            NpcInstance alchemist,
            PotionRefillRequest request,
            out int paidGold,
            out string failureReason)
        {
            paidGold = 0;
            failureReason = string.Empty;
            if (!TryFindConsumable(session, request, out var consumable, out _))
            {
                failureReason = "Wybierz przedmiot do napełnienia.";
                return false;
            }

            var option = GetCompatibleMediumOptions(session, alchemist, consumable)
                .FirstOrDefault(candidate => candidate.Medium?.Id == request?.MediumDefinitionId);
            if (option?.Medium == null)
            {
                failureReason = "Wybierz legalny materiał do napełnienia.";
                return false;
            }

            return new AlchemyCraftingService().TryBuyMissingMaterial(session, alchemist,
                new AlchemyCraftRequest
                {
                    Medium = option.Medium,
                    RecipientCharacterId = request.RecipientCharacterId
                }, option.Medium.Id, out paidGold, out failureReason);
        }

        private static bool TryFindConsumable(
            GameSession session,
            PotionRefillRequest request,
            out ConsumableItemInstance consumable,
            out CharacterInstance owner)
        {
            consumable = null;
            owner = null;
            var instanceId = string.IsNullOrWhiteSpace(request?.ConsumableInstanceId)
                ? request?.PotionInstanceId
                : request.ConsumableInstanceId;
            if (session == null || string.IsNullOrWhiteSpace(instanceId)) return false;

            foreach (var characterId in session.PlayerParty.ActiveMemberIds)
            {
                if (!session.CharactersById.TryGetValue(characterId, out var character)) continue;
                consumable = character.Inventory?.Items
                    .OfType<ConsumableItemInstance>()
                    .FirstOrDefault(item => item.InstanceId == instanceId
                        && (item.ConsumableDefinition?.UseType == ConsumableUseType.Potion
                            || item.ConsumableDefinition?.UseType == ConsumableUseType.Bomb));
                if (consumable == null) continue;
                owner = character;
                return true;
            }

            return false;
        }

        private static ItemDefinition ResolveSelectedMedium(
            IReadOnlyList<RefillMediumOption> options,
            string selectedMediumId)
        {
            if (options == null || string.IsNullOrWhiteSpace(selectedMediumId)) return null;
            return options.FirstOrDefault(option => option.Medium?.Id == selectedMediumId)?.Medium;
        }

        private static bool TryRemovePartyItem(
            GameSession session,
            string definitionId,
            out ItemInstance removed)
        {
            removed = null;
            if (session == null) return false;
            foreach (var characterId in session.PlayerParty.ActiveMemberIds)
            {
                if (!session.CharactersById.TryGetValue(characterId, out var character)) continue;
                var item = character.Inventory?.Items?.FirstOrDefault(candidate =>
                    candidate?.Definition?.Id == definitionId);
                if (item == null) continue;
                character.Inventory.Items.Remove(item);
                removed = item;
                return true;
            }

            return false;
        }
    }
}
