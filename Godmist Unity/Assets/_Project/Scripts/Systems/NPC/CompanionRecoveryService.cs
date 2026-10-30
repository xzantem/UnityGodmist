using System;
using Godmist.Data.Configuration;
using Godmist.Core.Enums;
using Godmist.Data.NPC;
using Godmist.Runtime.Game;
using Godmist.Runtime.Characters;
using Godmist.Runtime.NPC;
using Godmist.Systems.Combat;
using UnityEngine;
namespace Godmist.Systems.NPC
{
    public sealed class CompanionRecoveryService
    {
        public bool CanRecover(CharacterInstance character) => character != null
            && character.Definition?.IsMainCharacter != true
            && character.AvailabilityState != CharacterAvailabilityState.StoryLocked
            && (character.LifeState == CharacterLifeState.Dead
                || character.AvailabilityState == CharacterAvailabilityState.Abandoned
                || character.AvailabilityState == CharacterAvailabilityState.Recoverable);

        public int GetBaseCost(CharacterInstance character) => character == null ? 0 :
            CombatEconomyScaling.Scale(GameDefinesRuntime.GetInt(GameDefineIds.CompanionRecoveryBase, 300), character.Level);

        public int GetCost(CharacterInstance character, CharacterInstance speaker)
        {
            if (character == null) return 0;
            var baseCost = GetBaseCost(character);
            var cost = Math.Ceiling(baseCost * (1d - HonorPriceSystem.GetDiscountPercent(speaker?.Honor ?? 0) / 100d));
            return (int)Math.Clamp(cost, 0d, int.MaxValue);
        }

        public bool TryRecover(GameSession game, NpcInstance priest, string speakerId, string characterId, out string message)
        {
            message = "Nie można odzyskać tej postaci.";
            if (game == null || game.IsDefeated || game.HasCombat
                || priest?.Definition?.GetRole<PriestRoleData>() == null
                || !game.NpcsById.TryGetValue(priest.NpcId, out var registered) || registered != priest
                || !game.PlayerParty.IsActiveMember(speakerId)
                || !game.CharactersById.TryGetValue(speakerId, out var speaker) || speaker.LifeState != CharacterLifeState.Alive
                || !game.PlayerParty.ContainsMember(characterId)
                || !game.CharactersById.TryGetValue(characterId, out var character) || !CanRecover(character)
                || character.Stats == null) return false;
            var cost = GetCost(character, speaker);
            if (game.Gold < cost) { message = "Za mało złota."; return false; }
            if (cost > 0 && !game.TrySpendGold(cost)) return false;
            if (cost > 0) new NpcEconomyService().TryReceiveGold(priest, cost);
            new PassiveEffectLifecycleSystem().RemoveCombatScopedEffects(character);
            character.LifeState = CharacterLifeState.Alive;
            character.AvailabilityState = CharacterAvailabilityState.Available;
            character.CurrentHealth = character.MaximumHealth;
            if (character.ResourcePool != null)
            {
                character.ResourcePool.MaxValue = Math.Max(0, Mathf.RoundToInt(character.Stats.MaxResource.GetValue()));
                var previousResource = character.ResourcePool.CurrentValue;
                character.ResourcePool.CurrentValue = character.ResourcePool.MaxValue;
                Godmist.Systems.Combat.MomentumResourceRule.RefreshSpeedAfterChange(character, previousResource);
                character.ResourcePool.HasStartedTurn = character.ResourcePool.HasGainedResourceSinceLastTurn = false;
                character.ResourcePool.FractionalGainRemainder = 0;
            }
            game.PlayerParty.TryMoveBenchMemberToActive(characterId);
            game.NotifyPartyRosterChanged();
            message = game.PlayerParty.IsActiveMember(characterId)
                ? "Towarzysz wrócił do drużyny." : "Towarzysz jest gotowy w rezerwie.";
            return true;
        }
    }
}
