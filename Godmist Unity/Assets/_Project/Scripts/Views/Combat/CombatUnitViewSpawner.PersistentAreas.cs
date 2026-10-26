using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using Godmist.Views.Characters;
using UnityEngine;

namespace Godmist.Views.Combat
{
    public partial class CombatUnitViewSpawner
    {
        sealed class AreaVfx
        {
            public CombatSession Session;
            public BattleUnit Owner;
            public GridPosition Anchor;
            public int Radius;
            public float NextGeometry;
            public bool Seen;
            public CombatTargetVfxClip Clip;
            public readonly Dictionary<GridPosition, SpriteRenderer> Tiles = new();
        }
        readonly Dictionary<object, AreaVfx> persistentAreas = new();
        readonly List<CombatSession> areaSessions = new();
        readonly List<object> staleAreas = new();
        const int AreaRendererBudget = 128;

        void TickPersistentAreaVfx()
        {
            areaSessions.Clear();
            if (dungeonSession != null) areaSessions.AddRange(dungeonSession.Combats);
            else if (sandboxBootstrap?.Session != null) areaSessions.Add(sandboxBootstrap.Session);
            RefreshPersistentAreaVfx(areaSessions, Time.time);
        }

        // Public to support deterministic Editor verification without entering Play Mode.
        public void RefreshPersistentAreaVfx(IReadOnlyList<CombatSession> sessions, float elapsed)
        {
            foreach (var state in persistentAreas.Values) state.Seen = false;
            foreach (var session in sessions)
            {
                if (session?.Grid == null) continue;
                foreach (var field in session.SummonedObjects)
                {
                    if (!PaladinCombatSystem.IsField(field) || field.IsExpired()) continue;
                    var source = session.Units.FirstOrDefault(u => u.Character?.CharacterId == field.CreatorCharacterId);
                    if (source == null) continue;
                    UpdateArea(field, session, null, field.Position, 2 + PaladinCombatSystem.C(source, "field_range"),
                        "judgement", elapsed);
                }
                foreach (var unit in session.Units)
                {
                    if (unit?.Character?.LifeState != CharacterLifeState.Alive) continue;
                    foreach (var aura in unit.ActiveAuras)
                        if (aura?.AuraId?.StartsWith("skill.mage.fire_aura.aura.", System.StringComparison.Ordinal) == true
                            && aura.RemainingTurns > 0)
                            UpdateArea(aura, session, unit, unit.Position, aura.Radius, "aura-embers", elapsed);
                }
            }
            staleAreas.Clear();
            foreach (var pair in persistentAreas)
            {
                var state = pair.Value;
                if (!state.Seen) { DestroyArea(state); staleAreas.Add(pair.Key); continue; }
                var sprite = state.Clip.FrameAt(elapsed, true);
                foreach (var tile in state.Tiles)
                {
                    tile.Value.sprite = sprite;
                    tile.Value.forceRenderingOff = !IsPositionVisible(state.Session, tile.Key)
                        || StatusEffectSystem.IsInvisible(state.Owner);
                }
            }
            foreach (var key in staleAreas) persistentAreas.Remove(key);
        }

        void UpdateArea(object key, CombatSession session, BattleUnit owner, GridPosition anchor, int radius,
            string clipId, float elapsed)
        {
            if (!persistentAreas.TryGetValue(key, out var state))
            {
                if (!CombatTargetVfxResolver.TryResolve(clipId, out var clip)) return;
                state = new AreaVfx { Clip = clip, NextGeometry = float.NegativeInfinity };
                persistentAreas.Add(key, state);
            }
            state.Seen = true; state.Owner = owner;
            // Bounded local geometry, not a scan of the dungeon. Reuse renderers;
            // refresh floor/LOS after destructible changes, also while stationary.
            if (state.Session == session && state.Anchor == anchor && state.Radius == radius && elapsed < state.NextGeometry) return;
            state.Session = session; state.Anchor = anchor; state.Radius = radius; state.NextGeometry = elapsed + .2f;
            var positions = new HashSet<GridPosition>(key is SummonedCombatMapObject field
                ? CombatPersistentVfxGeometry.FieldCells(session, field)
                : CombatPersistentVfxGeometry.AuraCells(session, owner, (CombatAuraRuntime)key));
            foreach (var tile in state.Tiles.Where(p => !positions.Contains(p.Key)).ToArray())
            { DestroyAreaTile(tile.Value); state.Tiles.Remove(tile.Key); }
            foreach (var position in positions)
            {
                if (state.Tiles.ContainsKey(position) || persistentAreas.Values.Sum(a => a.Tiles.Count) >= AreaRendererBudget) continue;
                var go = new GameObject("Persistent combat area " + clipId);
                go.transform.SetParent(summonedObjectsRoot != null ? summonedObjectsRoot : transform, false);
                go.transform.position = (floorMap != null && session.DungeonContext != null ? WorldPosition(session, position)
                    : GridToWorldPosition(position)) + (Vector3)state.Clip.Offset;
                var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = state.Clip.Frames[0];
                renderer.sortingOrder = 3; renderer.color = new Color(1, 1, 1, .65f);
                state.Tiles.Add(position, renderer);
            }
        }
        void DestroyAreaTile(SpriteRenderer renderer)
        {
            if (renderer == null) return;
            renderer.forceRenderingOff = true; renderer.enabled = false;
            DestroyPresentationObject(renderer.gameObject);
        }
        void DestroyArea(AreaVfx state) { foreach (var renderer in state.Tiles.Values) DestroyAreaTile(renderer); state.Tiles.Clear(); }
        void ClearPersistentAreaVfx()
        { foreach (var state in persistentAreas.Values) DestroyArea(state); persistentAreas.Clear(); areaSessions.Clear(); }
    }
}
