using System;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Serialization;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Data.World;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Systems.Combat;
using UnityEngine;

namespace Godmist.Systems.SaveLoad
{
    // Persist logical identities and values. Views, callbacks and derived navigation are rebound on load.
    public static partial class CombatPersistence
    {
        public delegate CharacterSaveData CaptureCharacter(CharacterInstance character);
        public delegate CharacterInstance RestoreCharacter(CharacterSaveData data, string definitionId);

        public static bool CanSave(GameSession game, out string reason)
        {
            reason = string.Empty;
            if (game == null || game.IsDefeated) { reason = "Brak aktywnej rozgrywki."; return false; }
            if (game.CurrentMode == GameModeState.Loading) { reason = "Poczekaj na zakończenie wczytywania."; return false; }
            foreach (var combat in game.Combats)
            {
                if (!combat.IsSeamless || combat.IsDebugSession || combat.DungeonContext == null || game.ActiveDungeon == null)
                { reason = "Zapis jest dostępny tylko w walkach podczas wyprawy."; return false; }
                if (combat.Outcome != CombatOutcomeState.Ongoing || combat.MembershipChangePending
                    || combat.SkillResolutionDepth != 0 || combat.EntryActionCaster != null
                    || combat.ObjectDamageAction != null || combat.PendingScoutKnifeActions.Count > 0
                    || combat.IsExecutionBusy?.Invoke() == true)
                { reason = "Poczekaj na zakończenie akcji."; return false; }
                if (combat.ActiveUnit?.IsPlayerControlled != true || combat.ActiveParticipant?.Unit != combat.ActiveUnit)
                { reason = "Poczekaj na turę gracza."; return false; }
                foreach (var id in combat.DefeatedEnemyInstanceIds)
                    if (combat.EnemySources.TryGetValue(id, out var origin)
                        && (!game.ActiveDungeon.TryGetFloor(combat.DungeonContext.FloorIndex, out var floor)
                            || !floor.TryGetEncounter(origin.EncounterId, out var encounter)
                            || !encounter.IsEnemyDefeated(origin.EnemyId) || !encounter.IsEnemyBodyGenerated(origin.EnemyId)))
                    { reason = "Poczekaj na zakończenie akcji."; return false; }
            }
            return true;
        }

        internal static string Id(BattleUnit unit) => unit?.Character?.CharacterId;
        internal static string Participant(CombatTurnParticipant participant) => participant.IsUnit
            ? "unit:" + Id(participant.Unit) : "object:" + participant.SummonedObject.ObjectId;
        static void Require(bool condition, string path)
        { if (!condition) throw new InvalidOperationException("Nieprawidłowy stan walki: " + path); }

        internal static CombatPositionSaveData Point(GridPosition position) => position.CaptureSave();
        internal static GridPosition Point(CombatPositionSaveData position) => GridPosition.RestoreSave(position);

