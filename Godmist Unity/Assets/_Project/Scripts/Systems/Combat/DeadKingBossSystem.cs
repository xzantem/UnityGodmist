using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public sealed class DeadKingBossSystem : IDisposable
    {
        public const string PhaseTwoId = CatacombsBossSystem.BossId + ".enraged";
        public const string KnightId = "enemy.catacombs.spectral_knight";
        public const string CommandId = "skill.enemy.catacombs.dead_king_command";
        public const string BladeId = "skill.enemy.catacombs.dead_king_blade";
        public const string ShadowId = "skill.enemy.catacombs.dead_king_shadow";
        public const string ShadowImpactCue = "Uderzenie Cienia Destrukcji";
        readonly CombatSession session;
        readonly CombatDamageSystem damage;
        readonly StatusEffectSystem status;
        readonly Action<CombatMapObject> changed;
        readonly Action<string> log;
        readonly CombatFeedbackBus feedback;
        readonly Func<BattleUnit, SkillData, bool> previousGate;
        public DeadKingBossSystem(CombatSession session, CombatDamageSystem damage, StatusEffectSystem status,
            Action<CombatMapObject> changed = null, Action<string> log = null, CombatFeedbackBus feedback = null)
        {
            this.session = session; this.damage = damage; this.status = status;
            this.changed = changed; this.log = log; this.feedback = feedback;
            Discover(); previousGate = session.CanUseBossSkill;
            session.CanUseBossSkill = CanUseSkill;
            session.SkillsFinished += CheckPhases;
            session.DamageApplied += OnDamage;
            session.SkillResolved += OnSkill;
        }
        static bool Alive(BattleUnit unit) => unit?.Character?.LifeState == CharacterLifeState.Alive && unit.Character.CurrentHealth > 0;
        void Discover()
        {
            foreach (var boss in session.Units.Where(u => u?.Character?.Definition?.Id == CatacombsBossSystem.BossId))
            {
                if (boss.Character.Definition.SecondPhaseDefinition == null) continue;
                boss.DeadKingState ??= new DeadKingCombatState { OriginalDefinition = boss.Character.Definition };
                if (boss.DeadKingState.ArenaRoomId == null && session.DungeonContext?.SurfaceMap != null)
                    session.DungeonContext.SurfaceMap.TryGetRoomId(new DungeonSurfacePosition(
                        DungeonBattleGridFactory.ToWorldPosition(session.DungeonContext, boss.Position), boss.Position.Level), out boss.DeadKingState.ArenaRoomId);
            }
        }
        public void CheckPhases()
        {
            Discover();
            if (session.SkillResolutionDepth > 0) return;
            foreach (var boss in session.Units.Where(u => u?.DeadKingState != null).ToArray())
            {
                var state = boss.DeadKingState;
                if (!Alive(boss)) { state.PhaseTwoPending = false; Clear(state); continue; }
                if (state.PhaseTwo || boss.Character.CurrentHealth > boss.Character.Stats.MaxHealth.GetValue() * .5f) continue;
                state.PhaseTwoPending = true;
            }
        }
        void OnDamage(BattleUnit source, BattleUnit target, int amount, DamageType type, string action)
        {
            if (session.SkillResolutionDepth == 0) CheckPhases();
        }
        void ApplyPendingPhase(BattleUnit boss, DeadKingCombatState state)
        {
            if (!state.PhaseTwoPending || state.PhaseTwo || !Alive(boss)) return;
            state.PhaseTwoPending = false;
            state.PhaseTwo = true;
            boss.Character.Definition = state.OriginalDefinition.SecondPhaseDefinition;
            boss.Character.Skills = new List<SkillData>(boss.Character.Definition.StartingSkills);
            new Godmist.Systems.Characters.SkillLoadoutSystem().TryInitialize(boss.Character);
            CombatTurnSystem.RefreshMaxActionPoints(session, boss);
            // Dismiss only this King's guards; no death rewards, bodies or unrelated summons.
            var summons = new SummonSystem(log);
            foreach (var knight in session.Summons.Where(s => s.CreatorCharacterId == boss.Character.CharacterId
                && s.SourceSkillId == CatacombsBossSystem.GuardSkillId
                && s.Unit?.Character?.Definition?.Id == KnightId).ToArray())
                summons.RemoveSummon(session, knight);
            log?.Invoke("Duch Umarłego Króla — Widmowy Szał!");
            feedback?.Publish(new CombatFeedbackEvent(boss, CombatFeedbackType.ProcTriggered, effectName: "Widmowy Szał"));
        }

        bool CanUseSkill(BattleUnit caster, SkillData skill)
        {
            if (previousGate?.Invoke(caster, skill) == false) return false;
            if (skill?.SkillId == ShadowId) return caster?.DeadKingState?.PhaseTwo == true && caster.DeadKingState.ShadowTurns == 0 && PlanSafeFields(caster).Count == 10;
            if (skill?.SkillId == BladeId) return session.Units.Any(t => Alive(t) && t.IsPlayerSide != caster.IsPlayerSide && CanBladeTarget(session,caster,t));
            return true;
        }
        public static bool Marked(BattleUnit target) => target?.Character?.PassiveEffects.Any(e =>
            e.Sources?.Any(s => s.SourceId.StartsWith(CommandId + ".", StringComparison.Ordinal)) == true) == true;
        public static float DamageMultiplier(CombatSession session, BattleUnit source, BattleUnit target, PassiveEffectCombatContext context)
        {
            var result = source?.DeadKingState?.PhaseTwo == true ? 1.3f : 1f;
            if (target?.DeadKingState?.PhaseTwo == true) result *= 1.5f;
            if (source != null && target != null && source.IsPlayerSide != target.IsPlayerSide && Marked(target)) result *= 1.2f;
            if (context?.AttackType == AttackType.Ranged && target != null && session?.Units?.Any(u => Alive(u)
                && u.IsPlayerSide == target.IsPlayerSide && u.DeadKingState != null && !u.DeadKingState.PhaseTwo) == true) result *= .75f;
            return result;
        }
        public static int KnightDamage(BattleUnit target, int amount, DamageType type)
        {
            if (target?.Character?.Definition?.Id != KnightId || amount <= 0) return amount;
            if (type == DamageType.Bleed || type == DamageType.Poison || type == DamageType.Burn) return 0;
            target.DeadKingKnightHits++;
            return target.DeadKingKnightHits >= 3 ? target.Character.CurrentHealth : 1;
        }
        public static bool CanBladeTarget(CombatSession session, BattleUnit caster, BattleUnit target) =>
            caster != null && target != null && caster.Position.Level == target.Position.Level && !StatusEffectSystem.IsImmobilized(caster) && Alive(target) && BladeDestination(session,caster,target).HasValue;
        static GridPosition? BladeDestination(CombatSession session, BattleUnit caster, BattleUnit target)
        {
            return session.Grid.GetNeighbours(target.Position).Where(p => session.Grid.IsWalkable(p)
                && !session.Grid.IsBlocked(p) && !session.IsPositionOccupied(p)
                && !session.IsPositionOccupiedByMapObject(p)
                && Math.Max(Math.Abs(p.X-caster.Position.X), Math.Abs(p.Y-caster.Position.Y)) <= 6)
                .OrderBy(p => Math.Abs(p.X-caster.Position.X)+Math.Abs(p.Y-caster.Position.Y)).Select(p => (GridPosition?)p).FirstOrDefault();
        }
        public static void BeforeSkill(CombatSession session, BattleUnit caster, SkillData skill,
            IReadOnlyDictionary<int, IReadOnlyList<BattleUnit>> targets)
        {
            if (skill.SkillId == CommandId)
            {
                var lifecycle = new PassiveEffectLifecycleSystem();
                foreach (var unit in session.Units)
                    foreach (var effect in unit.Character.PassiveEffects.Where(e => e.Sources?.Any(s => s.SourceId.StartsWith(CommandId + ".", StringComparison.Ordinal)) == true).ToArray())
                        lifecycle.RemoveEffect(unit.Character, effect.EffectId);
            }
            if (skill.SkillId != BladeId || !targets.TryGetValue(0, out var victims) || victims.Count == 0) return;
            var destination = BladeDestination(session, caster, victims[0]);
            if (destination.HasValue) new CombatTeleportSystem().TryTeleport(session,caster,caster,destination.Value,false);
        }
        List<GridPosition> Arena(BattleUnit boss)
        {
            var context = session.DungeonContext; var room = boss.DeadKingState.ArenaRoomId;
            return session.Grid.Positions.Where(p => p.Level == boss.Position.Level && session.Grid.IsWalkable(p)
                && !session.Grid.IsBlocked(p) && !session.IsPositionOccupiedByMapObject(p)
                && (room == null || context.SurfaceMap.TryGetRoomId(new DungeonSurfacePosition(
                    DungeonBattleGridFactory.ToWorldPosition(context,p),p.Level),out var id) && id == room)).ToList();
        }
        void OnSkill(BattleUnit caster, SkillData skill, string action)
        {
            if (skill.SkillId == CatacombsBossSystem.GuardSkillId && caster?.DeadKingState != null)
            {
                var definition = skill.Effects[0].SummonedCharacterDefinition;
                var summons = new SummonSystem(log);
                while (session.Summons.Count(s => Alive(s.Unit) && s.CreatorCharacterId == caster.Character.CharacterId && s.SourceSkillId == skill.SkillId) < 3)
                    if (!summons.TrySummonAdjacent(session,caster,definition,definition.ClassData,skill.SkillId,99,3,1f,out var summoned,caster.Character.Level)) break;
                    else
                    {
                        summoned.Character.CharacterId += "." + Guid.NewGuid().ToString("N");
                        CombatTargetVfxFeedback.PublishSummon(feedback, caster, summoned, skill, action);
                    }
            }
            if (skill.SkillId != ShadowId || caster?.DeadKingState == null) return;
            var state = caster.DeadKingState; var arena = Arena(caster);
            var safe = PlanSafeFields(caster);
            if (safe.Count != 10) return;
            state.SafeFields.UnionWith(safe);
            state.DarkFields.UnionWith(arena.Where(p => !safe.Contains(p)));
            state.ShadowTurns = 2;
            foreach(var cell in arena)
            {
                var definition = safe.Contains(cell) ? caster.Character.Definition.SanctuaryWarning : caster.Character.Definition.SanctuaryFire;
                if(definition == null) continue;
                var marker = new SummonedCombatMapObject("deadking.shadow."+Guid.NewGuid().ToString("N"),cell,caster.Character.CharacterId,ShadowId,0,definition);
                session.SummonedObjects.Add(marker); session.MapObjects.Add(marker); state.Markers.Add(marker); changed?.Invoke(marker);
            }
            log?.Invoke("Cień Destrukcji — pozostań na jasnych polach. Wybuch za dwie tury Króla.");
            CombatBossVfxFeedback.PublishState(feedback, caster, skill.SkillId, action);
        }
        HashSet<GridPosition> PlanSafeFields(BattleUnit caster)
        {
            var arena = Arena(caster); var safe = new HashSet<GridPosition>();
            foreach (var unit in session.Units.Where(u => Alive(u) && u.IsPlayerSide != caster.IsPlayerSide))
            {
                if (!arena.Contains(unit.Position)) continue;
                if (StatusEffectSystem.IsImmobilized(unit) || StatusEffectSystem.IsStunned(unit)) { safe.Add(unit.Position); continue; }
                var refuge = session.Grid.GetNeighbours(unit.Position).Where(p => arena.Contains(p) && !safe.Contains(p)
                    && !session.IsPositionOccupied(p) && session.CanMoveBetween(unit,unit.Position,p)).Select(p => (GridPosition?)p).FirstOrDefault();
                safe.Add(refuge ?? unit.Position);
            }
            var reachable = new HashSet<GridPosition>(safe); var queue = new Queue<GridPosition>(safe);
            while(queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach(var next in session.Grid.GetNeighbours(current))
                    if(arena.Contains(next) && session.Grid.CanMoveBetween(current,next)
                        && !session.IsPositionOccupied(next) && reachable.Add(next)) queue.Enqueue(next);
            }
            foreach(var cell in reachable.OrderBy(p => p.Y).ThenBy(p => p.X))
                if(safe.Count < 10) safe.Add(cell);
            return safe;
        }
        public void BeginTurn(BattleUnit unit)
        {
            CheckPhases();
            var state = unit?.DeadKingState;
            if (state != null) ApplyPendingPhase(unit, state);
            if(state == null || state.ShadowTurns <= 0 || !Alive(unit)) return;
            if(--state.ShadowTurns > 0) return;
            var presentation = Guid.NewGuid().ToString("N");
            feedback?.Publish(new CombatFeedbackEvent(unit, CombatFeedbackType.ProcTriggered,
                effectName: ShadowImpactCue, actionId: presentation));
            foreach(var target in session.Units.Where(t => Alive(t) && t.IsPlayerSide != unit.IsPlayerSide && state.DarkFields.Contains(t.Position)).ToArray())
                for(var hit=0;hit<3 && Alive(target);hit++)
                {
                    var impactPosition = target.Position;
                    var result = damage.ResolveAndApplyDamage(session,unit,target,
                        CombatDamageValueCalculator.CalculateDamageValue(unit,DamageValueSource.AttackRange,1f),DamageType.Magical,true);
                    CombatBossVfxFeedback.PublishContact(feedback, unit, target, result, CombatTargetVfxKind.ShadowImpact, hit, presentation, impactPosition);
                }
            CombatBossVfxFeedback.PublishResolutionArea(feedback, unit, state.DarkFields, CombatTargetVfxKind.ShadowGround, presentation);
            Clear(state);
            if(Alive(unit)) status.ApplyGuaranteedStun(unit,unit,null,-1,2);
        }
        void Clear(DeadKingCombatState state)
        {
            foreach(var marker in state.Markers) { session.SummonedObjects.Remove(marker);session.MapObjects.Remove(marker);changed?.Invoke(marker); }
            state.Markers.Clear();state.SafeFields.Clear();state.DarkFields.Clear();state.ShadowTurns=0;
        }
        public void OnBossDied(BattleUnit unit) { if(unit?.DeadKingState != null) { unit.DeadKingState.PhaseTwoPending = false; Clear(unit.DeadKingState); } }
        public void Dispose() => Dispose(false);
        public void Dispose(bool preserve)
        {
            session.SkillsFinished -= CheckPhases;session.SkillResolved -= OnSkill;
            session.DamageApplied -= OnDamage;
            session.CanUseBossSkill = previousGate;
            if(preserve) return;
            foreach(var boss in session.Units.Where(u => u?.DeadKingState != null))
            {
                Clear(boss.DeadKingState);
                boss.Character.Definition=boss.DeadKingState.OriginalDefinition;
                boss.Character.Skills=new List<SkillData>(boss.Character.Definition.StartingSkills);
                new Godmist.Systems.Characters.SkillLoadoutSystem().TryInitialize(boss.Character);
                boss.DeadKingState=null;
            }
        }
    }
}
