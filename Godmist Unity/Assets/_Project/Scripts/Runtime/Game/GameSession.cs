using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Runtime.Quests;

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
        public Party PlayerParty { get; } = new();
        public WorldState WorldState { get; } = new();
        public IReadOnlyDictionary<string, CharacterInstance> CharactersById => charactersById;
        public IReadOnlyDictionary<string, NpcInstance> NpcsById => npcsById;
        public IReadOnlyDictionary<string, QuestInstance> QuestsById => questsById;
        public string TrackedQuestId => trackedQuestId;
        public DungeonInstance ActiveDungeon { get; private set; }
        public CombatSession ActiveCombat { get; private set; }
        public int Gold { get; private set; }
        public IReadOnlyList<ItemInstance> PendingLoot => pendingLoot;
        public CombatRewardResult LastCombatRewards { get; private set; }

        public event Action<GameModeState, GameModeState> ModeChanged;

        public event Action<NpcInstance> NpcInteractionRequested;
        public event Action<string> TrackedQuestChanged;

        public GameSession(GameModeState initialMode = GameModeState.Menu)
        {
            CurrentMode = initialMode;
        }

        public bool TrySetMode(GameModeState nextMode)
        {
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

        public bool TrySetActiveDungeon(DungeonInstance dungeon)
        {
            if (dungeon == null || string.IsNullOrWhiteSpace(dungeon.DungeonId)) return false;
            if (ReferenceEquals(ActiveDungeon, dungeon)) return false;

            ActiveDungeon = dungeon;
            return true;
        }

        public bool TryClearActiveDungeon()
        {
            if (ActiveDungeon == null) return false;

            ActiveDungeon = null;
            return true;
        }

        public bool TrySetActiveCombat(CombatSession combat)
        {
            if (combat == null || string.IsNullOrWhiteSpace(combat.CombatId)) return false;
            if (ActiveCombat != null) return false;

            ActiveCombat = combat;
            return true;
        }

        public bool TryClearActiveCombat()
        {
            if (ActiveCombat == null) return false;

            ActiveCombat = null;
            return true;
        }

        public bool TryAddGold(int amount)
        {
            if (amount <= 0) return false;

            Gold += amount;
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