        public static List<CombatSaveData> Capture(GameSession game, CaptureCharacter character)
        {
            if (!CanSave(game, out var reason)) throw new InvalidOperationException(reason);
            var result = new List<CombatSaveData>();
            foreach (var combat in game.Combats)
            {
                var all = AllUnits(combat).ToArray();
                Require(all.All(u => u?.Character != null) && all.Select(Id).Distinct().Count()==all.Length,"Units/identities");
                var saved = new CombatSaveData
                {
                    CombatId=combat.CombatId, EncounterId=combat.DungeonContext.EncounterInstanceId,
                    FocusedCombatId=game.ActiveCombat?.CombatId, FloorIndex=combat.DungeonContext.FloorIndex,
                    IsDungeonWide=combat.IsDungeonWide, IsFloorWide=combat.DungeonContext.IsFloorWide,
                    EscapeChance=combat.CurrentEscapeChancePercent,ActiveTurnIndex=combat.ActiveTurnIndex,ActionPointsPerSpeed=combat.ActionPointsPerSpeed,
                    HasEscapePenaltyEligibleCharacters=combat.HasEscapePenaltyEligibleCharacters,
                    EscapePenaltyEligibleIds=combat.EscapePenaltyEligibleCharacterIds.ToList(),
                    ActiveUnitIds=combat.Units.Select(Id).ToList(),DepartedUnitIds=combat.DepartedUnits.Keys.ToList(),
                    MapObjectOrder=combat.MapObjects.Select(o=>o.ObjectId).ToList(),
                    TurnOrder=combat.TurnParticipants.Select(Participant).ToList(),
                    SuspendedTurns=combat.SuspendedTurns.Select(Participant).ToList(),
                    DefeatedEnemyIds=combat.DefeatedEnemyInstanceIds.ToList(),
                    ExperienceGrantedEnemyIds=combat.ExperienceGrantedEnemyIds.ToList(),
                    StallEnemyId=Id(combat.StallTrackedEnemy),EnemyTurnsWithoutDamage=combat.EnemyTurnsWithoutDamage,
                    StallEnemyInvisibleAtTurnStart=combat.StallEnemyInvisibleAtTurnStart,EnemyDamagedSinceLastTurn=combat.EnemyDamagedSinceLastTurn
                };
                foreach (var unit in all) saved.Units.Add(CaptureUnit(unit,game,character));
                foreach (var pair in combat.EnemySources) saved.EnemySources.Add(new()
                { UnitId=pair.Key,EncounterId=pair.Value.EncounterId,EnemyId=pair.Value.EnemyId });
                foreach (var id in combat.DefeatedEnemyInstanceIds)
                    if (combat.TryGetEnemyDeathPosition(id,out var position)) saved.DeathPositions.Add(new() {Id=id,Position=Point(position)});
                foreach (var obj in combat.MapObjects)
                {
                    if (obj is KnifeCombatMapObject knife) saved.Objects.Add(new()
                    {Id=knife.ObjectId,Position=Point(knife.Position),IsKnife=true,CreatorId=knife.OwnerCharacterId,KnifeCount=knife.KnifeCount});
                    else if (obj is SummonedCombatMapObject summon) saved.Objects.Add(new()
                    {Id=summon.ObjectId,Position=Point(summon.Position),DefinitionId=summon.Definition.ObjectId,
                     CreatorId=summon.CreatorCharacterId,SkillId=summon.SourceSkillId,EffectIndex=summon.SourceEffectIndex,
                     Hits=summon.RemainingHits,Duration=summon.RemainingDurationTurns,ActionValue=summon.CurrentActionValue});
                    else Require(obj is DoorCombatMapObject or ObstacleCombatMapObject or TrapCombatMapObject,"Objects/unsupported " + obj?.GetType().Name);
                }
                foreach (var effect in combat.ScheduledEffects)
                {
                    Require(effect.Skill != null && effect.EffectIndex>=0 && effect.EffectIndex<effect.Skill.Effects.Count
                        && ReferenceEquals(effect.Effect,effect.Skill.Effects[effect.EffectIndex]),"Scheduled/definition");
                    saved.Scheduled.Add(new() {CasterId=effect.CasterCharacterId,SkillId=effect.Skill.SkillId,
                        ActionId=effect.ActionId,TargetId=effect.TargetCharacterId,EffectIndex=effect.EffectIndex,
                        RemainingTurns=effect.RemainingCasterTurns,HasTargetPosition=effect.TargetPosition.HasValue,TargetPosition=effect.TargetPosition.HasValue?Point(effect.TargetPosition.Value):null});
                }
                foreach (var summon in combat.Summons) saved.Summons.Add(new()
                {UnitId=Id(summon.Unit),CreatorId=summon.CreatorCharacterId,GroupId=summon.GroupId,SkillId=summon.SourceSkillId,
                 EffectIndex=summon.SourceEffectIndex,Duration=summon.RemainingDurationTurns,CreationOrder=summon.CreationOrder});
                foreach (var pair in combat.EquipmentActionHits) saved.ActionHits.Add(new()
                {Id=pair.Key,Values=pair.Value.Select(hit=>new CombatIntEntrySaveData {Id=Id(hit.Key),Value=hit.Value}).ToList()});
                combat.Contact?.CaptureSave(saved);
                result.Add(saved);
            }
            return result;
        }

