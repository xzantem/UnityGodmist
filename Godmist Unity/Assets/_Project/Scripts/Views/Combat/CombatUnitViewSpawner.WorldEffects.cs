using System.Collections.Generic;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using Godmist.Views.Characters;
using UnityEngine;

namespace Godmist.Views.Combat
{
    public partial class CombatUnitViewSpawner
    {
        private sealed class WorldEffect
        {
            public SpriteRenderer Renderer;
            public CombatSession Session;
            public GridPosition Position;
            public float Ends;
            public float Starts;
            public BattleUnit Target;
            public string ActionId;
            public CombatTargetVfxKind? Kind;
        }
        private readonly List<WorldEffect> worldEffects = new();

        public bool TryPlayTargetVfx(CombatSession session, CombatFeedbackEvent feedback, float? presentationEndsAt = null)
        {
            if (session == null || feedback.Target == null || !feedback.TargetVfx.HasValue) return false;
            var ground = CombatTargetVfxFeedback.IsGround(feedback.TargetVfx.Value);
            if (!ground && StatusEffectSystem.IsInvisible(feedback.Target)) return false;
            var delay = Mathf.Max(0, feedback.CueDelaySeconds);
            if (presentationEndsAt.HasValue && Time.time + delay >= presentationEndsAt.Value) return false;
            // Repeated hits retain their ordered feedback but do not pile identical
            // static sprites on the same cell for the same skill action.
            if (presentationEndsAt.HasValue && !string.IsNullOrEmpty(feedback.ActionId))
                foreach (var existing in worldEffects)
                    if (existing.Session == session && existing.ActionId == feedback.ActionId
                        && existing.Kind == feedback.TargetVfx && existing.Position == feedback.Position
                        && existing.Target == (ground ? null : feedback.Target) && existing.Renderer != null
                        && existing.Ends > Time.time)
                        return true;
            if (feedback.TargetVfx == CombatTargetVfxKind.BanditOrder)
            {
                // Reuse the approved target mark, but only after a real buff succeeds.
                if (!CombatPoseResolver.TryResolveSkill(BanditLeaderBossSystem.BossId, BanditLeaderBossSystem.OrderId,
                    Vector2.down, Godmist.Core.Enums.AttackType.Unknown, false, out var orderPose)) return false;
                return TryPlayTargetHeadEffects(session, feedback.Target, orderPose.TargetHeadEffects,
                    presentationEndsAt.HasValue ? presentationEndsAt.Value - Time.time : .5f, orderPose.TargetHeadOffsets);
            }
            var id = feedback.TargetVfx.Value switch
            {
                CombatTargetVfxKind.Heal => "heal",
                CombatTargetVfxKind.CutBlood => "cut",
                CombatTargetVfxKind.CutFragments => "fragments",
                CombatTargetVfxKind.Fire => "fire",
                CombatTargetVfxKind.Blunt => "blunt",
                CombatTargetVfxKind.EnergyImpact => "energy-impact",
                CombatTargetVfxKind.EnergyGround => "energy-ground",
                CombatTargetVfxKind.FireGround => "fire-ground",
                CombatTargetVfxKind.PierceBlood => "pierce",
                CombatTargetVfxKind.SmokeGround => "smoke",
                CombatTargetVfxKind.SmokeHead => "smoke-hit",
                CombatTargetVfxKind.ArrowGround => "arrow-ground",
                CombatTargetVfxKind.ArcaneWard => "arcane-ward",
                CombatTargetVfxKind.HolyGrace => "holy-grace",
                CombatTargetVfxKind.Rally => "rally",
                CombatTargetVfxKind.HolyImpact => "holy-impact",
                CombatTargetVfxKind.HolyBlind => "holy-blind",
                CombatTargetVfxKind.ArcaneFocus => "arcane-focus",
                CombatTargetVfxKind.PullDust => "pull-dust",
                CombatTargetVfxKind.Shout => "shout",
                CombatTargetVfxKind.KnifeRecover => "knife-recover",
                CombatTargetVfxKind.DestructionMark => "destruction-mark-hit",
                CombatTargetVfxKind.WeakPointMark => "weakpoint-mark-hit",
                CombatTargetVfxKind.Sacrifice => "sacrifice",
                CombatTargetVfxKind.Transfusion => "transfusion",
                CombatTargetVfxKind.DeathWish => "death-wish-hit",
                CombatTargetVfxKind.Offering => "offering-hit",
                CombatTargetVfxKind.Fatigue => "fatigue-hit",
                CombatTargetVfxKind.Tonic => "tonic",
                CombatTargetVfxKind.FangContact => "fang-contact",
                CombatTargetVfxKind.SpectralContact => "spectral-contact",
                CombatTargetVfxKind.AncientContact => "ancient-contact",
                CombatTargetVfxKind.SpectralLanding => "spectral-landing",
                CombatTargetVfxKind.BoneWard => "bone-ward",
                CombatTargetVfxKind.SpectralDeparture => "spectral-departure",
                CombatTargetVfxKind.SpectralReveal => "spectral-reveal",
                CombatTargetVfxKind.LifeSiphon => "life-siphon",
                CombatTargetVfxKind.ToxicContact => "toxic-splash",
                CombatTargetVfxKind.ToxicGround => "toxic-ground",
                CombatTargetVfxKind.RootContact => "root-contact",
                CombatTargetVfxKind.GrowthGround => "growth-ground",
                CombatTargetVfxKind.FlareHead => "flare-flash",
                CombatTargetVfxKind.FlareGround => "flare-ground",
                CombatTargetVfxKind.RoyalCommand => "royal-command-hit",
                CombatTargetVfxKind.SlayerWard => "slayer-ward-hit",
                CombatTargetVfxKind.HammerCharge => "hammer-charge-hit",
                CombatTargetVfxKind.ShadowImpact => "shadow-impact",
                CombatTargetVfxKind.ShadowGround => "shadow-ground",
                CombatTargetVfxKind.BarkWard => "bark-ward",
                CombatTargetVfxKind.WoodImpact => "wood-impact",
                CombatTargetVfxKind.WoodGround => "wood-ground",
                CombatTargetVfxKind.MineBurst => "mine-burst",
                CombatTargetVfxKind.TrapGround or CombatTargetVfxKind.TrapContact => "trap-snap",
                _ => "tremor"
            };
            if (!CombatTargetVfxResolver.TryResolve(id, out var clip)) return false;
            var order = 3;
            var offset = clip.Offset;
            if (!ground)
            {
                SpriteRenderer body = null;
                if (borrowed.TryGetValue(feedback.Target, out var exploration) && exploration != null) body = exploration.BodyRenderer;
                else if (views.TryGetValue(feedback.Target, out var view) && view != null) body = view.BodyRenderer;
                else body=ExplorationView?.Invoke(feedback.Target)?.BodyRenderer;
                var group = body != null ? body.GetComponentInParent<UnityEngine.Rendering.SortingGroup>() : null;
                order = (group != null && group.enabled ? group.sortingOrder : body != null ? body.sortingOrder : 10) + 5;
                if (clip.AboveHead && body != null)
                {
                    var bounds = CombatTargetVfxResolver.ActorVisibleBounds(body.sprite);
                    var head = body.transform.TransformPoint(new Vector3(bounds.center.x,
                        bounds.max.y + clip.ContentHeight * .5f + 2f / 32f, 0));
                    var floor = floorMap != null && session.DungeonContext != null
                        ? WorldPosition(session, feedback.Position) : GridToWorldPosition(feedback.Position);
                    offset += (Vector2)(head - floor);
                }
                else if(ExplorationView?.Invoke(feedback.Target) is {} free)
                {
                    var floor=floorMap!=null&&session.DungeonContext!=null?WorldPosition(session,feedback.Position):GridToWorldPosition(feedback.Position);
                    offset+=(Vector2)(free.transform.position-floor);
                }
            }
            // Skill execution is a one-frame freeze. Only persistent statuses animate.
            var duration = presentationEndsAt.HasValue ? presentationEndsAt.Value - Time.time - delay : clip.Duration;
            if (!TryPlayWorldEffect(session, feedback.Position, clip.Frames[0], offset, duration, order, delay)) return false;
            var effect = worldEffects[worldEffects.Count - 1];
            effect.Target = ground ? null : feedback.Target;
            effect.ActionId = feedback.ActionId;
            effect.Kind = feedback.TargetVfx;
            if (presentationEndsAt.HasValue) effect.Ends = presentationEndsAt.Value;
            // Sparks and magical/fire floor marks are emissive. Physical floor
            // marks keep ground lighting; all still obey the same visibility gate.
            if (effect.Target != null || feedback.TargetVfx is CombatTargetVfxKind.EnergyGround or CombatTargetVfxKind.FireGround
                or CombatTargetVfxKind.SpectralLanding or CombatTargetVfxKind.SpectralDeparture
                or CombatTargetVfxKind.ToxicGround or CombatTargetVfxKind.GrowthGround or CombatTargetVfxKind.FlareGround
                or CombatTargetVfxKind.ShadowGround or CombatTargetVfxKind.MineBurst)
                floorMap?.UnregisterLightingPresentation(effect.Renderer);
            return true;
        }

