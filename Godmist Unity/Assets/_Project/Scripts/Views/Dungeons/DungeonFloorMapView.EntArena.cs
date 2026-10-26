using System.Linq;
using Godmist.Data.Dungeons;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public partial class DungeonFloorMapView
    {
        private readonly System.Collections.Generic.Dictionary<Godmist.Core.Enums.DungeonRoomType, Vector2> forestTransitions = new();
        private readonly System.Collections.Generic.List<Rect> forestTransitionClearances = new();
        private readonly System.Collections.Generic.Dictionary<Godmist.Core.Enums.DungeonRoomType, Vector2> forestTransitionApproaches = new();

        // Reserve the exit before vegetation is built. This is presentation,
        // not a new route through the map or a hole in its movement boundary.
        public void PrepareForestTransitionBoundaries(Godmist.Runtime.Dungeons.DungeonFloorInstance floor)
        {
            forestTransitions.Clear(); forestTransitionApproaches.Clear(); forestTransitionClearances.Clear();
            if (vegetationPresentation == null || floor?.Map == null) return;
            foreach (var type in new[]{Godmist.Core.Enums.DungeonRoomType.Final, Godmist.Core.Enums.DungeonRoomType.Start})
            {
                if (type == Godmist.Core.Enums.DungeonRoomType.Start && floor.FloorIndex == 0) continue;
                var room = floor.Rooms.FirstOrDefault(r => r.RoomType == type);
                if (room == null) continue;
                // New generation guarantees an exterior edge. A historical
                // saved layout can be enclosed on both sides; retain its old
                // threshold rather than moving rooms/actors or failing load.
                if (!Godmist.Runtime.Dungeons.DungeonForestTransitionBoundary.TryResolve(floor, room.RoomId,
                        out var cell, out var normal)
                    && !Godmist.Runtime.Dungeons.DungeonForestTransitionBoundary.TryResolve(floor, room.RoomId,
                        out cell, out normal, requireExterior: false))
                    throw new System.InvalidOperationException("No usable forest boundary for " + room.RoomId);
                var width = Vector2.Distance(CellToWorldCenter(cell), CellToWorldCenter(cell + Vector2Int.right));
                var point = CellToWorldCenter(cell) + (Vector2)normal * width * .5f;
                forestTransitions[type] = point;
                forestTransitionApproaches[type] = CellToWorldCenter(cell);
                // Clear only the opening; leave the fence wings in the trees.
                forestTransitionClearances.Add(new Rect(point.x-.8f,point.y-.3f,1.6f,2.4f));
            }
        }

        public Vector2 ResolveForestTransitionPosition(Godmist.Runtime.Dungeons.DungeonFloorInstance floor,
            Vector2 fallback, Godmist.Core.Enums.DungeonRoomType type)
        {
            if(vegetationPresentation==null) return fallback;
            if(!forestTransitions.ContainsKey(type)) PrepareForestTransitionBoundaries(floor);
            return forestTransitionApproaches.TryGetValue(type,out var point)?point:fallback;
        }

        private bool IntersectsForestTransition(Sprite sprite, Vector2 position)
        {
            var bounds=sprite.bounds; var rect=new Rect(position+(Vector2)bounds.min,bounds.size);
            return forestTransitionClearances.Any(r=>r.Overlaps(rect));
        }

        public void StyleForestTransition(DungeonExitView transition)
        {
            if (vegetationPresentation == null || transition == null || transition.UsesStandingPresentation) return;
            var gate = DungeonForestObjectVisuals.Load("exit-gate-v2");
            // The open wicket replaces the stair marker; the interaction
            // trigger/action stay at the clear centre of its threshold.
            foreach (var renderer in transition.GetComponentsInChildren<SpriteRenderer>(true)) renderer.enabled = false;
            var go = new GameObject("Forest exit wicket gate");
            go.transform.SetParent(transition.transform, false); go.layer = transition.gameObject.layer;
            var body = go.AddComponent<SpriteRenderer>(); body.sprite = gate;
            if(forestTransitions.Count>0) go.transform.position=forestTransitions.Values.OrderBy(p=>((Vector2)transition.transform.position-p).sqrMagnitude).First();
            var group = go.AddComponent<UnityEngine.Rendering.SortingGroup>();
            transition.ConfigureStandingPresentation(this, group);
            transition.GetComponent<Godmist.Views.Exploration.ExplorationWorldObjectInteraction>()?.InvalidatePresentation();
        }

        // Static, fog-owned ground accents. No collider, navigation or shadow
        // caster is added; all artwork retains its authored 32 PPU and scale.
        private void BuildEntArenaDetails()
        {
            var presentationFloor = forestPresentationFloor ?? FogFloor;
            if (presentationFloor == null || boundMap == null || vegetationPresentation == null) return;
            foreach (var room in presentationFloor.Rooms.Where(r => r.TemplateId == "room.forest.ent_arena"
                || r.TemplateId == "room.forest.production.arena"))
            {
                var cells = boundMap.FloorCells.Where(c => !presentationFloor.IsConnectionCell(c) && boundMap.TryGetRoomId(c, out var id)
                    && id == room.RoomId).ToArray();
                if (cells.Length == 0) continue;
                var center = new Vector2(cells.Average(c => (float)c.x), cells.Average(c => (float)c.y));
                // Three root clusters leave the entrance and central lane clear.
                var patches = new[]{center + new Vector2(-5,2), center + new Vector2(5,-1), center + new Vector2(-4,-4)};
                var accents = cells.Where(c => !presentationOccupiedCells.Contains(c)
                    && !forestPathObstacles.Contains(c) && Mathf.Abs(c.x - center.x) > 1.5f)
                    .Where(c => patches.Any(p => ((Vector2)c-p).sqrMagnitude <= 2f))
                    .OrderBy(c => VegetationHash(c)).Take(20);
                foreach (var cell in accents)
                {
                    var noise = VegetationHash(cell);
                    var deadwood = (noise & 3u) == 0;
                    var sprite = DungeonVegetationPresentation.SelectDetailSprite(
                        deadwood ? vegetationPresentation.Deadwood : vegetationPresentation.ExposedRoots,
                        deadwood ? vegetationPresentation.DeadwoodVariants : vegetationPresentation.ExposedRootsVariants, noise);
                    if (sprite == null) continue;
                    var position = CellToWorldCenter(cell);
                    var detail = CreateDecoration(deadwood ? "Ent arena deadwood" : "Ent arena roots", sprite,
                        transform, position, GetFloorFeedbackDepthSortingOrder(),
                        new Color(.68f, .66f, .48f, 1), 1, FloorFeedbackSortingLayerID);
                    detail.Cell = cell; detail.Object.layer = gameObject.layer;
                    detail.Renderer.flipX = cell.x > center.x;
                    detail.Object.transform.position += (Vector3)(position - (Vector2)detail.Renderer.bounds.center);
                    forestPropContacts[detail.Renderer] = position;
                    presentationDecorations.Add(detail); floorSortedPresentations.Add(detail.Renderer);
                }
            }
        }
    }
}
