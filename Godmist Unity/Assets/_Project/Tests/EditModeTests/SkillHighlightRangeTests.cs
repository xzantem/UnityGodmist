using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using NUnit.Framework;
public class SkillHighlightRangeTests
{
 [TestCase(false)] [TestCase(true)]
 public void DriverRangeBatchPreservesDestructibleTargetsAndFreshSight(bool requiresSight)
 {
  var skill=UnityEngine.ScriptableObject.CreateInstance<SkillData>();
  var definition=UnityEngine.ScriptableObject.CreateInstance<Godmist.Data.Dungeons.ObstacleDungeonObjectDefinition>();
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  void Set(object target,string field,object value)=>target.GetType().GetField(field,flags).SetValue(target,value);
  using var driver=new Godmist.Views.Combat.CombatExecutionDriver(null,null,null,null,null,null,null,8,8,0,0,0,null,null,new(),null,null,null);
  try
  {
   Set(definition,"<IsDestructible>k__BackingField",true);
   Set(skill,"<RequiresLineOfSight>k__BackingField",requiresSight);
   var mode=new SkillTargetModeData();
   Set(mode,"absoluteRange",new SkillRangeData(RangeShape.Square,0,6));
   Set(mode,"targetType",TargetType.Single); Set(mode,"targetingType",TargetingType.Enemies);
   Set(skill,"targetModes",new List<SkillTargetModeData>{mode});
   var damage=new Godmist.Data.Effects.SkillEffectData(); Set(damage,"effectType",EffectType.DealDamage);
   Set(skill,"effects",new List<Godmist.Data.Effects.SkillEffectData>{damage});
   var caster=new BattleUnit{Character=new CharacterInstance{LifeState=CharacterLifeState.Alive},Position=new GridPosition(1,1),IsPlayerSide=true};
   var enemy=new BattleUnit{Character=new CharacterInstance{LifeState=CharacterLifeState.Alive},Position=new GridPosition(5,1)};
   var session=new CombatSession{Grid=new BattleGrid(8,8),ActiveUnit=caster}; session.Units.Add(caster); session.Units.Add(enemy);
   var source=new Godmist.Runtime.Dungeons.ObstacleDungeonObjectInstance("blocker",definition,new UnityEngine.Vector2Int(3,1));
   var blocker=new ObstacleCombatMapObject("blocker",new GridPosition(3,1),true,true,source); session.MapObjects.Add(blocker);
   Set(driver,"<Session>k__BackingField",session); Set(driver,"targetingService",new CombatTargetingService());
   Set(driver,"lineOfSightService",new CombatLineOfSightService());
   Assert.That(driver.GetSelectableSkillPositions(skill,mode,0),Does.Contain(blocker.Position),"A destructible target must not occlude itself.");
   Assert.That(driver.GetSelectableSkillPositions(skill,mode,0).Contains(enemy.Position),Is.EqualTo(!requiresSight));
   var expected=new CombatTargetingService().GetAbsoluteRangePositions(session,caster,mode)
    .Where(p=>!requiresSight||new CombatLineOfSightService().HasLineOfSight(session,caster.Position,p));
   Assert.That(driver.GetAbsoluteSkillRangePositions(skill,mode),Is.EqualTo(expected));
   source.TryDeactivate();
   Assert.That(driver.GetSelectableSkillPositions(skill,mode,0),Does.Contain(enemy.Position),"Next query must observe destruction immediately.");
  }
  finally{UnityEngine.Object.DestroyImmediate(skill);UnityEngine.Object.DestroyImmediate(definition);}
 }