        static IEnumerable<BattleUnit> AllUnits(CombatSession combat)
        {
            var seen=new HashSet<BattleUnit>();
            var pending=new Queue<BattleUnit>(combat.Units.Concat(combat.DepartedUnits.Values).Concat(combat.Summons.Select(s=>s.Unit))
                .Concat(combat.Contact?.ReferencedUnits ?? Enumerable.Empty<BattleUnit>())
                .Concat(combat.EquipmentActionHits.Values.SelectMany(v=>v.Keys)).Concat(new[]{combat.StallTrackedEnemy}));
            while(pending.Count>0)
            {
                var u=pending.Dequeue();if(u==null||!seen.Add(u))continue;yield return u;
                foreach(var link in u.ScoutState.AngerBySource.Keys.Concat(u.ScoutState.Knives.Select(k=>k.Owner))
                    .Concat(new[]{u.WarriorState.OrderSource,u.WarriorState.ProtectorSource})
                    .Concat(u.ActiveAuras.Select(a=>a.Creator)).Concat(u.OutgoingDamageRedirects.Select(a=>a.Target))
                    .Concat(u.BanditLeaderState?.Guards ?? Enumerable.Empty<BattleUnit>())
                    .Concat(u.ForestEntState?.Plague.Keys ?? Enumerable.Empty<BattleUnit>())
                    .Concat(u.ForestEntState?.PeriodicHits ?? Enumerable.Empty<BattleUnit>())
                    .Concat(u.ForestEntState?.FireHits ?? Enumerable.Empty<BattleUnit>())
                    .Concat(u.ForestEntState?.ActionHits.Select(h=>h.Item2) ?? Enumerable.Empty<BattleUnit>()))pending.Enqueue(link);
            }
        }

