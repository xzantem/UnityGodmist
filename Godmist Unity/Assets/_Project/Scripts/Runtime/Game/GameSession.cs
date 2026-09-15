using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Runtime.Quests;
using UnityEngine;

namespace Godmist.Runtime.Game
{
    public class GameSession
    {
        private readonly Dictionary<string, CharacterInstance> charactersById = new();
        private readonly Dictionary<string, NpcInstance> npcsById = new();
        private readonly Dictionary<string, QuestInstance> questsById = new();
        private string trackedQuestId = string.Empty;
        private readonly List<ItemInstance> pendingLoot = new();
        public GameModeState CurrentMode { get; private set; }
        public string CampaignId { get; }
        public Party PlayerParty { get; } = new();
        // Campaign-wide, weightless storage. Ownership stays with the session rather
        // than a scene object so it persists across locations and saves.
        public Inventory Stash { get; } = new();
        public WorldState WorldState { get; } = new();
        public IReadOnlyDictionary<string, CharacterInstance> CharactersById => charactersById;
        public IReadOnlyDictionary<string, NpcInstance> NpcsById => npcsById;
        public IReadOnlyDictionary<string, QuestInstance> QuestsById => questsById;
        public string TrackedQuestId => trackedQuestId;
        public DungeonInstance ActiveDungeon { get; private set; }
        public bool HasExplorationPosition { get; private set; }
        public Vector2 ExplorationPosition { get; private set; }
        public CombatSession ActiveCombat { get; private set; }
        private readonly List<CombatSession> combats = new();
        public IReadOnlyList<CombatSession> Combats => combats;
        public bool HasCombat => combats.Count > 0;
        public bool IsDefeated { get; private set; }
        public event Action PartyRosterChanged;
        public void NotifyPartyRosterChanged() => PartyRosterChanged?.Invoke();
        public void MarkDefeated() { IsDefeated = true; TrySetMode(GameModeState.Menu); }
        public bool IsCharacterInCombat(string characterId) =>
            combats.Exists(combat => !combat.IsSeamless) || GetCombatForCharacter(characterId) != null;

