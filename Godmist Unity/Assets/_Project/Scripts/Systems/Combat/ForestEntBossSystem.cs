using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Combat;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    /// <summary>Encounter-local Ent phases, plague and boss-turn terrain. No frame polling.</summary>
    public sealed class ForestEntBossSystem : IDisposable
    {
        public const string BossId = "enemy.forest.infected_ent";
        public const string PhaseTwoId = BossId + ".burning";
        public const string SanctuarySkillId = "skill.enemy.forest.ent_sanctuary";
        public const string BarkSkillId = "skill.enemy.forest.ent_bark";
        public const string PlagueId = "status.forest.demonic_plague";
        private readonly CombatSession session;
        private readonly CombatDamageSystem damage;
        private readonly StatusEffectSystem status;
        private readonly CombatRecoverySystem recovery;
        private readonly Action<CombatMapObject> changed;
        private readonly Action<string> log;
        private readonly CombatFeedbackBus feedback;
        private readonly Dictionary<BattleUnit, ForestEntCombatState> bosses = new();
        public ForestEntBossSystem(CombatSession session, CombatDamageSystem damage,
            StatusEffectSystem status, CombatRecoverySystem recovery,
            Action<CombatMapObject> changed = null, Action<string> log = null, CombatFeedbackBus feedback = null)
        {
            this.session = session; this.damage = damage; this.status = status;
            this.recovery = recovery; this.changed = changed; this.log = log; this.feedback = feedback;
            DiscoverBosses();
            session.DamageApplied += OnDamage;
            session.SkillResolved += OnSkill;
            session.SkillsFinished += CheckPhases;
            session.UnitEnteredCell += EnterCell;
            session.CanUseBossSkill = CanUseSkill;
        }
        public bool IsPhaseTwo(BattleUnit boss) => bosses.TryGetValue(boss, out var s) && s.PhaseTwo;
        public int PlagueStacks(BattleUnit boss, BattleUnit target) =>
            bosses.TryGetValue(boss, out var s) && s.Plague.TryGetValue(target, out var count) ? count : 0;
        public IReadOnlyCollection<GridPosition> BurningFields(BattleUnit boss) =>
            bosses.TryGetValue(boss, out var s) ? s.Fields : Array.Empty<GridPosition>();
        public bool IsWarning(BattleUnit boss) => bosses.TryGetValue(boss, out var s) && s.Warning;
        private static bool Alive(BattleUnit u) => u?.Character?.LifeState == CharacterLifeState.Alive
            && u.Character.CurrentHealth > 0;
        public bool CanUseSkill(BattleUnit caster, SkillData skill)
        {
            if (skill?.SkillId != SanctuarySkillId) return true;
            return bosses.TryGetValue(caster, out var s) && s.PhaseTwo && Alive(caster)
                && s.Fields.Count == 0 && Arena(caster).Count >= 2;
        }
        private void DiscoverBosses()
        {
            foreach (var unit in session.Units)
                if (unit != null && (unit.Character?.Definition?.Id == BossId || unit.ForestEntState != null) && !bosses.ContainsKey(unit))
                {
                    unit.ForestEntState ??= new ForestEntCombatState { OriginalDefinition = unit.Character.Definition };
                    if (unit.ForestEntState.ArenaRoomId == null && session.DungeonContext?.SurfaceMap != null
                        && session.DungeonContext.SurfaceMap.TryGetRoomId(
                            new Godmist.Runtime.Dungeons.DungeonSurfacePosition(
                                DungeonBattleGridFactory.ToWorldPosition(session.DungeonContext, unit.Position), unit.Position.Level), out var room))
                        unit.ForestEntState.ArenaRoomId = room;
                    bosses.Add(unit, unit.ForestEntState);
                }
        }
        public void CheckPhases()
        {
            DiscoverBosses();
            if (session.SkillResolutionDepth > 0) return;
            foreach (var pair in bosses.ToArray())
            {
                var boss = pair.Key; var s = pair.Value;
                if (!Alive(boss)) { s.PhaseTwoPending=false; ClearTerrain(s); continue; }
                var definition = boss.Character.Definition;
                if (s.PhaseTwo || definition.SecondPhaseDefinition == null
                    || boss.Character.CurrentHealth > boss.Character.Stats.MaxHealth.GetValue() * .5f) continue;
                // Latch the threshold; form and skillset change only on this boss's next turn.
                s.PhaseTwoPending = true;
            }
        }
        private void ApplyPendingPhase(BattleUnit boss, ForestEntCombatState s)
        {
            var definition=boss.Character.Definition;
            if (!s.PhaseTwoPending || s.PhaseTwo || !Alive(boss) || definition.SecondPhaseDefinition == null) return;
            s.PhaseTwoPending=false;
            s.PhaseTwo = true;
            var context = session.DungeonContext;
            if (s.ArenaRoomId == null && context?.SurfaceMap != null && context.SurfaceMap.TryGetRoomId(
                new Godmist.Runtime.Dungeons.DungeonSurfacePosition(
                    DungeonBattleGridFactory.ToWorldPosition(context, boss.Position), boss.Position.Level), out var arena))
                s.ArenaRoomId = arena;
            // Remove only self-authored first-phase buffs; all debuffs and other units survive.
            var lifecycle = new PassiveEffectLifecycleSystem();
            foreach (var effect in boss.Character.PassiveEffects.ToArray())
                if (effect.Sources?.Any(source => source.SourceCharacterId == boss.Character.CharacterId
                    && source.SourceId.StartsWith(BarkSkillId + ".", StringComparison.Ordinal)) == true)
                    lifecycle.RemoveEffect(boss.Character, effect.EffectId);
            boss.Character.Definition = definition.SecondPhaseDefinition;
            boss.Character.Skills = new List<SkillData>(definition.SecondPhaseDefinition.StartingSkills);
            new Godmist.Systems.Characters.SkillLoadoutSystem().TryInitialize(boss.Character);
            CombatTurnSystem.RefreshMaxActionPoints(session, boss);
            log?.Invoke("Zarażony Ent płonie spaczonym ogniem — Demoniczna Zaraza!");
            feedback?.Publish(new CombatFeedbackEvent(boss, CombatFeedbackType.ProcTriggered,
                effectName: "Demoniczna Zaraza"));
        }
        private void OnSkill(BattleUnit caster, SkillData skill, string action)
        {
            if (skill?.SkillId == SanctuarySkillId && bosses.TryGetValue(caster, out var s))
                MarkSanctuary(caster, s);
            foreach (var state in bosses.Values) state.ActionHits.RemoveWhere(hit => hit.Item1 == action);
        }
        private void OnDamage(BattleUnit source, BattleUnit target, int amount, DamageType type, string action)
        {
            if (source?.ForestEntState != null && !bosses.ContainsKey(source)) bosses.Add(source, source.ForestEntState);
            if (amount > 0 && Alive(target) && source != null && bosses.TryGetValue(source, out var s)
                && s.PhaseTwo && Alive(source) && source.IsPlayerSide != target.IsPlayerSide)
            {
                var periodic = type == DamageType.Bleed || type == DamageType.Poison || type == DamageType.Burn
                    || string.IsNullOrEmpty(action);
                var add = periodic ? s.PeriodicHits.Add(target) : s.ActionHits.Add((action, target));
                if (add && (!s.Plague.TryGetValue(target, out var count) || count < 5))
                {
                    s.Plague[target] = Mathf.Min(5, count + 1);
                    var id = PlagueId + "." + source.Character.CharacterId;
                    var runtime = target.Character.PassiveEffects.Find(e => e.EffectId == id);
                    if (runtime == null)
                    {
                        runtime = new PassiveEffectRuntime { EffectId = id, DisplayName = "Demoniczna Zaraza",
                            Lifetime = PassiveEffectLifetime.CombatScoped };
                        target.Character.PassiveEffects.Add(runtime);
                    }
                    runtime.Stacks = s.Plague[target];
                    feedback?.Publish(new CombatFeedbackEvent(target, CombatFeedbackType.StatusApplied,
                        effectName: "Demoniczna Zaraza", source: source));
                }
            }
            if (session.SkillResolutionDepth == 0) CheckPhases();
        }
        public void BeginTurn(BattleUnit unit)
        {
            CheckPhases();
            if (unit != null && bosses.TryGetValue(unit,out var ownState)) ApplyPendingPhase(unit,ownState);
            foreach (var pair in bosses.ToArray())
            {
                var boss = pair.Key; var s = pair.Value;
                s.PeriodicHits.Remove(unit); s.FireHits.Remove(unit);
                if (!Alive(boss)) ClearTerrain(s);
                if (s.Plague.TryGetValue(unit, out var stacks) && Alive(unit))
                {
                    // No source: plague cannot apply itself or trigger the Ent's damage procs.
                    damage.ApplyDamage(session, null, unit,
                        Mathf.RoundToInt(unit.Character.Stats.MaxHealth.GetValue() * .005f * stacks),
                        DamageType.Direct, allowWarriorProcs: false);
                    s.ManaCarry += stacks * .5f;
                    var mana = Mathf.FloorToInt(s.ManaCarry); s.ManaCarry -= mana;
                    recovery.RestoreResource(boss, mana);
                }
                if (unit == boss && s.Fields.Count > 0)
                {
                    if (s.Warning) { s.Warning = false; s.FireTurns = 3; ShowMarkers(boss, s); }
                    else if (--s.FireTurns <= 0) ClearTerrain(s);
                }
                HitFire(boss, s, unit);
            }
            CheckPhases();
        }
        public void EndTurn(BattleUnit unit)
        {
            if (!bosses.TryGetValue(unit, out var s) || s.PhaseTwo || !Alive(unit)) return;
            foreach (var target in session.Units.ToArray())
                if (Alive(target) && target.IsPlayerSide != unit.IsPlayerSide
                    && target.Position.Level == unit.Position.Level
                    && Mathf.Max(Mathf.Abs(target.Position.X - unit.Position.X), Mathf.Abs(target.Position.Y - unit.Position.Y)) <= 3)
                    status.ApplyStatus(unit, target, StatusEffectIds.Poison, .5f, 1, 1, 1f, 0f, "forest.ent.rotten_aura");
        }
        private void EnterCell(BattleUnit unit)
        { foreach (var pair in bosses) HitFire(pair.Key, pair.Value, unit); }
        private void HitFire(BattleUnit boss, ForestEntCombatState s, BattleUnit unit)
        {
            if (s.Warning || s.FireTurns <= 0 || !Alive(boss) || !Alive(unit)
                || unit.IsPlayerSide == boss.IsPlayerSide || !s.Fields.Contains(unit.Position) || !s.FireHits.Add(unit)) return;
            var impactPosition = unit.Position;
            var result = damage.ResolveAndApplyDamage(session, boss, unit,
                CombatDamageValueCalculator.CalculateDamageValue(boss, DamageValueSource.AttackRange, 1f),
                DamageType.Magical, true);
            CombatBossVfxFeedback.PublishContact(feedback, boss, unit, result, CombatTargetVfxKind.Fire, 0, null, impactPosition);
            if (Alive(unit)) status.ApplyStatus(boss, unit, StatusEffectIds.Burn, .7f, 2, 1, .4f, 0f, SanctuarySkillId);
        }
        private List<GridPosition> Arena(BattleUnit boss)
        {
            var context = session.DungeonContext;
            string room = null;
            if (context?.SurfaceMap != null && !context.SurfaceMap.TryGetRoomId(
                new Godmist.Runtime.Dungeons.DungeonSurfacePosition(
                    DungeonBattleGridFactory.ToWorldPosition(context, boss.Position), boss.Position.Level), out room))
                return new List<GridPosition>();
            return session.Grid.Positions.Where(p => p.Level == boss.Position.Level && session.Grid.IsWalkable(p)
                && !session.Grid.IsBlocked(p) && !session.IsPositionOccupiedByMapObject(p)
                && (room == null || context.SurfaceMap.TryGetRoomId(new Godmist.Runtime.Dungeons.DungeonSurfacePosition(
                    DungeonBattleGridFactory.ToWorldPosition(context, p), p.Level), out var id) && id == room)).ToList();
        }
        private void MarkSanctuary(BattleUnit boss, ForestEntCombatState s)
        {
            var arena = Arena(boss); var available = new HashSet<GridPosition>(arena);
            // Reserve an adjacent, actually reachable refuge for every living opponent.
            // Movement checks account for walls/doors and current occupants.
            var safe = new HashSet<GridPosition>();
            foreach (var unit in session.Units.Where(u => Alive(u) && u.IsPlayerSide != boss.IsPlayerSide))
            {
                var refuge = session.Grid.GetNeighbours(unit.Position).Where(p => available.Contains(p)
                    && session.CanMoveBetween(unit, unit.Position, p) && !session.IsPositionOccupied(p)).ToList();
                if (refuge.Count > 0) safe.Add(refuge[UnityEngine.Random.Range(0, refuge.Count)]);
                else safe.Add(unit.Position);
            }
            var candidates = arena.Where(p => !safe.Contains(p)).ToList();
            for (var i = candidates.Count - 1; i > 0; i--)
            { var j = UnityEngine.Random.Range(0, i + 1); (candidates[i], candidates[j]) = (candidates[j], candidates[i]); }
            s.Fields.UnionWith(candidates.Take(Mathf.Min(arena.Count / 2, candidates.Count)));
            s.Warning = s.Fields.Count > 0;
            ShowMarkers(boss, s);
            log?.Invoke("Piekielne Sanktuarium — oznaczone pola zapłoną na początku następnej tury Enta.");
        }
        private void ShowMarkers(BattleUnit boss, ForestEntCombatState s)
        {
            RemoveMarkers(s);
            var definition = s.Warning ? boss.Character.Definition.SanctuaryWarning : boss.Character.Definition.SanctuaryFire;
            if (definition == null) return;
            foreach (var cell in s.Fields)
            {
                var marker = new SummonedCombatMapObject("forest.ent.terrain." + Guid.NewGuid().ToString("N"), cell,
                    boss.Character.CharacterId, SanctuarySkillId, 0, definition);
                session.SummonedObjects.Add(marker); session.MapObjects.Add(marker); s.Markers.Add(marker);
                // Deliberately no turn participant per tile; the boss owns duration and damage.
                changed?.Invoke(marker);
            }
        }
        private void RemoveMarkers(ForestEntCombatState s)
        {
            foreach (var marker in s.Markers)
            { session.SummonedObjects.Remove(marker); session.MapObjects.Remove(marker); changed?.Invoke(marker); }
            s.Markers.Clear();
        }
        private void ClearTerrain(ForestEntCombatState s)
        { RemoveMarkers(s); s.Fields.Clear(); s.Warning = false; s.FireTurns = 0; s.FireHits.Clear(); }
        private void Clear(BattleUnit boss, ForestEntCombatState s)
        {
            ClearTerrain(s);
            foreach (var target in s.Plague.Keys)
                target.Character?.PassiveEffects?.RemoveAll(e => e.EffectId == PlagueId + "." + boss.Character.CharacterId);
            s.Plague.Clear(); s.ActionHits.Clear(); s.PeriodicHits.Clear();
        }
        public void Dispose() => Dispose(false);
        public void Dispose(bool preserveCombatState)
        {
            session.DamageApplied -= OnDamage; session.SkillResolved -= OnSkill;
            session.SkillsFinished -= CheckPhases; session.UnitEnteredCell -= EnterCell;
            session.CanUseBossSkill = null;
            if (!preserveCombatState)
                foreach (var pair in bosses)
                {
                    Clear(pair.Key, pair.Value);
                    if (Alive(pair.Key) && pair.Value.OriginalDefinition != null)
                    {
                        pair.Key.Character.Definition = pair.Value.OriginalDefinition;
                        pair.Key.Character.Skills = new List<SkillData>(pair.Value.OriginalDefinition.StartingSkills);
                        new Godmist.Systems.Characters.SkillLoadoutSystem().TryInitialize(pair.Key.Character);
                    }
                    pair.Key.ForestEntState = null;
                }
        }
    }
}
