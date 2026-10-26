using System;
using Godmist.Core.Enums;
using Godmist.Data.Quests;

namespace Godmist.Runtime.Quests
{
    // Per-offer parameters; the authored quest remains an unchanged slot/template.
    public sealed class RadiantBoardContract
    {
        public const string ObjectiveId = "objective.board.contract";
        public QuestObjectiveType ObjectiveType { get; }
        public DungeonTheme Theme { get; }
        public DungeonLength Length { get; }
        public int Level { get; }
        public int RequiredAmount { get; }
        public int RewardMultiplier { get; }
        public int Cycle { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public QuestStageDefinition Stage { get; }

        public RadiantBoardContract(string stageId, QuestObjectiveType type, DungeonTheme theme, DungeonLength length,
            int level, int requiredAmount, int multiplier, int cycle)
        {
            if (string.IsNullOrWhiteSpace(stageId) || !Enum.IsDefined(typeof(DungeonTheme),theme) || theme==DungeonTheme.Story
                || level<1 || level>50 || cycle<0 || multiplier<1 || multiplier>3
                || (type!=QuestObjectiveType.KillEnemy && type!=QuestObjectiveType.ExitDungeon))
                throw new ArgumentException("Invalid mission-board contract.");
            if (type==QuestObjectiveType.ExitDungeon
                ? length<DungeonLength.Short || length>DungeonLength.Long || requiredAmount!=1 || multiplier!=(int)length
                : length!=DungeonLength.Unspecified || requiredAmount<(multiplier==1?8:multiplier==2?16:24)
                    || requiredAmount>(multiplier==1?12:multiplier==2?24:36))
                throw new ArgumentException("Invalid mission-board objective.");
            ObjectiveType=type; Theme=theme; Length=length; Level=level; RequiredAmount=requiredAmount;
            RewardMultiplier=multiplier; Cycle=cycle;
            var name=theme switch {DungeonTheme.Catacombs=>"Katakumby",DungeonTheme.Forest=>"Las",_=>theme.ToString()};
            if(type==QuestObjectiveType.ExitDungeon)
            {
                var size=length switch {DungeonLength.Short=>"krótką",DungeonLength.Normal=>"normalną",_=>"długą"};
                DisplayName=$"Ukończ {size} wyprawę — {name}";
                Description=$"Ukończ {size} wyprawę — {name}.";
            }
            else
            {
                DisplayName=$"Oczyść: {name}";
                Description=$"Pokonaj {requiredAmount} przeciwników — {name}. Przywołania i posiłki nie liczą się do zlecenia.";
            }
            Stage=new QuestStageDefinition(stageId, Description,
                new QuestObjectiveDefinition(ObjectiveId,Description.Split('.')[0],type,theme.ToString(),requiredAmount));
        }
    }
}
