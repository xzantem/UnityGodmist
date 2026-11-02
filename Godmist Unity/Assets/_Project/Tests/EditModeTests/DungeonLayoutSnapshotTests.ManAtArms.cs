using System.Linq;
using System.Text.RegularExpressions;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.World;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class DungeonLayoutSnapshotTests
    {
        [TestCase(false)][TestCase(true)]
        public void ManAtArmsDungeonStacksAndLegacyCharacterSurviveTwoJsonCycles(bool legacyMissingCounter)
        {
            var production=AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            catalog.CharacterDefinitions.AddRange(production.CharacterDefinitions);
            catalog.CharacterClasses.AddRange(production.CharacterClasses);
            catalog.SkillDefinitions.AddRange(production.SkillDefinitions);
            catalog.ItemDefinitions.AddRange(production.ItemDefinitions);
            var definition=production.CharacterDefinitions.Single(d=>d.Id=="wladimir");
            var character=new CharacterInstanceFactory().Create(definition);
            new CharacterProgressionSystem().TrySetLevel(character,40,out _);
            Assert.That(session.TryRegisterCharacter(character),Is.True);
            Assert.That(session.TryAddCharacterToActiveParty(character.CharacterId),Is.True);
            var progression=new SkillTreeProgressionSystem();
            Assert.That(progression.TryInvest(character,WarriorCombatSystem.ManAtArms,"node.warrior.man_at_arms.t1_1"),Is.True);
            Assert.That(progression.TryInvest(character,WarriorCombatSystem.ManAtArms,"node.warrior.man_at_arms.t1_2"),Is.True);
            character.ManAtArmsDungeonKills=17;
            var expectedPoints=character.AvailableSkillPoints;var serializer=new GameSaveJsonSerializer();
            var saved=Snapshot();
            for(var cycle=0;cycle<2;cycle++)
            {
                var json=serializer.Serialize(saved);
                if(legacyMissingCounter)json=Regex.Replace(json,@",?\s*""ManAtArmsDungeonKills""\s*:\s*\d+","");
                Assert.That(serializer.TryDeserialize(json,out saved),Is.True);
                Assert.That(new GameSessionRestoreFactory().TryCreate(saved,catalog,out var restored,out var error),Is.True,error?.ToString());
                var c=restored.CharactersById[character.CharacterId];
                Assert.That(c.ManAtArmsDungeonKills,Is.EqualTo(legacyMissingCounter?0:17));
                Assert.That(c.AvailableSkillPoints,Is.EqualTo(expectedPoints));
                Assert.That(c.ClassData.SkillTrees.Count,Is.EqualTo(3));
                Assert.That(SkillTreeProgressionSystem.GetInvestedRanks(c,WarriorCombatSystem.ManAtArms),Is.EqualTo(2));
                saved=new GameSessionSaveSnapshotFactory().CreateSnapshot(restored,"second");
                Assert.That(restored.TryClearActiveDungeon(),Is.True);
                Assert.That(c.ManAtArmsDungeonKills,Is.Zero);
            }
        }
    }
}
