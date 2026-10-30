using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Systems.Combat;
using NUnit.Framework;
namespace Godmist.Tests.EditMode
{
    public partial class CombatRecoverySystemTests
    {
        [TestCase(false)][TestCase(true)]
        public void StableReferenceExecutesItsDependencyBeforeFollowupAfterReorder(bool reorder)
        {
            var session=CreateSession(out var caster,out var target);target.Character.CurrentHealth=40;
            var skill=CreateRecoverySkill(EffectType.Heal,25);
            var first=skill.Effects[0];var followup=new SkillEffectData();
            SetPrivateField(followup,"effectType",EffectType.Heal);SetPrivateField(followup,"baseValue",10);
            var condition=new SkillEffectConditionData();
            SetPrivateField(condition,"conditionType",EffectConditionType.ReferencedEffectSucceeded);
            SetPrivateField(condition,"referencedEffectId",first.StableId);
            SetPrivateField(followup,"conditions",new List<SkillEffectConditionData>{condition});
            SetPrivateField(skill,"stableEffectIdsInitialized",true);
            SetPrivateField(skill,"effects",reorder?new List<SkillEffectData>{followup,first}:new List<SkillEffectData>{first,followup});
            Assert.That(new SkillExecutionSystem().TryExecute(session,caster,skill,target),Is.True);
            Assert.That(target.Character.CurrentHealth,Is.EqualTo(75));
        }
    }
}
