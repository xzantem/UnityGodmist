using System.Collections.Generic;
using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public partial class DungeonFloorMapView
    {
        private readonly Dictionary<Sprite, (Vector2[] Vertices, ushort[] Triangles, Vector2[][] Outlines)> forestCanopyShapes = new();
        private readonly HashSet<Vector2Int> forestPassageCells = new();
        private readonly HashSet<SpriteRenderer> forestPassageCanopies = new();
        private DungeonFloorMap forestPassageMap;
        private int forestPassageTopologyRevision = -1;

        public void CollectVisibleForestForegroundCanopies(List<SpriteRenderer> result)
        {
            result.Clear(); forestPassageCanopies.Clear();
            if (!UsesVegetationPresentation || FogFloor == null || boundMap == null) return;
            // The bottom edge is local to each clearing/alcove, not the global
            // minimum Y of the map. A boundary tree south of visible floor can
            // project its crown over a wide room as well as a narrow passage.
            foreach (var pair in forestCanopyCells)
            {
                var position = new DungeonSurfacePosition(pair.Key);
                if (Knowledge(new DungeonTraversalPosition(position)) != DungeonKnowledge.Visible
                    || IsSurfaceOccluded(position)) continue;
                var point = CellToWorldCenter(pair.Key);
                var next = CellToWorldCenter(pair.Key + Vector2Int.one);
                var ground = new Bounds(point, new Vector3(Mathf.Abs(next.x-point.x)*.98f,
                    Mathf.Abs(next.y-point.y)*.98f, 1));
                if (!IsInPresentationCamera(ground)) continue;
                foreach (var canopy in pair.Value)
                    if (CanopyVisible(canopy) && forestTreeFootprints.ContainsKey(canopy)
                        && forestCanopyGroundBounds.TryGetValue(canopy, out var roots)
                        && ground.max.y > roots.yMax && ForestCanopyOverlapsBody(canopy, ground)
                        && forestPassageCanopies.Add(canopy)) result.Add(canopy);
            }
        }

        public void CollectVisibleForestPassageCanopies(List<SpriteRenderer> result)
        {
            result.Clear(); forestPassageCanopies.Clear();
            if (!UsesVegetationPresentation || FogFloor == null || boundMap == null) return;
            if (!ReferenceEquals(forestPassageMap, boundMap) || forestPassageTopologyRevision != boundMap.TraversalRevision)
            {
                forestPassageMap = boundMap; forestPassageTopologyRevision = boundMap.TraversalRevision;
                forestPassageCells.Clear();
                foreach (var cell in boundMap.FloorCells)
                    if (NarrowAlong(cell, Vector2Int.up, Vector2Int.right)
                        || NarrowAlong(cell, Vector2Int.right, Vector2Int.up)) forestPassageCells.Add(cell);
            }
            // Logical LOS gates this separately from the decorative woodland
            // fringe. A remembered or unknown passage cannot reveal a route.
            foreach (var cell in forestPassageCells)
            {
                if (Knowledge(new DungeonTraversalPosition(new DungeonSurfacePosition(cell))) != DungeonKnowledge.Visible
                    || IsSurfaceOccluded(new DungeonSurfacePosition(cell))) continue;
                var point = CellToWorldCenter(cell);
                var next = CellToWorldCenter(cell + Vector2Int.one);
                var ground = new Bounds(point, new Vector3(Mathf.Abs(next.x-point.x)*.98f,
                    Mathf.Abs(next.y-point.y)*.98f, 1));
                if (!IsInPresentationCamera(ground)
                    || !forestCanopyCells.TryGetValue(cell, out var canopies)) continue;
                foreach (var canopy in canopies)
                    if (CanopyVisible(canopy) && ForestCanopyOverlapsBody(canopy, ground)
                        && forestPassageCanopies.Add(canopy)) result.Add(canopy);
            }

            bool NarrowAlong(Vector2Int cell, Vector2Int along, Vector2Int across)
            {
                if (!boundMap.CanMoveBetween(cell, cell + along, true)
                    || !boundMap.CanMoveBetween(cell, cell - along, true)) return false;
                int width = 1;
                for (int sign = -1; sign <= 1; sign += 2)
                    for (int step = 1; step <= 3; step++)
                    {
                        var next = cell + across * (sign * step);
                        if (!boundMap.CanMoveBetween(next - across * sign, next, true)) break;
                        if (++width > 3) return false;
                    }
                return true;
            }
        }

        private void CacheForestCanopyShape(Sprite sprite)
        {
            if (forestCanopyShapes.ContainsKey(sprite)) return;
            // Production art deliberately renders as FullRect. Its imported
            // physics outlines describe the silhouette, unlike that rectangle.
            // Read once per species; no texture readback or collider is needed.
            var outlines = new Vector2[sprite.GetPhysicsShapeCount()][];
            var points = new List<Vector2>();
            for (int i = 0; i < outlines.Length; i++)
            {
                points.Clear(); sprite.GetPhysicsShape(i, points); outlines[i] = points.ToArray();
            }
            forestCanopyShapes.Add(sprite, (sprite.vertices, sprite.triangles, outlines));
        }

        public void CollectForestRevealCanopies(Vector2Int cell, Bounds body, float feetY, List<SpriteRenderer> result)
        {
            result.Clear();
            if (!UsesVegetationPresentation || !forestCanopyCells.TryGetValue(cell, out var canopies)) return;
            var order = GetActorDepthSortingOrder(feetY);
            foreach (var canopy in canopies)
                if (CanopyVisible(canopy) && ForestCanopyOrder(canopy) > order
                    && forestCanopyBaselines[canopy] <= feetY + .0001f
                    && ForestCanopyOverlapsBody(canopy, body)) result.Add(canopy);
        }

        public bool ForestCanopyContainsPoint(SpriteRenderer canopy, Vector2 world)
        {
            if (canopy==null || !forestCanopyGroundBounds.TryGetValue(canopy,out var roots)
                || roots.Contains(world) || !forestCanopyShapes.TryGetValue(canopy.sprite,out var mesh)) return false;
            var point=(Vector2)canopy.transform.InverseTransformPoint(world);
            if(canopy.flipX)point.x=-point.x;
            if(canopy.flipY)point.y=-point.y;
            if (mesh.Outlines.Length > 0)
            {
                foreach (var outline in mesh.Outlines) if (CanopyPolygonContains(outline, point)) return true;
                return false;
            }
            for(var i=0;i+2<mesh.Triangles.Length;i+=3)
            {
                var p=mesh.Vertices[mesh.Triangles[i]];var q=mesh.Vertices[mesh.Triangles[i+1]];var r=mesh.Vertices[mesh.Triangles[i+2]];
                if(Mathf.Abs(Cross(q-p,r-p))<.00000001f)continue;
                var a=Cross(q-p,point-p);var b=Cross(r-q,point-q);var c=Cross(p-r,point-r);
                if(a>=0 && b>=0 && c>=0 || a<=0 && b<=0 && c<=0)return true;
            }
            return false;
            static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        }

        private static bool CanopyPolygonContains(Vector2[] polygon, Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length-1; i < polygon.Length; j = i++)
            {
                var a = polygon[j]; var b = polygon[i]; var delta = b-a; var offset = point-a;
                float cross = delta.x*offset.y-delta.y*offset.x;
                float projection = Vector2.Dot(offset, delta);
                if (delta.sqrMagnitude > .00000001f && cross*cross <= .000000000001f*delta.sqrMagnitude
                    && projection >= 0 && projection <= delta.sqrMagnitude) return true;
                if ((a.y > point.y) != (b.y > point.y)
                    && point.x < (b.x-a.x)*(point.y-a.y)/(b.y-a.y)+a.x) inside = !inside;
            }
            return inside;
        }

        private static bool CanopyPolygonOverlaps(Vector2[] polygon, Vector2 lo, Vector2 hi)
        {
            if (CanopyPolygonContains(polygon, (lo+hi)*.5f)) return true;
            for (int i = 0, j = polygon.Length-1; i < polygon.Length; j = i++)
            {
                var a = polygon[j]; var b = polygon[i]; var delta = b-a;
                float enter = 0, exit = 1;
                if (Clip(a.x, delta.x, lo.x, hi.x, ref enter, ref exit)
                    && Clip(a.y, delta.y, lo.y, hi.y, ref enter, ref exit)) return true;
            }
            return false;

            static bool Clip(float start, float delta, float min, float max, ref float enter, ref float exit)
            {
                if (Mathf.Abs(delta) < .0000001f) return start >= min && start <= max;
                float a = (min-start)/delta, b = (max-start)/delta;
                enter = Mathf.Max(enter, Mathf.Min(a,b)); exit = Mathf.Min(exit, Mathf.Max(a,b));
                return enter <= exit;
            }
        }

        public bool ForestCanopyOverlapsBody(SpriteRenderer canopy, Bounds body)
        {
            if (canopy == null || !forestCanopyGroundBounds.TryGetValue(canopy, out var roots)
                || !canopy.bounds.Intersects(body)) return false;
            var min = Vector2.Max(body.min, canopy.bounds.min);
            var max = Vector2.Min(body.max, canopy.bounds.max);
            // Remove the protected root rectangle before testing the mesh.
            // Low leaves on either side count; opaque roots alone do not.
            return IntersectsCanopy(new Rect(min.x, Mathf.Max(min.y, roots.yMax), max.x-min.x, max.y-Mathf.Max(min.y, roots.yMax)))
                || IntersectsCanopy(new Rect(min.x, min.y, Mathf.Min(max.x, roots.xMin)-min.x, Mathf.Min(max.y, roots.yMax)-min.y))
                || IntersectsCanopy(new Rect(Mathf.Max(min.x, roots.xMax), min.y, max.x-Mathf.Max(min.x, roots.xMax), Mathf.Min(max.y, roots.yMax)-min.y));

            bool IntersectsCanopy(Rect world)
            {
                if (world.width <= 0 || world.height <= 0) return false;
                // Trees are static. The imported mesh and native transform
                // handle native scale, mirrored sprites and parent transforms.
                var a = (Vector2)canopy.transform.InverseTransformPoint(world.min);
                var b = (Vector2)canopy.transform.InverseTransformPoint(world.max);
                var c = (Vector2)canopy.transform.InverseTransformPoint(new Vector2(world.xMin, world.yMax));
                var d = (Vector2)canopy.transform.InverseTransformPoint(new Vector2(world.xMax, world.yMin));
                var lo = Vector2.Min(Vector2.Min(a,b), Vector2.Min(c,d));
                var hi = Vector2.Max(Vector2.Max(a,b), Vector2.Max(c,d));
                if (canopy.flipX) { var old=lo.x; lo.x=-hi.x; hi.x=-old; }
                if (canopy.flipY) { var old=lo.y; lo.y=-hi.y; hi.y=-old; }
                if (!forestCanopyShapes.TryGetValue(canopy.sprite, out var mesh)) return false;
                if (mesh.Outlines.Length > 0)
                {
                    foreach (var outline in mesh.Outlines) if (CanopyPolygonOverlaps(outline, lo, hi)) return true;
                    return false;
                }
                var centre = (lo+hi)*.5f; var half = (hi-lo)*.5f;
                for (var i=0; i+2<mesh.Triangles.Length; i+=3)
                {
                    var p=mesh.Vertices[mesh.Triangles[i]];var q=mesh.Vertices[mesh.Triangles[i+1]];var r=mesh.Vertices[mesh.Triangles[i+2]];
                    var triMin=Vector2.Min(p,Vector2.Min(q,r));var triMax=Vector2.Max(p,Vector2.Max(q,r));
                    if (triMax.x<=lo.x || triMin.x>=hi.x || triMax.y<=lo.y || triMin.y>=hi.y) continue;
                    if (!Separated(p,q,r,q-p) && !Separated(p,q,r,r-q) && !Separated(p,q,r,p-r)) return true;
                }
                return false;

                bool Separated(Vector2 p, Vector2 q, Vector2 r, Vector2 edge)
                {
                    var axis = new Vector2(-edge.y,edge.x);
                    var x=Vector2.Dot(p-centre,axis);var y=Vector2.Dot(q-centre,axis);var z=Vector2.Dot(r-centre,axis);
                    var extent=Mathf.Abs(axis.x)*half.x+Mathf.Abs(axis.y)*half.y;
                    return Mathf.Min(x,Mathf.Min(y,z))>extent || Mathf.Max(x,Mathf.Max(y,z)) < -extent;
                }
            }
        }
    }
}