        public static void Restore(List<CombatSaveData> data, GameSession game, GameContentCatalog catalog, RestoreCharacter character)
        {
            if (data == null || data.Count == 0) return; // Saves preceding combat persistence have no combat payload.
            Require(game.ActiveDungeon?.TryGetCurrentFloor(out _) == true,"ActiveDungeon");
            var globalUnits=new Dictionary<string,BattleUnit>(StringComparer.Ordinal);
            var globalStates=new Dictionary<string,string>(StringComparer.Ordinal);
            var restoredStates=new HashSet<string>(StringComparer.Ordinal);
            foreach (var saved in data)
            {
                Require(saved?.Version==1,"Version");
                Require(game.ActiveDungeon.TryGetFloor(saved.FloorIndex,out var floor)
                    && floor.FloorIndex==game.ActiveDungeon.CurrentFloorIndex,"FloorIndex");
                Require(floor.TryGetEncounter(saved.EncounterId,out var encounter),"EncounterId");
                Require(new DungeonCombatContextFactory().TryCreate(game.ActiveDungeon,encounter,out var context,saved.IsFloorWide),"Context");
                Require(new DungeonBattleGridFactory().TryCreate(context,out var grid),"Grid");
                var combat=new CombatSession {CombatId=saved.CombatId,IsSeamless=true,IsDungeonWide=saved.IsDungeonWide,
                    DungeonContext=context,Grid=grid,DungeonDefinition=game.ActiveDungeon.Definition,DungeonTheme=game.ActiveDungeon.Definition.Theme,
                    CurrentEscapeChancePercent=saved.EscapeChance,ActiveTurnIndex=saved.ActiveTurnIndex,ActionPointsPerSpeed=saved.ActionPointsPerSpeed,
                    HasEscapePenaltyEligibleCharacters=saved.HasEscapePenaltyEligibleCharacters,
                    StallEnemyInvisibleAtTurnStart=saved.StallEnemyInvisibleAtTurnStart,EnemyDamagedSinceLastTurn=saved.EnemyDamagedSinceLastTurn,
                    EnemyTurnsWithoutDamage=saved.EnemyTurnsWithoutDamage};
                var units=new Dictionary<string,BattleUnit>(StringComparer.Ordinal);
                foreach(var state in saved.Units)
                {
                    Require(state != null && !string.IsNullOrWhiteSpace(state.Id) && !units.ContainsKey(state.Id),"Units/Id");
                    var serializedState=JsonUtility.ToJson(state);
                    if(globalUnits.TryGetValue(state.Id,out var shared))
                    {
                        Require(globalStates[state.Id]==serializedState,"Units/inconsistent shared state "+state.Id);
                        units.Add(state.Id,shared);continue;
                    }
                    CharacterInstance actor;
                    if(!state.HasOwnCharacter) {Require(game.CharactersById.TryGetValue(state.Id,out actor),"Units/party " + state.Id);}
                    else { actor=character(state.Character,state.DefinitionId); Require(actor!=null,"Units/character "+state.Id); actor.CharacterId=state.Id; }
                    var unit=new BattleUnit {Character=actor}; RestoreScalars(state,unit);unit.Position=Point(state.Position);
                    Require(float.IsFinite(unit.CurrentActionValue) && unit.CurrentActionPoints>=0 && unit.MaxActionPoints>=unit.CurrentActionPoints,"Units/AP");
                    Require(state.Stats.Count==Enum.GetValues(typeof(CharacterStatType)).Length
                        &&state.Stats.Select(s=>s.Type).Distinct().Count()==state.Stats.Count,"Units/Stats completeness");
                    Require(state.CurrentHealth>=0&&state.Resource!=null&&state.BaseResistances!=null&&state.CurrentResistances!=null
                        &&state.Resource.CurrentValue>=0&&state.Resource.MaxValue>=0&&float.IsFinite(state.Resource.FractionalGainRemainder),"Units/Character state");
                    foreach(var stat in state.Stats)
                    {Require(float.IsFinite(stat.Base)&&float.IsFinite(stat.Effective),"Units/Stats");ref var value=ref actor.Stats.GetStat(stat.Type);value=new Stat(stat.Base);value.SetEffectiveValue(stat.Effective);}
                    actor.CurrentHealth=state.CurrentHealth;
                    GameSessionRestoreFactory.RestoreResourcePool(actor.ResourcePool,state.Resource);
                    GameSessionRestoreFactory.RestoreResistances(actor.BaseResistances,state.BaseResistances);
                    GameSessionRestoreFactory.RestoreResistances(actor.Resistances,state.CurrentResistances);
                    units.Add(state.Id,unit);globalUnits.Add(state.Id,unit);globalStates.Add(state.Id,serializedState);
                }
                BattleUnit Unit(string id) {if(string.IsNullOrEmpty(id))return null;Require(units.TryGetValue(id,out var u),"Unit reference "+id);return u;}
                foreach(var id in saved.ActiveUnitIds) {Require(!combat.Units.Contains(Unit(id)),"Units/duplicate");combat.Units.Add(Unit(id));}
                foreach(var id in saved.DepartedUnitIds) combat.DepartedUnits.Add(id,Unit(id));
                Require(combat.Units.All(u=>grid.IsWalkable(u.Position)),"Units/Position");
                Require(float.IsFinite(combat.ActionPointsPerSpeed)&&combat.ActionPointsPerSpeed>0,"ActionPointsPerSpeed");
                CombatSessionFactory.AddDungeonMapObjects(combat,context);
                combat.EnvironmentBlocksLineOfSight=(from,to)=>DungeonCombatArenaSystem.BlocksEnvironmentSight(floor,context,from,to);
                combat.IsUnitKnownForMovementPreview=unit=>!StatusEffectSystem.IsInvisibleToPlayer(unit);
                foreach(var state in saved.Objects)
                {
                    Require(!combat.MapObjects.Any(o=>o.ObjectId==state.Id),"Objects/duplicate");
                    if(state.IsKnife) {Require(Unit(state.CreatorId)!=null&&state.KnifeCount>0,"Knife");combat.MapObjects.Add(new KnifeCombatMapObject(state.Id,Point(state.Position),state.CreatorId,state.KnifeCount));}
                    else
                    {
                        var definition=ObjectDefinition(catalog,state.DefinitionId);
                        Require(definition!=null,"Objects/DefinitionId "+state.DefinitionId);
                        var obj=new SummonedCombatMapObject(state.Id,Point(state.Position),state.CreatorId,state.SkillId,state.EffectIndex,definition)
                        {RemainingHits=state.Hits,RemainingDurationTurns=state.Duration,CurrentActionValue=state.ActionValue};
                        combat.MapObjects.Add(obj);combat.SummonedObjects.Add(obj);
                    }
                }
                foreach(var trap in floor.Objects.OfType<SpikeTrapDungeonObjectInstance>())
                    if(saved.MapObjectOrder.Contains(trap.InstanceId)&&context.ContainsWorldCell(trap.Position))
                        combat.MapObjects.Add(new TrapCombatMapObject(trap,DungeonBattleGridFactory.ToBattlePosition(context,trap.Position)));
                Require(saved.MapObjectOrder.Count==combat.MapObjects.Count
                    &&saved.MapObjectOrder.Distinct().Count()==saved.MapObjectOrder.Count,"MapObjectOrder");
                combat.MapObjects=saved.MapObjectOrder.Select(id=>combat.MapObjects.Single(o=>o.ObjectId==id)).ToList();
                SummonedCombatMapObject Obj(string id)
                {var obj=combat.SummonedObjects.SingleOrDefault(o=>o.ObjectId==id);Require(obj!=null,"Object reference "+id);return obj;}
                foreach(var state in saved.Units) if(restoredStates.Add(state.Id)) RestoreUnit(state,Unit(state.Id),combat,Unit,Obj,catalog);
                foreach(var origin in saved.EnemySources)
                {
                    Require(game.ActiveDungeon.TryGetPreRolledRewards(origin.EncounterId,out var rewards),"EnemySources/rewards");
                    Require(DungeonCombatMembershipSystem.EnemyKey(origin.EncounterId,origin.EnemyId)==origin.UnitId,"EnemySources/identity");
                    combat.EnemySources.Add(origin.UnitId,(origin.EncounterId,origin.EnemyId));
                    var reward=rewards.EnemyRewards.SingleOrDefault(r=>r.EnemyInstanceId==origin.EnemyId);
                    if(reward!=null)combat.Rewards.EnemyRewards.Add(new CombatEnemyReward
                    {EnemyInstanceId=origin.UnitId,ExperienceLevel=reward.ExperienceLevel,IsBoss=reward.IsBoss,
                     ExperiencePerActiveCharacter=reward.ExperiencePerActiveCharacter,Gold=reward.Gold,Items=new(reward.Items)});
                }
                combat.DefeatedEnemyInstanceIds.UnionWith(saved.DefeatedEnemyIds);
                foreach(var death in saved.DeathPositions) combat.RestoreEnemyDeathPosition(death.Id,Point(death.Position));
                combat.ExperienceGrantedEnemyIds.UnionWith(saved.ExperienceGrantedEnemyIds);
                combat.EscapePenaltyEligibleCharacterIds.UnionWith(saved.EscapePenaltyEligibleIds);
                combat.StallTrackedEnemy=Unit(saved.StallEnemyId);
                foreach(var s in saved.Scheduled)
                {
                    Require(catalog.TryGetSkill(s.SkillId,out var skill)&&s.EffectIndex>=0&&s.EffectIndex<skill.Effects.Count
                        &&Unit(s.CasterId)!=null&&s.RemainingTurns>0,"Scheduled");
                    combat.ScheduledEffects.Add(new(s.CasterId,skill,skill.Effects[s.EffectIndex],s.EffectIndex,s.RemainingTurns,s.TargetId,!s.HasTargetPosition?null:Point(s.TargetPosition)) {ActionId=s.ActionId});
                }
                foreach(var s in saved.Summons) combat.Summons.Add(new(Unit(s.UnitId),s.CreatorId,s.SkillId,s.EffectIndex,s.Duration,s.CreationOrder) {GroupId=s.GroupId});
                CombatTurnParticipant Turn(string key) => key.StartsWith("unit:",StringComparison.Ordinal)
                    ?new(Unit(key.Substring(5))):key.StartsWith("object:",StringComparison.Ordinal)?new(Obj(key.Substring(7)))
                    :throw new InvalidOperationException("Nieprawidłowy uczestnik tury: "+key);
                combat.TurnParticipants=saved.TurnOrder.Select(Turn).ToList();
                Require(saved.TurnOrder.Distinct().Count()==saved.TurnOrder.Count&&saved.ActiveTurnIndex>=0&&saved.ActiveTurnIndex<combat.TurnParticipants.Count,"TurnOrder");
                combat.TurnOrder=combat.TurnParticipants.Where(p=>p.IsUnit).Select(p=>p.Unit).ToList();
                combat.SummonedObjectTurnOrder=combat.TurnParticipants.Where(p=>p.IsSummonedObject).Select(p=>p.SummonedObject).ToList();
                combat.ActiveParticipant=combat.TurnParticipants[saved.ActiveTurnIndex];combat.ActiveUnit=combat.ActiveParticipant.Unit;
                combat.ActiveSummonedObject=combat.ActiveParticipant.SummonedObject;
                foreach(var key in saved.SuspendedTurns) combat.SuspendedTurns.Add(combat.TurnParticipants.Single(p=>Participant(p)==key));
                foreach(var hits in saved.ActionHits)
                {
                    var values=hits.Values.ToDictionary(hit=>Unit(hit.Id),hit=>hit.Value);
                    combat.EquipmentActionHits.Add(hits.Id,values);
                }
                // The contact owner restores its memory before any visibility refresh.
                combat.Contact=new DungeonContactState((_,_)=>false);combat.Contact.RestoreSave(saved,Unit);
                Require(game.TryRegisterDungeonCombat(combat),"Combat registration");
            }
            foreach(var unit in globalUnits.Values)
                unit.WarriorState.Session=game.Combats.FirstOrDefault(c=>c.Units.Contains(unit))
                    ??game.Combats.FirstOrDefault(c=>c.DepartedUnits.ContainsValue(unit));
            var focus=data[0].FocusedCombatId;
            Require(data.All(c=>c.FocusedCombatId==focus),"FocusedCombatId");
            Require(game.TryFocusCombat(string.IsNullOrEmpty(focus)?null:game.Combats.Single(c=>c.CombatId==focus)),"FocusedCombatId");
            Require(CanSave(game,out _),"Restored boundary");
        }

        static SummonedObjectDefinition ObjectDefinition(GameContentCatalog catalog,string id)
        {
            foreach(var definition in catalog.CharacterDefinitions.Where(d=>d!=null))
                foreach(var candidate in new[]{definition.SanctuaryWarning,definition.SanctuaryFire})
                    if(candidate!=null&&candidate.ObjectId==id)return candidate;
            foreach(var skill in catalog.SkillDefinitions.Where(s=>s!=null))
                foreach(var effect in skill.Effects)
                    if(effect.SummonedObjectDefinition?.ObjectId==id)return effect.SummonedObjectDefinition;
            return null;
        }
    }
}