 [TestCase(RangeShape.Square,0,0)]
 [TestCase(RangeShape.Radial,60,50)]
 [TestCase(RangeShape.Ring,119,99)]
 [TestCase(RangeShape.XShape,3,8)]
 public void BoundedRangeMatchesWholeGridReference(RangeShape shape,int x,int y)
 {
  var session=new CombatSession { Grid=new BattleGrid(120,100) };
  var caster=new BattleUnit { Character=new CharacterInstance(), Position=new GridPosition(x,y) };
  var range=new SkillRangeData(shape,2,8); var mode=new SkillTargetModeData();
  typeof(SkillTargetModeData).GetField("absoluteRange",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(mode,range);
  var reference=new List<GridPosition>();
  for(int px=0;px<session.Grid.Width;px++) for(int py=0;py<session.Grid.Height;py++)
   foreach(var p in session.Grid.GetPositionsAt(px,py)) if(CombatTargetingService.IsPositionWithinRange(caster.Position,p,range)) reference.Add(p);
  var actual=new CombatTargetingService().GetAbsoluteRangePositions(session,caster,mode);
  Assert.That(actual,Is.EqualTo(reference));
 }
 [TestCase(TargetType.Single)] [TestCase(TargetType.AreaAroundTile)]
 public void SelectablePositionsMatchWholeGridReference(TargetType type)
 {
  var session=new CombatSession { Grid=new BattleGrid(80,60) };
  var caster=new BattleUnit { Character=new CharacterInstance { LifeState=CharacterLifeState.Alive }, Position=new GridPosition(5,5),IsPlayerSide=true };
  session.Units.Add(caster);
  session.Units.Add(new BattleUnit { Character=new CharacterInstance { LifeState=CharacterLifeState.Alive },Position=new GridPosition(6,6) });
  session.Units.Add(new BattleUnit { Character=new CharacterInstance { LifeState=CharacterLifeState.Alive },Position=new GridPosition(50,50) });
  var range=new SkillRangeData(RangeShape.Square,0,4);var mode=new SkillTargetModeData();
  typeof(SkillTargetModeData).GetField("absoluteRange",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(mode,range);
  typeof(SkillTargetModeData).GetField("targetType",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(mode,type);
  var service=new CombatTargetingService();var reference=new List<GridPosition>();
  for(int x=0;x<80;x++)for(int y=0;y<60;y++)foreach(var p in session.Grid.GetPositionsAt(x,y))
   if(session.Grid.IsWalkable(p) && CombatTargetingService.IsPositionWithinRange(caster.Position,p,range)
    && (type==TargetType.AreaAroundTile || service.GetTargets(session,caster,mode,p).Count>0))reference.Add(p);
  Assert.That(service.GetSelectablePositions(session,caster,mode),Is.EqualTo(reference));
  Assert.That(reference,Is.Not.Empty);
 }
 [Test]
 public void ReportLargeDungeonRangeCost()
 {
  var session=new CombatSession { Grid=new BattleGrid(300,300) };
  var caster=new BattleUnit { Character=new CharacterInstance(),Position=new GridPosition(150,150) };
  var range=new SkillRangeData(RangeShape.Square,0,10);var mode=new SkillTargetModeData();
  typeof(SkillTargetModeData).GetField("absoluteRange",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(mode,range);
  var service=new CombatTargetingService();service.GetAbsoluteRangePositions(session,caster,mode);
  var watch=System.Diagnostics.Stopwatch.StartNew();int count=0;
  for(int repeat=0;repeat<10;repeat++) for(int x=0;x<300;x++) for(int y=0;y<300;y++)
   foreach(var p in session.Grid.GetPositionsAt(x,y)) if(CombatTargetingService.IsPositionWithinRange(caster.Position,p,range)) count++;
  var before=watch.Elapsed.TotalMilliseconds;watch.Restart();int afterCount=0;
  for(int repeat=0;repeat<10;repeat++) afterCount+=service.GetAbsoluteRangePositions(session,caster,mode).Count;
  TestContext.WriteLine($"10 range builds: whole-grid reference {before:F2} ms; bounded implementation {watch.Elapsed.TotalMilliseconds:F2} ms");
  Assert.That(afterCount,Is.EqualTo(count));
 }
}
