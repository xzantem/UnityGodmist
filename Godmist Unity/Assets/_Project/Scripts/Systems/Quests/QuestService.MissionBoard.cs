using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Quests;
using Godmist.Systems.Dungeons;

namespace Godmist.Systems.Quests
{
    public partial class QuestService
    {
        public bool EnsureMissionBoard()
        {
            if (contentCatalog==null || gameSession.ActiveDungeon!=null
                || !DungeonOfferSystem.EnsureOffers(gameSession,contentCatalog.DungeonDefinitions)) return false;
            var board=gameSession.DungeonOffers;
            var partyLevel=DungeonOfferSystem.PartyLevel(gameSession);
            var choices=board.Offers.Select(offer =>
            {
                contentCatalog.TryGetDungeon(offer.DefinitionId,out var definition);
                return (Definition:definition, Offer:offer, Level:DungeonOfferSystem.OfferLevel(partyLevel,offer));
            }).Where(p=>p.Definition!=null && p.Offer.Length>=DungeonLength.Short && p.Offer.Length<=DungeonLength.Long).ToArray();
            if(choices.Length==0) return false;
            var candidates=new List<(QuestObjectiveType Type,DungeonTheme Theme,DungeonLength Length,int Level,int Tier)>();
            foreach(var choice in choices)
            {
                if(choice.Level<=partyLevel)
                    candidates.Add((QuestObjectiveType.ExitDungeon,choice.Definition.Theme,choice.Offer.Length,choice.Level,(int)choice.Offer.Length));
                for(int tier=1;tier<=3;tier++) candidates.Add((QuestObjectiveType.KillEnemy,choice.Definition.Theme,DungeonLength.Unspecified,partyLevel,tier));
            }
            candidates=candidates.Distinct().ToList();
            var used=new HashSet<(QuestObjectiveType,DungeonTheme,DungeonLength,int,int)>();
            bool Keep(QuestInstance quest)
            {
                if(quest.Status is QuestStatus.Active or QuestStatus.Completed) return true;
                if(quest.Status==QuestStatus.TurnedIn && quest.QuestFlags.TryGetValue(QuestInstance.BoardClaimedCycleFlag,out var value)
                    && int.TryParse(value,out var claimed) && claimed>=board.CompletedExpeditions) return true;
                var contract=quest.BoardContract;
                return quest.Status==QuestStatus.Available && contract!=null && contract.Cycle==board.CompletedExpeditions
                    && contract.Level<=partyLevel;
            }
            var retained=new HashSet<string>();
            foreach(var id in CatacombsMissionBoard.QuestIds)
            {
                if(!gameSession.TryGetQuest(id,out var quest))
                {
                    if(!contentCatalog.TryGetQuest(id,out var definition) || !TryRegisterQuest(definition)
                        || !gameSession.TryGetQuest(id,out quest)) return false;
                }
                if(Keep(quest)) retained.Add(id);
            }
            var themes=candidates.Select(c=>c.Theme).Distinct().ToArray();
            // A different kill quota/reward is still the same job destination.
            // Keep accepted contracts intact; reroll only duplicate offers.
            var killThemes=new HashSet<DungeonTheme>();
            DungeonTheme? KillTheme(QuestInstance quest) => quest.Status==QuestStatus.TurnedIn ? null
                : quest.BoardContract is {} c ? c.ObjectiveType==QuestObjectiveType.KillEnemy ? c.Theme : null
                : quest.QuestId==CatacombsMissionBoard.KillQuestId ? DungeonTheme.Catacombs : null;
            foreach(var id in retained.OrderBy(id=>gameSession.QuestsById[id].Status==QuestStatus.Available?1:0).ToArray())
            {
                var quest=gameSession.QuestsById[id];
                if(KillTheme(quest) is not {} theme) continue;
                if(!killThemes.Add(theme) && quest.Status==QuestStatus.Available) retained.Remove(id);
            }
            var themeCounts=themes.ToDictionary(theme=>theme,theme=>0);
            foreach(var id in retained)
            {
                var quest=gameSession.QuestsById[id];
                // Legacy accepted contracts in these three slots target Catacombs.
                var theme=quest.BoardContract?.Theme??DungeonTheme.Catacombs;
                if(quest.Status!=QuestStatus.TurnedIn && themeCounts.ContainsKey(theme)) themeCounts[theme]++;
            }
            // Repair a previously rolled single-theme board once, using only
            // unaccepted slots. Accepted jobs and pending rewards remain intact.
            var fullRetainedBoard=themeCounts.Values.Sum()==CatacombsMissionBoard.QuestIds.Length;
            foreach(var theme in themes.Where(theme=>fullRetainedBoard && themeCounts[theme]==0))
            {
                var replace=CatacombsMissionBoard.QuestIds.Select(id=>gameSession.QuestsById[id])
                    .LastOrDefault(quest=>retained.Contains(quest.QuestId) && quest.Status==QuestStatus.Available
                        && quest.BoardContract is {} contract && themeCounts.TryGetValue(contract.Theme,out var count) && count>1);
                if(replace==null) continue;
                retained.Remove(replace.QuestId);
                themeCounts[replace.BoardContract.Theme]--;
            }
            foreach(var id in retained)
                if(gameSession.QuestsById[id] is {Status:not QuestStatus.TurnedIn,BoardContract:{} c})
                    used.Add((c.ObjectiveType,c.Theme,c.Length,c.Level,c.RewardMultiplier));
            killThemes.Clear();
            foreach(var id in retained)
                if(KillTheme(gameSession.QuestsById[id]) is {} theme) killThemes.Add(theme);
            foreach(var id in CatacombsMissionBoard.QuestIds)
            {
                var quest=gameSession.QuestsById[id];
                if(retained.Contains(id)) continue;
                var available=candidates.Where(c=>!used.Contains(c)
                    && (c.Type!=QuestObjectiveType.KillEnemy || !killThemes.Contains(c.Theme))).ToArray();
                if(available.Length==0) return false;
                var availableThemes=available.Select(c=>c.Theme).Distinct().ToArray();
                var minimum=availableThemes.Min(theme=>themeCounts[theme]);
                var leastUsed=availableThemes.Where(theme=>themeCounts[theme]==minimum).ToArray();
                var selectedTheme=leastUsed[random.Next(leastUsed.Length)];
                available=available.Where(c=>c.Theme==selectedTheme).ToArray();
                var kind=random.Next(2)==0?QuestObjectiveType.KillEnemy:QuestObjectiveType.ExitDungeon;
                var matching=available.Where(c=>c.Type==kind).ToArray();
                if(matching.Length==0) matching=available;
                var selected=matching[random.Next(matching.Length)]; used.Add(selected); themeCounts[selected.Theme]++;
                if(selected.Type==QuestObjectiveType.KillEnemy) killThemes.Add(selected.Theme);
                var required=selected.Type==QuestObjectiveType.ExitDungeon?1:
                    random.Next(selected.Tier==1?8:selected.Tier==2?16:24,1+(selected.Tier==1?12:selected.Tier==2?24:36));
                var contract=new RadiantBoardContract(quest.Definition.StartStageId,selected.Type,selected.Theme,selected.Length,
                    selected.Level,required,selected.Tier,board.CompletedExpeditions);
                var roll=random.Next(10); var variant=roll<3?"experience":roll<6?"gold":"mix";
                var reward=questRewardSystem.CreateRadiantRewardOffer(contract.Level,false,variant);
                int Scale(int amount)=>(int)Math.Min(int.MaxValue,(long)amount*contract.RewardMultiplier);
                var scaled=new RadiantRewardOffer(reward.Level,Scale(reward.Experience),Scale(reward.Gold),Scale(reward.Honor),reward.BonusItem);
                if(!quest.TrySetBoardContract(contract) || !quest.TrySetRadiantRewardOffer(scaled)) return false;
                QuestBecameAvailable?.Invoke(quest);
            }
            return true;
        }

        internal bool TryGetMissionDungeon(string id, out DungeonDefinition definition)
        {
            definition=gameSession.ActiveDungeon?.DungeonId==id?gameSession.ActiveDungeon.Definition:null;
            return definition!=null || contentCatalog!=null && contentCatalog.TryGetDungeon(id,out definition);
        }
    }
}
