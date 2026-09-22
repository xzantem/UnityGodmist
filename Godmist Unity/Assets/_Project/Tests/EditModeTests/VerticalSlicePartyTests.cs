using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.World;
using Godmist.Runtime.Game;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.Items;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEditor;

namespace Godmist.Tests.EditMode
{
    public sealed class VerticalSlicePartyTests
    {
        static NewCampaignDefinition Profile => AssetDatabase.LoadAssetAtPath<NewCampaignDefinition>("Assets/_Project/ScriptableObjects/StartingCampaign/NC_Arungard.asset");
        static GameSession Create()
        {
            Assert.That(new NewCampaignService().TryCreate(Profile,out var session,out var error),Is.True,error);
            return session;
        }
        [TestCase(0,300,18,36,55,200,0,ResourceType.Momentum)]
        [TestCase(1,375,24,32,40,50,0,ResourceType.Rage)]
        [TestCase(2,250,27,36,45,100,20,ResourceType.Mana)]
        [TestCase(3,450,21,30,35,100,15,ResourceType.Mana)]
        public void PartyUsesApprovedClassStatsAndResources(int index,int hp,int min,int max,int speed,int capacity,int regen,ResourceType type)
        {
            var session=Create();var c=session.CharactersById[Profile.Characters[index].Character.Id];
            Assert.That(c.Stats.MaxHealth.BaseValue,Is.EqualTo(hp));
            Assert.That(c.Stats.MinAttack.BaseValue,Is.EqualTo(min));Assert.That(c.Stats.MaxAttack.BaseValue,Is.EqualTo(max));
            Assert.That(c.Stats.Speed.GetValue(),Is.EqualTo(speed));
            Assert.That(c.Stats.CritChance.BaseValue,Is.Zero);Assert.That(c.Stats.CritDamage.BaseValue,Is.EqualTo(1));
            Assert.That(c.ResourcePool.ResourceType,Is.EqualTo(type));Assert.That(c.ResourcePool.MaxValue,Is.EqualTo(capacity));
            Assert.That(c.ResourcePool.Regeneration,Is.EqualTo(regen));
            Assert.That(c.ResourcePool.CurrentValue,Is.EqualTo(type==ResourceType.Mana?capacity:0));
            if(type==ResourceType.Mana)
            {
                c.ResourcePool.CurrentValue=0;
                new ResourceRuleSystem().ProcessTurnStart(new BattleUnit { Character=c });
                Assert.That(c.ResourcePool.CurrentValue,Is.EqualTo(regen));
            }
        }
        [Test]
        public void PartyHasDistinctPortraitsAndCompleteMovementFrames()
        {
            var chars=Profile.Characters.Select(x=>x.Character).ToArray();
            Assert.That(chars.Select(x=>x.VisualSprite).Distinct().Count(),Is.EqualTo(4));
            foreach(var c in chars)
            {
                Assert.That(c.VisualSprite,Is.Not.Null,c.Id);
                Assert.That(c.VisualSprite.pixelsPerUnit,Is.EqualTo(16f),c.Id);
                foreach(var sprite in c.IdleAnimationFrames.Concat(c.RunAnimationFrames).Append(c.HitSprite))
                {
                    var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
                    Assert.That(importer.filterMode,Is.EqualTo(UnityEngine.FilterMode.Point),sprite.name);
                    Assert.That(importer.GetDefaultPlatformTextureSettings().textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed),sprite.name);
                    Assert.That(importer.GetPlatformTextureSettings("Standalone").textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed),sprite.name);
                }Assert.That(c.HitSprite,Is.Not.Null,c.Id);
                Assert.That(c.IdleAnimationFrames.Count,Is.EqualTo(4),c.Id);Assert.That(c.RunAnimationFrames.Count,Is.EqualTo(4),c.Id);
                Assert.That(c.IdleAnimationFrames.Concat(c.RunAnimationFrames).All(x=>x!=null),Is.True,c.Id);
            }
        }
        [TestCase(0,.5f,.7f,.4f,.5f,.35f)]
        [TestCase(1,.7f,.5f,.4f,.6f,.6f)]
        [TestCase(2,.25f,.25f,.8f,.7f,.3f)]
        [TestCase(3,.3f,.4f,.5f,.25f,.75f)]
        public void ResistancesFollowClassSheet(int index,float bleed,float poison,float fire,float weakening,float control)
        {
            var r=Create().CharactersById[Profile.Characters[index].Character.Id].Resistances;
            Assert.That(r.BleedResistance,Is.EqualTo(bleed));Assert.That(r.PoisonResistance,Is.EqualTo(poison));
            Assert.That(r.FireResistance,Is.EqualTo(fire));Assert.That(r.WeakeningResistance,Is.EqualTo(weakening));
            Assert.That(r.SuppressionResistance,Is.EqualTo(control));Assert.That(r.MoveResistance,Is.EqualTo(control));
        }
        [Test]
        public void EveryMemberCanLeadRegroupEquipAndProgressAcrossSaveLoad()
        {
            var s=Create();var ids=s.PlayerParty.ActiveMemberIds.ToArray();var equipment=new EquipmentService();
            foreach(var id in ids)
            {
                Assert.That(s.TrySetActiveLeader(id),Is.True);Assert.That(s.PlayerParty.ActiveLeaderId,Is.EqualTo(id));
                Assert.That(s.PlayerParty.TryDetach(id),Is.True);
                Assert.That(s.PlayerParty.TryJoinGroup(id,ids.First(x=>x!=id)),Is.True);
                var c=s.CharactersById[id];var weapon=c.Equipment.GetItem(EquipmentSlot.Weapon);var damage=c.Stats.MinAttack.GetValue();
                Assert.That(equipment.TryUnequip(c,EquipmentSlot.Weapon),Is.True);Assert.That(c.Inventory.Items,Does.Contain(weapon));
                Assert.That(equipment.TryEquip(c,weapon.InstanceId),Is.True);Assert.That(c.Stats.MinAttack.GetValue(),Is.EqualTo(damage));
                var progress=new CharacterProgressionSystem();Assert.That(progress.AddExperience(c,progress.GetExperienceRequiredForNextLevel(1)),Is.EqualTo(1));
                Assert.That(c.Level,Is.EqualTo(2));Assert.That(c.AvailableSkillPoints,Is.GreaterThan(0));
            }
            Assert.That(s.PlayerParty.TryDetach(ids[2]),Is.True);
            var snapshot=new GameSessionSaveSnapshotFactory().CreateSnapshot(s,"party");
            var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            Assert.That(new GameSessionRestoreFactory().TryCreate(snapshot,catalog,out var restored),Is.True);
            Assert.That(restored.PlayerParty.ActiveLeaderId,Is.EqualTo(ids.Last()));
            Assert.That(restored.PlayerParty.GroupCount,Is.EqualTo(s.PlayerParty.GroupCount));
            for(var i=0;i<s.PlayerParty.GroupCount;i++)Assert.That(restored.PlayerParty.GetGroup(i),Is.EqualTo(s.PlayerParty.GetGroup(i)));
            foreach(var id in ids)
            {
                var before=s.CharactersById[id];var after=restored.CharactersById[id];
                Assert.That(after.Level,Is.EqualTo(2));Assert.That(after.AvailableSkillPoints,Is.EqualTo(before.AvailableSkillPoints));
                Assert.That(after.Stats.MaxHealth.GetValue(),Is.EqualTo(before.Stats.MaxHealth.GetValue()));
                Assert.That(after.ResourcePool.Regeneration,Is.EqualTo(before.ResourcePool.Regeneration));
                Assert.That(after.Equipment.GetItem(EquipmentSlot.Weapon).InstanceId,Is.EqualTo(before.Equipment.GetItem(EquipmentSlot.Weapon).InstanceId));
            }
        }
    }
}
