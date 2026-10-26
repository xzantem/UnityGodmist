using System.Collections.Generic;
using Godmist.Views.Exploration;
using UnityEngine;

namespace Godmist.Views.Lighting
{
    public sealed partial class DungeonSilhouetteLighting
    {
        // Art tuning: each crown keeps visible leaves at alpha 90/255.
        // Never reduce this further based on other overlapping trees.
        private const float RevealedCanopyAlpha = 90f/255f;
        private const float PassageCanopyAlpha = 160f/255f;
        private const float CanopyRevealSeconds = .2f, CanopyRestoreSeconds = .35f;
        private const float CanopyReleaseDelay = .1f;
        private const int MaxForestRevealActors = 32;
        private sealed class CanopyFade
        {
            public float Alpha=1, From=1, Target=1, Elapsed, Release;
        }
        private readonly Dictionary<SpriteRenderer, CanopyFade> canopyFades = new();
        private readonly Stack<CanopyFade> spareCanopyFades = new();
        private readonly Dictionary<SpriteRenderer, float> canopyFadeTargets = new();
        private readonly List<SpriteRenderer> canopyCandidates = new(), retiredCanopyFades = new();
        private MaterialPropertyBlock canopyFadeProperties;
        public int LastForestPartyRevealCount { get; private set; }
        public int LastForestCombatRevealCount { get; private set; }
        public int LastForestPassageRevealCount { get; private set; }
        public int LastForestForegroundRevealCount { get; private set; }

        private void RefreshForestPartyReveal()
        {
            canopyFadeTargets.Clear();
            int count=0;LastForestCombatRevealCount=LastForestPassageRevealCount=LastForestForegroundRevealCount=0;
            if (terrain != null && terrain.UsesVegetationPresentation && party != null)
            {
                terrain.CollectVisibleForestPassageCanopies(canopyCandidates);
                foreach (var canopy in canopyCandidates) canopyFadeTargets[canopy]=PassageCanopyAlpha;
                LastForestPassageRevealCount=canopyCandidates.Count;
                terrain.CollectVisibleForestForegroundCanopies(canopyCandidates);
                foreach (var canopy in canopyCandidates) canopyFadeTargets[canopy]=PassageCanopyAlpha;
                LastForestForegroundRevealCount=canopyCandidates.Count;
                // Actor overlap overrides the milder route target. Existing
                // timings, root protection and actor selection stay unchanged.
                foreach (var actor in party.ActiveMemberViews) AddActor(actor);
                if (encounters != null)
                    for (var i=0; i<encounters.EnemyViews.Count && count<MaxForestRevealActors; i++)
                    {
                        var actor=encounters.EnemyViews[i];
                        if (actor != null && actor.CombatUnit?.Character?.CurrentHealth > 0
                            && actor.IsVisibleForInspection && AddActor(actor)) LastForestCombatRevealCount++;
                    }
            }
            LastForestPartyRevealCount=count;

            bool AddActor(ExplorationCharacterView actor)
            {
                if (count>=MaxForestRevealActors || actor==null || !actor.gameObject.activeInHierarchy) return false;
                var sprite=actor.BodyRenderer;
                if (sprite==null || !sprite.enabled || sprite.forceRenderingOff || sprite.color.a<=.01f) return false;
                var body=sprite.bounds;
                if (!terrain.IsInPresentationCamera(body)) return false;
                var feet=actor.Position;
                var retained=new Bounds(new Vector3(feet.x,feet.y,body.center.z),
                    new Vector3(canopyCellHeight*.16f,canopyCellHeight*.16f,body.size.z));
                terrain.CollectForestRevealCanopies(terrain.WorldToCell(actor.Position),retained,actor.Position.y,canopyCandidates);
                // Enter when the actor's ground point is inside the actual
                // crown, regardless of distance from its trunk. The small box
                // only retains an existing fade at the edge; it cannot start one.
                for (var i=canopyCandidates.Count-1; i>=0; i--)
                {
                    var canopy=canopyCandidates[i];
                    // A mild passage fade is not a previous actor contact and
                    // must not grant the actor's small retention margin.
                    var alreadyFading=canopyFades.TryGetValue(canopy,out var state)
                        && state.Target<=RevealedCanopyAlpha+.0001f;
                    if (!terrain.ForestCanopyContainsPoint(canopy,feet)
                        && (!alreadyFading || !terrain.ForestCanopyOverlapsBody(canopy,retained)))
                        canopyCandidates.RemoveAt(i);
                }
                foreach (var canopy in canopyCandidates)
                    canopyFadeTargets[canopy]=RevealedCanopyAlpha;
                count++;return true;
            }
        }

        private void AdvanceForestCanopyFade(float deltaTime)
        {
            var delta=Mathf.Max(0,deltaTime);
            foreach (var target in canopyFadeTargets)
                if (!canopyFades.ContainsKey(target.Key))
                {
                    var state=spareCanopyFades.Count>0 ? spareCanopyFades.Pop() : new CanopyFade();
                    state.Alpha=state.From=state.Target=1;state.Elapsed=0;state.Release=CanopyReleaseDelay;
                    canopyFades.Add(target.Key,state);
                }
            retiredCanopyFades.Clear();
            foreach (var pair in canopyFades)
            {
                var tree=pair.Key;var state=pair.Value;
                if (tree==null) { retiredCanopyFades.Add(tree);continue; }
                var hidden=!tree.enabled || tree.forceRenderingOff || !tree.gameObject.activeInHierarchy;
                var target=1f;
                if (!hidden && canopyFadeTargets.TryGetValue(tree,out target)) state.Release=CanopyReleaseDelay;
                else
                {
                    state.Release-=delta;
                    target=!hidden && state.Release>0 ? state.Target : 1;
                }
                if (!Mathf.Approximately(target,state.Target))
                { state.From=state.Alpha;state.Target=target;state.Elapsed=0; }
                state.Elapsed+=delta;
                var duration=state.Target<state.From ? CanopyRevealSeconds : CanopyRestoreSeconds;
                state.Alpha=hidden ? 1 : Mathf.Lerp(state.From,state.Target,Mathf.SmoothStep(0,1,state.Elapsed/duration));
                SetCanopyFade(tree,1-state.Alpha);
                if (state.Target==1 && state.Alpha>=.9999f) retiredCanopyFades.Add(tree);
            }
            foreach (var tree in retiredCanopyFades)
            { var state=canopyFades[tree];canopyFades.Remove(tree);spareCanopyFades.Push(state); }
        }

        private void SetCanopyFade(SpriteRenderer tree,float fade)
        {
            canopyFadeProperties ??= new MaterialPropertyBlock();
            tree.GetPropertyBlock(canopyFadeProperties);
            if (SetReceiverFloat(canopyFadeProperties,CanopyFadeId,fade)) tree.SetPropertyBlock(canopyFadeProperties);
        }

        private void ResetForestCanopyFade()
        {
            foreach (var pair in canopyFades) if (pair.Key!=null) SetCanopyFade(pair.Key,0);
            canopyFades.Clear();spareCanopyFades.Clear();canopyFadeTargets.Clear();
            canopyCandidates.Clear();retiredCanopyFades.Clear();canopyFadeProperties=null;
            LastForestPartyRevealCount=LastForestCombatRevealCount=LastForestPassageRevealCount=LastForestForegroundRevealCount=0;
        }
    }
}