        public bool TryPlayMapObjectTrigger(CombatSession session, SummonedCombatMapObject mapObject)
        {
            var definition=mapObject?.Definition;
            if(definition==null || session==null || !IsPositionVisible(session,mapObject.Position))return false;
            if (definition.TriggerSprite == null && CombatTargetVfxFeedback.IsBearTrap(mapObject)
                && CombatTargetVfxResolver.TryResolve("trap-snap", out var snap))
                return TryPlayWorldEffect(session, mapObject.Position, snap.Frames[0], snap.Offset,
                    snap.Duration, summonedObjectSortingOrder + 2);
            if (definition.TriggerSprite == null) return false;
            // Hidden traps have no standing view. Show their registered footprint only
            // at activation, alongside the burst, and release both even if combat ends.
            var duration = CombatTargetVfxFeedback.TriggerDuration(mapObject);
            if(definition.WorldSprite!=null)
                TryPlayWorldEffect(session,mapObject.Position,definition.WorldSprite,definition.WorldSpriteOffset,duration,summonedObjectSortingOrder);
            return TryPlayWorldEffect(session,mapObject.Position,definition.TriggerSprite,definition.TriggerSpriteOffset,duration,summonedObjectSortingOrder+2);
        }

        public bool TryPlayWorldEffect(CombatSession session, GridPosition position, Sprite sprite, Vector2 offset, float duration, int sortingOrder=3,
            float delaySeconds=0)
        {
            if(session==null || sprite==null || !IsPositionVisible(session,position))return false;
            // A new cast can start in Update before LateUpdate releases the last
            // cast's expired floor cues. Reclaim those slots only at the budget.
            if (worldEffects.Count >= 128) TickWorldEffects();
            if (worldEffects.Count >= 128) return false;
            var go=new GameObject("Combat World Effect");go.transform.SetParent(summonedObjectsRoot!=null?summonedObjectsRoot:transform,false);
            go.transform.position=(floorMap!=null && session.DungeonContext!=null?WorldPosition(session,position):GridToWorldPosition(position))+(Vector3)offset;go.transform.localScale=Vector3.one;
            var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sortingOrder=sortingOrder;renderer.color=Color.white;
            var starts = Time.time + Mathf.Max(0, delaySeconds);
            renderer.forceRenderingOff = Time.time < starts;
            floorMap?.RegisterGroundLightingPresentation(renderer);
            worldEffects.Add(new WorldEffect { Renderer=renderer,Session=session,Position=position,Starts=starts,Ends=starts+Mathf.Max(.01f,duration) });
            return true;
        }

        private void TickWorldEffects()
        {
            for(var i=worldEffects.Count-1;i>=0;i--)
            {
                var effect=worldEffects[i];
                if(effect.Renderer==null || Time.time>=effect.Ends)
                {
                    if(effect.Renderer!=null){effect.Renderer.forceRenderingOff=true;effect.Renderer.enabled=false;floorMap?.UnregisterLightingPresentation(effect.Renderer);DestroyPresentationObject(effect.Renderer.gameObject);}
                    worldEffects.RemoveAt(i);
                }
                else
                {
                    effect.Renderer.forceRenderingOff=Time.time < effect.Starts || !IsPositionVisible(effect.Session,effect.Position)
                        || StatusEffectSystem.IsInvisible(effect.Target);
                }
            }
        }

        private void ClearWorldEffects()
        {
            foreach(var effect in worldEffects)
                if(effect.Renderer!=null){effect.Renderer.forceRenderingOff=true;effect.Renderer.enabled=false;floorMap?.UnregisterLightingPresentation(effect.Renderer);DestroyPresentationObject(effect.Renderer.gameObject);}
            worldEffects.Clear();
            ClearPersistentAreaVfx();
        }

        private void OnDisable()=>ClearWorldEffects();
    }
}
