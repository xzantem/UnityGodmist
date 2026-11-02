using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.World;
using Godmist.Runtime.Game;
using Godmist.Systems.Items;
using UnityEngine;

namespace Godmist.Systems.Characters
{
    public sealed class NewCampaignService
    {
        public const string InitializedFlag = "campaign.start.initialized.v1";

        // Prepare a separate session: a bad profile never partially changes the current campaign.
        public bool TryCreate(NewCampaignDefinition definition, out GameSession session, out string error)
        {
            session = null;
            error = "Brak poprawnej konfiguracji nowej kampanii.";
            if (definition == null || definition.StartingGold < 0 || definition.Characters == null
                || definition.Characters.Count != 4) return false;
            var candidate = new GameSession(GameModeState.Menu);
            var factory = new CharacterInstanceFactory();
            var equipment = new EquipmentGenerationService();
            foreach (var entry in definition.Characters)
            {
                if (entry?.Character == null || entry.Class == null
                    || string.IsNullOrWhiteSpace(entry.Character.Id)
                    || string.IsNullOrWhiteSpace(entry.Class.Id)
                    || entry.Weapon?.RequiredClass != entry.Class || entry.Armor?.RequiredClass != entry.Class
                    || entry.Weapon.ItemType != ItemType.Weapon || entry.Armor.ItemType != ItemType.Armor
                    || entry.WeaponParts == null || entry.ArmorParts == null
                    || entry.WeaponParts.Concat(entry.ArmorParts).Any(p => p == null
                        || p.LevelBracket != EquipmentPartLevelBracket.Level1 || !p.IsStarterOnly)) return false;
                var weapon = equipment.GenerateFromSelectedParts(entry.Weapon, entry.WeaponParts, EquipmentQuality.Normal, RarityTier.Common);
                var armor = equipment.GenerateFromSelectedParts(entry.Armor, entry.ArmorParts, EquipmentQuality.Normal, RarityTier.Common);
                if (weapon == null || armor == null) return false;
                var character = factory.Create(entry.Character, entry.Class, 1);
                character.Equipment.SetItem(EquipmentSlot.Weapon, weapon);
                character.Equipment.SetItem(EquipmentSlot.Armor, armor);
                new CharacterStatPipelineSystem().Refresh(character);
                character.CurrentHealth = character.MaximumHealth;
                if (character.ResourcePool.ResourceType == ResourceType.Mana)
                    character.ResourcePool.CurrentValue = character.ResourcePool.MaxValue;
                if (!candidate.TryRegisterCharacter(character)
                    || !candidate.TryAddCharacterToActiveParty(character.CharacterId)) return false;
            }
            candidate.TrySetActiveLeader(definition.Characters[0].Character.Id);
            if (definition.StartingGold > 0) candidate.TryAddGold(definition.StartingGold);
            candidate.WorldState.TrySetFlag(InitializedFlag, "true");
            session = candidate;
            error = string.Empty;
            return true;
        }
    }
}