        public CombatSession GetCombatForCharacter(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId)) return null;
            return combats.Find(combat => combat.Units.Exists(unit =>
                unit?.IsPlayerSide == true && unit.Character?.CharacterId == characterId));
        }

        public bool TryRegisterDungeonCombat(CombatSession combat)
        {
            var context = combat?.DungeonContext;
            if (context == null || ActiveDungeon == null
                || context.DungeonId != ActiveDungeon.DungeonId
                || context.FloorIndex != ActiveDungeon.CurrentFloorIndex
                || combats.Exists(existing => existing.CombatId == combat.CombatId
                    || (!combat.IsDungeonWide && existing.DungeonContext?.EncounterRoomId == context.EncounterRoomId))) return false;
            foreach (var unit in combat.Units)
                if (unit?.IsPlayerSide == true && GetCombatForCharacter(unit.Character?.CharacterId) != null)
                    return false;
            combats.Add(combat);
            combat.ExternalPositionOccupied = position => IsOccupiedInOtherCombat(combat, position);
            return true;
        }

        public bool IsOccupiedInOtherCombat(CombatSession source, GridPosition position)
        {
            var origin = source.DungeonContext.WorldBounds.min;
            var world = new Vector2Int(origin.x + position.X, origin.y + position.Y);
            foreach (var other in combats)
            {
                if (other == source || other.DungeonContext == null || other.DungeonContext.FloorIndex != source.DungeonContext.FloorIndex) continue;
                var otherOrigin = other.DungeonContext.WorldBounds.min;
                if (other.Units.Exists(unit => unit != null
                    && otherOrigin.x + unit.Position.X == world.x
                    && otherOrigin.y + unit.Position.Y == world.y
                    && unit.Position.Level == position.Level && unit.Position.StairStep == position.StairStep
                    && (!position.IsOnStairs || unit.Position.Stair.Equals(position.Stair)))) return true;
            }
            return false;
        }

        public bool TryFocusCombat(CombatSession combat)
        {
            if (combat != null && !combats.Contains(combat)) return false;
            ActiveCombat = combat;
            ActiveDungeonCombatContext = combat?.DungeonContext;
            return true;
        }

        public bool TryRemoveCombat(CombatSession combat)
        {
            if (combat == null || !combats.Remove(combat)) return false;
            combat.ExternalPositionOccupied = null;
            if (ActiveCombat == combat) TryFocusCombat(null);
            return true;
        }
        public DungeonCombatContext ActiveDungeonCombatContext { get; private set; }
        public int Gold { get; private set; }
        public IReadOnlyList<ItemInstance> PendingLoot => pendingLoot;
        public CombatRewardResult LastCombatRewards { get; private set; }

        public event Action<GameModeState, GameModeState> ModeChanged;

        public event Action<NpcInstance> NpcInteractionRequested;
        public event Action<string> TrackedQuestChanged;

        public GameSession(
            GameModeState initialMode = GameModeState.Menu,
            string campaignId = null)
        {
            CurrentMode = initialMode;

            CampaignId = string.IsNullOrWhiteSpace(campaignId)
                ? Guid.NewGuid().ToString("N")
                : campaignId.Trim();
        }

        public bool TrySetMode(GameModeState nextMode)
        {
            if (IsDefeated && nextMode != GameModeState.Menu && nextMode != GameModeState.Loading) return false;
            if (CurrentMode == nextMode) return false;

            var previousMode = CurrentMode;
            CurrentMode = nextMode;

            ModeChanged?.Invoke(previousMode, nextMode);
            return true;
        }

        public bool TryRegisterCharacter(CharacterInstance character)
        {
            if (character == null || string.IsNullOrWhiteSpace(character.CharacterId) 
                || charactersById.ContainsKey(character.CharacterId)) return false;

            charactersById.Add(character.CharacterId, character);
            return true;
        }

        public bool TryRegisterNpc(NpcInstance npc)
        {
            if (npc == null
                || string.IsNullOrWhiteSpace(npc.NpcId)
                || npcsById.ContainsKey(npc.NpcId))
            {
                return false;
            }

            npcsById.Add(npc.NpcId, npc);
            return true;
        }

        public bool TryRegisterQuest(QuestInstance quest)
        {
            if (quest == null
                || string.IsNullOrWhiteSpace(quest.QuestId)
                || questsById.ContainsKey(quest.QuestId))
            {
                return false;
            }

            questsById.Add(quest.QuestId, quest);
            return true;
        }

        public bool TryGetQuest(
            string questId,
            out QuestInstance quest)
        {
            quest = null;

            return !string.IsNullOrWhiteSpace(questId)
                && questsById.TryGetValue(questId, out quest);
        }

        public bool TrySetTrackedQuest(string questId)
        {
            if (!TryGetQuest(questId, out var quest)
                || quest.Status != QuestStatus.Active
                || trackedQuestId == questId)
            {
                return false;
            }

            trackedQuestId = questId;
            TrackedQuestChanged?.Invoke(trackedQuestId);
            return true;
        }

        public bool TryClearTrackedQuest(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId)
                || trackedQuestId != questId)
            {
                return false;
            }

            trackedQuestId = string.Empty;
            TrackedQuestChanged?.Invoke(trackedQuestId);
            return true;
        }

        public bool TryRequestNpcInteraction(string npcId)
        {
            if (string.IsNullOrWhiteSpace(npcId)
                || !npcsById.TryGetValue(npcId, out var npc))
            {
                return false;
            }

            NpcInteractionRequested?.Invoke(npc);
            return true;
        }

        public bool TryAddCharacterToActiveParty(string characterId)
        {
            if (!CanJoinActiveParty(characterId)) return false;

            var added = PlayerParty.TryAddActiveMember(characterId);

            if (added && string.IsNullOrEmpty(PlayerParty.ActiveLeaderId))
            {
                TryAssignFirstAvailableLeader();
            }

            return added;
        }

        public bool TryAddCharacterToBench(string characterId)
        {
            return charactersById.ContainsKey(characterId) && PlayerParty.TryAddBenchMember(characterId);
        }

        public bool TryMoveActiveMemberToBench(string characterId)
        {
            var wasLeader = PlayerParty.ActiveLeaderId == characterId;

            if (!PlayerParty.TryMoveActiveMemberToBench(characterId)) return false;

            if (wasLeader)
            {
                TryAssignFirstAvailableLeader();
            }

            return true;
        }

        public bool TryMoveBenchMemberToActive(string characterId)
        {
            if (!CanJoinActiveParty(characterId)) return false;

            var moved = PlayerParty.TryMoveBenchMemberToActive(characterId);

            if (moved && string.IsNullOrEmpty(PlayerParty.ActiveLeaderId))
            {
                TryAssignFirstAvailableLeader();
            }

            return moved;
        }

        public bool TrySetActiveLeader(string characterId)
        {
            if (!CanLeadParty(characterId)) return false;

            return PlayerParty.TrySetActiveLeader(characterId);
        }

        public bool TrySetExplorationPosition(Vector2 position)
        {
            if (float.IsNaN(position.x)
                || float.IsNaN(position.y)
                || float.IsInfinity(position.x)
                || float.IsInfinity(position.y))
            {
                return false;
            }

            ExplorationPosition = position;
            HasExplorationPosition = true;
            return true;
        }

        public bool TrySetActiveDungeon(DungeonInstance dungeon)
        {
            if (HasCombat) return false;
            if (dungeon == null || string.IsNullOrWhiteSpace(dungeon.DungeonId)) return false;
            if (ReferenceEquals(ActiveDungeon, dungeon)) return false;

            ActiveDungeon = dungeon;
            return true;
        }

        public bool TryClearActiveDungeon()
        {
            if (ActiveDungeon == null || HasCombat) return false;

            ActiveDungeon = null;
            return true;
        }

        public bool TrySetActiveDungeonCombatContext(
            DungeonCombatContext context)
        {
            if (context == null
                || ActiveDungeonCombatContext != null
                || ActiveDungeon == null
                || context.DungeonId != ActiveDungeon.DungeonId)
            {
                return false;
            }

            ActiveDungeonCombatContext = context;
            return true;
        }

        public bool TryClearActiveDungeonCombatContext()
        {
            if (ActiveDungeonCombatContext == null) return false;

            ActiveDungeonCombatContext = null;
            return true;
        }

        public bool TrySetActiveCombat(CombatSession combat)
        {
            if (combat == null || string.IsNullOrWhiteSpace(combat.CombatId)) return false;
            if (HasCombat) return false;

            ActiveCombat = combat;
            combat.DungeonContext ??= ActiveDungeonCombatContext;
            combats.Add(combat);
            return true;
        }

        public bool TryClearActiveCombat()
        {
            if (ActiveCombat == null) return false;

            ActiveCombat.ExternalPositionOccupied = null;
            combats.Remove(ActiveCombat);
            ActiveCombat = null;
            TryClearActiveDungeonCombatContext();
            return true;
        }

        public bool TryAddGold(int amount)
        {
            if (amount <= 0) return false;

            Gold += amount;
            return true;
        }

        public bool TrySpendGold(int amount)
        {
            if (amount <= 0 || Gold < amount) return false;

            Gold -= amount;
            return true;
        }

        public void AddPendingLoot(IEnumerable<ItemInstance> items)
        {
            if (items == null) return;

            foreach (var item in items)
            {
                if (item != null) pendingLoot.Add(item);
            }
        }

        public bool TryRemovePendingLoot(string itemInstanceId, out ItemInstance item)
        {
            item = null;

            if (string.IsNullOrWhiteSpace(itemInstanceId)) return false;

            var index = pendingLoot.FindIndex(
                candidate => candidate != null
                    && candidate.InstanceId == itemInstanceId);

            if (index < 0) return false;

            item = pendingLoot[index];
            pendingLoot.RemoveAt(index);
            return true;
        }

        public void SetLastCombatRewards(CombatRewardResult rewards)
        {
            LastCombatRewards = rewards;
        }

        private bool CanJoinActiveParty(string characterId)
        {
            if (!charactersById.TryGetValue(characterId, out var character)) return false;

            return character.AvailabilityState == CharacterAvailabilityState.Available 
                && character.LifeState != CharacterLifeState.Dead;
        }

        private bool CanLeadParty(string characterId)
        {
            if (!charactersById.TryGetValue(characterId, out var character)) return false;

            return character.AvailabilityState == CharacterAvailabilityState.Available
                && character.LifeState == CharacterLifeState.Alive 
                && PlayerParty.IsActiveMember(characterId);
        }

        private bool TryAssignFirstAvailableLeader()
        {
            foreach (var characterId in PlayerParty.ActiveMemberIds)
            {
                if (CanLeadParty(characterId))
                {
                    return PlayerParty.TrySetActiveLeader(characterId);
                }
            }

            return false;
        }
    }
}
