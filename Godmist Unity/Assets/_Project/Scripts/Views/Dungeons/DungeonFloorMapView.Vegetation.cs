using Godmist.Data.Dungeons;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using Godmist.Runtime.Dungeons;

namespace Godmist.Views.Dungeons
{
    public partial class DungeonFloorMapView
    {
        private readonly Dictionary<Vector2Int, List<SpriteRenderer>> forestCanopyCells = new();
        private readonly Dictionary<SpriteRenderer, UnityEngine.Rendering.SortingGroup> forestCanopyGroups = new();
        private readonly Dictionary<SpriteRenderer, float> forestCanopyBaselines = new();
        private readonly Dictionary<SpriteRenderer, Rect> forestCanopyGroundBounds = new();
        private readonly HashSet<SpriteRenderer> forestFrontRowCanopies = new();
        private readonly HashSet<SpriteRenderer> forestDividerCanopies = new();
        private readonly HashSet<SpriteRenderer> forestWideTrunks = new();
        private readonly HashSet<SpriteRenderer> forestLargeRoots = new();
        private readonly Dictionary<SpriteRenderer, RectInt> forestTreeFootprints = new();
        private readonly Dictionary<Renderer, DungeonSurfaceEdge[]> forestBoundaryOwners = new();
        // Geometry and its discovery composite must end at the same depth;
        // a shorter mask makes a deep woodland read as a single border row.
        private int ForestBoundaryDepthInCells => Mathf.Clamp(vegetationPresentation.BoundaryDepthInCells + 2, 4, 10);
        public int ForestCanopyRootHeightInCells(SpriteRenderer renderer) => forestLargeRoots.Contains(renderer) ? 2 : 1;
        public Rect ForestCanopyGroundBounds(SpriteRenderer renderer)
            => forestCanopyGroundBounds.TryGetValue(renderer, out var bounds) ? bounds : default;
        private int ForestCanopyOrder(SpriteRenderer renderer)
            => forestCanopyGroups.TryGetValue(renderer,out var group) && group != null && group.enabled
                ? group.sortingOrder : renderer.sortingOrder;
        private static bool CanopyVisible(SpriteRenderer r) => r != null && r.enabled && !r.forceRenderingOff
            && r.gameObject.activeInHierarchy && r.color.a > .01f;
        public bool TryGetForestCanopyOrder(Vector2Int cell, float feetY, out int order)
        {
            order = int.MinValue;
            if (vegetationPresentation == null || !forestCanopyCells.TryGetValue(cell,out var canopies)) return false;
            var actorOrder = GetActorDepthSortingOrder(feetY);
            foreach(var canopy in canopies) if(CanopyVisible(canopy)) order=Mathf.Max(order,ForestCanopyOrder(canopy));
            return order > actorOrder;
        }
        // Query only this cell's static index, then retain the nearest foreground
        // sprites. No scene scan, tree material changes or offscreen render pass.
        public int CollectForestCanopies(Vector2Int cell, Bounds body, float feetY, SpriteRenderer[] result)
        {
            if(vegetationPresentation == null || !forestCanopyCells.TryGetValue(cell,out var canopies)) return 0;
            var count=0;var actorOrder=GetActorDepthSortingOrder(feetY);
            foreach(var canopy in canopies)
            {
                if(!CanopyVisible(canopy) || !canopy.bounds.Intersects(body)) continue;
                var order=ForestCanopyOrder(canopy);if(order<=actorOrder) continue;
                var slot=0;while(slot<count && ForestCanopyOrder(result[slot])>=order) slot++;
                if(slot>=result.Length) continue;
                for(var i=Mathf.Min(count,result.Length-1);i>slot;i--) result[i]=result[i-1];
                result[slot]=canopy;count=Mathf.Min(count+1,result.Length);
            }
            return count;
        }
        public int ForestCanopyForegroundOrder(SpriteRenderer renderer) => ForestCanopyOrder(renderer);
        public bool IsForestCanopyRenderer(SpriteRenderer renderer) => renderer != null && forestCanopyGroups.ContainsKey(renderer);
        // Keep callers on their grounded lane, including behind opaque crowns.
        public int GetForestReadableSortingOrder(Vector2Int cell, Bounds body, int baseline, int clearance = 2)
            => baseline;
        public void RegisterForestCanopy(SpriteRenderer renderer, bool reserveBaseline = true)
        {
            if (vegetationPresentation == null || renderer?.sprite == null || renderer.bounds.size.y < .4f) return;
            if (vegetationPresentation.TreeVariants.Length > 5 && renderer.sprite == vegetationPresentation.TreeVariants[5])
                forestWideTrunks.Add(renderer);
            if ((forestTreeFootprints.TryGetValue(renderer, out var footprint) && footprint.size == new Vector2Int(2, 2))
                || renderer.GetComponentInParent<DungeonObstacleView>()?.OccupiedSizeInCells == new Vector2Int(2, 2))
                forestLargeRoots.Add(renderer);
            forestCanopyGroups[renderer]=renderer.GetComponentInParent<UnityEngine.Rendering.SortingGroup>();
            var feet = renderer.transform.position;
            forestCanopyBaselines[renderer] = feet.y;
            var cellSize = CellToWorldCenter(Vector2Int.one) - CellToWorldCenter(Vector2Int.zero);
            var size = ForestCanopyRootHeightInCells(renderer);
            var ground = new Rect(feet.x - cellSize.x * size * .5f, feet.y, cellSize.x * size, cellSize.y * size);
            if (forestTreeFootprints.TryGetValue(renderer, out var reserved))
            {
                var origin = CellToWorldCenter(reserved.min) - cellSize * .5f;
                ground = new Rect(origin.x, origin.y, reserved.width * cellSize.x, reserved.height * cellSize.y);
            }
            if (forestDividerCanopies.Contains(renderer) && forestBoundaryOwners.TryGetValue(renderer, out var dividerEdges))
            {
                var edge=dividerEdges[0];
                var a=CellToWorldCenter(edge.FirstCell);var b=CellToWorldCenter(edge.SecondCell);
                var middle=(a+b)*.5f;
                var rootSize=edge.FirstCell.x==edge.SecondCell.x
                    ? new Vector2(cellSize.x,.24f*cellSize.y) : new Vector2(.24f*cellSize.x,cellSize.y);
                ground=new Rect(middle-rootSize*.5f,rootSize);
            }
            forestCanopyGroundBounds[renderer] = ground;
            CacheForestCanopyShape(renderer.sprite);
            // Boundary roots now share exact ground rows: reserving every row
            // is bounded by map height, not one fractional baseline per tree.
            // Deep crowns must obey the same feet ordering as the front row.
            if ((reserveBaseline || forestTreeFootprints.ContainsKey(renderer))
                && RegisterDepthBaseline(standingObjectDepthSortBaselines, feet.y))
                DepthBaselineAdded();
            // A different tree may already have reserved this exact row.
            // New receivers still need their own grounded order immediately.
            if (forestCanopyGroups[renderer] == null)
                renderer.sortingOrder = ResolveDepthSortingOrder(feet.y, false) + 2;
            var bounds=renderer.bounds;
            var first=WorldToCell(bounds.min-new Vector3(2,4,0));var last=WorldToCell(bounds.max+Vector3.right*2);
            for(var y=first.y;y<=last.y;y++) for(var x=first.x;x<=last.x;x++)
            {
                var cell=new Vector2Int(x,y);if(!boundMap.IsWalkable(cell)) continue;
                if(!forestCanopyCells.TryGetValue(cell,out var list)) forestCanopyCells.Add(cell,list=new List<SpriteRenderer>());
                if(!list.Contains(renderer)) list.Add(renderer);
            }
        }
        private void RefreshForestCanopyDepthOrders()
        {
            foreach (var pair in forestCanopyBaselines)
                if (pair.Key != null && (!forestCanopyGroups.TryGetValue(pair.Key, out var group) || group == null))
                    pair.Key.sortingOrder = ResolveDepthSortingOrder(pair.Value, false) + 2;
        }
        private DungeonFloorInstance forestPresentationFloor;
        public void ConfigureForestPresentationFloor(DungeonFloorInstance floor) => forestPresentationFloor = floor;
        private DungeonVegetationPresentation vegetationPresentation;
        private readonly Dictionary<DungeonWallSegmentView, Renderer[]> vegetationRenderers = new();
        private readonly List<(SpriteRenderer Renderer, DungeonSurfaceEdge[] Edges)> vegetationGround = new();
        private readonly HashSet<SpriteRenderer> vegetationGroundRenderers = new();
        private readonly Dictionary<SpriteRenderer, Vector2> forestPropContacts = new();
        public IEnumerable<KeyValuePair<SpriteRenderer, Vector2>> ForestPropContacts => forestPropContacts;
        public bool IsForestReadableProp(SpriteRenderer renderer) => renderer != null && forestPropContacts.ContainsKey(renderer);
        private readonly List<(Collider2D Collider, int Level)> vegetationColliders = new();
        public bool PreservesGroundPresentationSorting(SpriteRenderer renderer) => vegetationGroundRenderers.Contains(renderer) || depthSortedGroundPresentations.Contains(renderer);
        public bool UsesVegetationPresentation => vegetationPresentation != null;

        public void ConfigureVegetationPresentation(DungeonVegetationPresentation presentation)
            => vegetationPresentation = presentation;

        // Build-time clearance for the tall boss silhouette on every legal arena cell.
        // Keep collision, fog ownership and fire registration at the original edge.
        public static Vector2 ResolveEntArenaTreeClearance(Bounds treeBounds, Vector2[] arenaCells,
            Vector2 arenaCenter, float cellWidth)
        {
            var direction = ((Vector2)treeBounds.center - arenaCenter).normalized;
            if (direction.sqrMagnitude < .01f) direction = Vector2.up;
            var offset = Vector2.zero;
            for (var step = 0; step < 40; step++)
            {
                var overlaps = false;
                foreach (var cell in arenaCells)
                {
                    // Feet can reach the cell edge; the Ent extends above them.
                    var body = new Bounds((Vector3)(cell + Vector2.up * (1.5f * cellWidth)),
                        new Vector3(4f * cellWidth, 4f * cellWidth, 2));
                    if (treeBounds.Intersects(body)) { overlaps = true; break; }
                }
                if (!overlaps) break;
                var delta = direction * (.5f * cellWidth);
                offset += delta; treeBounds.center += (Vector3)delta;
            }
            return offset;
        }

        private void BuildVegetationBoundary()
        {
            var previous = reserveDepthBaselines;
            reserveDepthBaselines = true;
            try { BuildVegetationBoundaryCore(); }
            finally
            {
                reserveDepthBaselines = previous;
                if (!reserveDepthBaselines && pendingDepthBaselines)
                { pendingDepthBaselines = false; ConfigureWallDepthSorting(); }
                RefreshForestCanopyDepthOrders();
            }
        }
        private void BuildVegetationBoundaryCore()
        {
            InvalidateForestLightingCoverage();
            if (vegetationPresentation == null || vegetationPresentation.BoundaryTree == null) return;
            forestCanopyCells.Clear(); forestCanopyGroups.Clear(); forestCanopyBaselines.Clear(); forestFrontRowCanopies.Clear();
            forestCanopyGroundBounds.Clear();
            forestDividerCanopies.Clear();
            forestCanopyShapes.Clear();
            forestWideTrunks.Clear(); forestLargeRoots.Clear(); forestTreeFootprints.Clear(); forestBoundaryOwners.Clear();
            var owners = new Dictionary<Vector2Int, (int Index, int Depth)>();
            var pending = new Queue<Vector2Int>();
            var renderers = new List<Renderer>[wallRenderers.Count];
            var directions = new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
            // Keep collision at the traversable boundary; the forest extends
            // into void cells and shares visibility with its nearest boundary edge.
            for (var i = 0; i < wallRenderers.Count; i++)
            {
                var pair = wallRenderers[i];
                if (pair.View == null) continue;
                renderers[i] = new List<Renderer>();
                foreach (var renderer in pair.View.GetComponentsInChildren<Renderer>(true))
                { renderer.forceRenderingOff = true; renderer.enabled = false; }
                // Their native masonry art is disabled; forest sprites are
                // separate siblings and never use these empty sorting groups.
                foreach (var group in pair.View.GetComponentsInChildren<UnityEngine.Rendering.SortingGroup>(true)) group.enabled = false;
                foreach (var collider in pair.View.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
                if (boundMap.GetWallType(pair.Edge) == Godmist.Core.Enums.DungeonWallType.Low)
                {
                    BuildForestLowHedge(pair.Edge,renderers[i]);
                    continue; // Crossable edge, never a blocking tree/collider.
                }
                if (boundMap.GetWallType(pair.Edge) == Godmist.Core.Enums.DungeonWallType.TreeTrunk
                    && boundMap.IsWalkable(pair.Edge.First) && boundMap.IsWalkable(pair.Edge.Second))
                {
                    // Legacy layouts contain these passable interior markers.
                    // Keep their save representation, but create neither tree
                    // art nor a physical obstacle on the walkable clearing.
                    continue;
                }
                var boundary = new GameObject("Vegetation movement boundary");
                boundary.layer = pair.View.gameObject.layer;
                boundary.transform.SetParent(wallsRoot != null ? wallsRoot : transform, false);
                var first = CellToWorldCenter(pair.Edge.FirstCell);
                var second = CellToWorldCenter(pair.Edge.SecondCell);
                boundary.transform.position = (first + second) * .5f;
                var box = boundary.AddComponent<BoxCollider2D>();
                var length = Vector2.Distance(first, second);
                box.size = pair.Edge.FirstCell.x == pair.Edge.SecondCell.x
                    ? new Vector2(Mathf.Max(.02f, length - .04f), .02f)
                    : new Vector2(.02f, Mathf.Max(.02f, length - .04f));
                box.edgeRadius = .02f;
                vegetationColliders.Add((box, pair.Edge.Level));
                if (boundMap.SurfaceClosedProgressionGateEdges.Contains(pair.Edge)) forestGateMovementBoundaries[pair.Edge]=box;
                spawnedWalls.Add(boundary);
                // A thin solid edge may have playable floor on both sides.
                // There is no void cell to seed below, but hiding the masonry
                // must not leave its collider looking like an open route.
                if (boundMap.IsWalkable(pair.Edge.First) && boundMap.IsWalkable(pair.Edge.Second)
                    && !boundMap.SurfaceClosedProgressionGateEdges.Contains(pair.Edge))
                    BuildForestDividerTree(pair.Edge,renderers[i]);
                // Nearest trunks stay at the real blocking edge; low shrubs are interior decoration only.
                foreach (var cell in new[] { pair.Edge.FirstCell, pair.Edge.SecondCell })
                    if (!boundMap.IsWalkable(cell) && owners.TryAdd(cell, (i, 1))) pending.Enqueue(cell);
            }
            var depthLimit = ForestBoundaryDepthInCells;
            while (pending.Count > 0)
            {
                var cell = pending.Dequeue();
                var owner = owners[cell];
                if (owner.Depth >= depthLimit) continue;
                foreach (var direction in directions)
                {
                    var next = cell + direction;
                    if (!boundMap.IsWalkable(next) && owners.TryAdd(next, (owner.Index, owner.Depth + 1))) pending.Enqueue(next);
                }
            }
            var occupiedTrunks = new HashSet<Vector2Int>();
            var ancientTrunks = new List<Vector2Int>();
            var treeSpacing = new Dictionary<Vector2Int, List<(Vector2 Position, float Radius)>>();
            var hasAncient = vegetationPresentation.TreeVariants.Length > 5 && vegetationPresentation.TreeVariants[5] != null;
            // Large footprints claim their ground first. Only afterwards do
            // compact 1x1 trees fill remaining gaps, including the blocking edge.
            foreach (var compact in new[] { false, true })
            foreach (var item in owners.OrderBy(p => hasAncient && VegetationHash(p.Key) % 72u == 11u ? 0 : 1)
                .ThenBy(p => p.Value.Depth).ThenBy(p => VegetationHash(p.Key)).ThenBy(p => p.Key.y).ThenBy(p => p.Key.x))
            {
                var cell = item.Key;
                if (occupiedTrunks.Contains(cell)) continue;
                var owner = item.Value;
                // Fill free slots throughout the visible woodland depth.
                // Mature crowns alone leave open soil behind the first row,
                // especially south of a clearing where crowns project north.
                var center = CellToWorldCenter(cell);
                var cellWidth = Vector2.Distance(CellToWorldCenter(cell + Vector2Int.right), center);
                var noise = VegetationHash(cell);
                var sprite = DungeonVegetationPresentation.SelectTreeSprite(vegetationPresentation.BoundaryTree, vegetationPresentation.TreeVariants, noise);
                if (compact)
                {
                    var small = vegetationPresentation.SmallTreeVariants;
                    if (small == null || small.Length == 0) continue;
                    sprite = small[noise % (uint)small.Length];
                    if (sprite == null) continue;
                }
                var ancient = vegetationPresentation.TreeVariants.Length > 5 && sprite == vegetationPresentation.TreeVariants[5];
                var boundaryEdge = wallRenderers[owner.Index].Edge;
                var outwardStep = boundMap.IsWalkable(boundaryEdge.FirstCell)
                    ? boundaryEdge.SecondCell - boundaryEdge.FirstCell : boundaryEdge.FirstCell - boundaryEdge.SecondCell;
                // Reserve away from the path on all four orientations, rather
                // than always trying to take already occupied north/east cells.
                var largeAnchor = cell + new Vector2Int(outwardStep.x < 0 ? -1 : 0, outwardStep.y < 0 ? -1 : 0);
                var largeOffset = new Vector2(largeAnchor.x - cell.x + .5f, largeAnchor.y - cell.y + .5f) * cellWidth;
                var footprint = new[] { largeAnchor, largeAnchor + Vector2Int.right, largeAnchor + Vector2Int.up, largeAnchor + Vector2Int.one };
                if (ancient && ancientTrunks.Any(c => (c-largeAnchor).sqrMagnitude < 36))
                { ancient = false; sprite = vegetationPresentation.BoundaryTree; }
                // Candidate priority, spacing and species use different bits:
                // accepting the first available ground slot must not also
                // select the same species along an entire edge.
                if (!compact && !ancient && vegetationPresentation.TreeVariants.Length >= 3)
                {
                    var species = VegetationHash(cell + new Vector2Int(193, 71)) % 4u;
                    sprite = species < 3 ? vegetationPresentation.TreeVariants[species] : vegetationPresentation.BoundaryTree;
                    if (sprite == null) sprite = vegetationPresentation.BoundaryTree;
                }
                var large = !compact && sprite.bounds.size.x >= cellWidth * 3;
                if (large && footprint.Any(c => !owners.ContainsKey(c) || boundMap.IsWalkable(c) || occupiedTrunks.Contains(c)))
                    continue;
                // Deep staggered rows close crown gaps without scaling the art
                // or moving the nearest trunks/collision off the real border.
                var jitter = ancient ? 0 : compact ? .12f : .24f;
                center.x += ((noise & 255u) / 255f - .5f) * jitter * cellWidth;
                // The bottom-pivot art projects above its occupied ground
                // footprint. Correct the drawing without moving the logical boundary.
                var offset = vegetationPresentation.TreeVisualOffsetInCells;
                center = center
                    + (CellToWorldCenter(cell + Vector2Int.right) - CellToWorldCenter(cell)) * offset.x
                    + (CellToWorldCenter(cell + Vector2Int.up) - CellToWorldCenter(cell)) * offset.y;
                if (owner.Depth == 1 && !large)
                {
                    var edge = wallRenderers[owner.Index].Edge;
                    var a = CellToWorldCenter(edge.FirstCell);
                    var b = CellToWorldCenter(edge.SecondCell);
                    var outward = (CellToWorldCenter(cell) - (a + b) * .5f).normalized;
                    var tangent = new Vector2(-outward.y, outward.x);
                    // Keep the compact roots inside their own 1x1 cell.
                    // Planting at the wall edge pushed them into walkable soil.
                    center = (a + b) * .5f + outward * (.5f * cellWidth)
                        + tangent * (((noise & 255u) / 255f - .5f) * .12f * cellWidth);
                }
                if (large) center += largeOffset;
                // The sprite pivot is the visible ground contact, not the
                // centre of its occupied cell(s). Match interior 2x2 trees.
                // Three small, stable offsets break planted rows while keeping
                // all ground contacts inside the reserved cell(s). This still
                // bounds sorting lanes to three positions per ground row.
                center.y = CellToWorldCenter(large ? largeAnchor : cell).y - cellWidth * .5f
                    + (ancient ? 0 : ((noise >> 24) % 3u) * cellWidth / 16f);
                // Test the projected crown, not just its grounded trunk. Large
                // art belongs deeper in the woods and must leave cell centres
                // readable even before the party approaches and reveals it.
                if (large && ForestCrownCoversTrailCore(sprite, center, cellWidth)) continue;
                if (IntersectsForestTransition(sprite, center)) continue;
                // Mature crowns span several cells: one tree per void cell
                // makes an opaque repeated hedge. Reserve room around each
                // accepted trunk without scaling the art.
                var spacingRadius = cellWidth * (ancient ? 1.8f : large
                    ? Mathf.Clamp(sprite.bounds.size.x / cellWidth * .25f, 1.15f, 1.5f) : .5f);
                if (large && !ancient)
                    spacingRadius *= Mathf.Lerp(1f, 1.3f, ((noise >> 8) & 255u) / 255f);
                var bucket = Vector2Int.FloorToInt(center / cellWidth);
                var crowded = false;
                for (var y = -4; y <= 4 && !crowded; y++)
                    for (var x = -4; x <= 4 && !crowded; x++)
                        if (treeSpacing.TryGetValue(bucket + new Vector2Int(x, y), out var neighbours))
                            foreach (var neighbour in neighbours)
                            {
                                var clearance = neighbour.Radius + spacingRadius;
                                if ((neighbour.Position - center).sqrMagnitude < clearance * clearance)
                                { crowded = true; break; }
                            }
                // Compact trunks fit inside their own free cell. Applying
                // the large-crown clearance again would leave the same holes.
                if (crowded && (!compact || owner.Depth > 1)) continue;
                if (!treeSpacing.TryGetValue(bucket, out var spaced)) treeSpacing.Add(bucket, spaced = new List<(Vector2, float)>());
                spaced.Add((center, spacingRadius));
                occupiedTrunks.Add(cell);
                if (ancient)
                    ancientTrunks.Add(largeAnchor);
                // Every large variant reserves the complete 2x2 ground block,
                // including ordinary mature trees, not only ancient trunks.
                if (large)
                    foreach (var part in footprint) occupiedTrunks.Add(part);
                var tree = new GameObject("Boundary vegetation");
                tree.transform.SetParent(wallsRoot != null ? wallsRoot : transform, false);
                tree.transform.position = center;
                var rendererTree = tree.AddComponent<SpriteRenderer>();
                rendererTree.sprite = sprite;
                forestTreeFootprints[rendererTree] = new RectInt(large ? largeAnchor : cell, Vector2Int.one * (large ? 2 : 1));
                // Smooth extinction keeps the nearest layers readable, then
                // closes the forest without a uniformly dark hedge.
                var depth = (owner.Depth - 1f) / depthLimit;
                // A tall foreground tree can have its roots far away while
                // its crown reaches the forest edge. Fade by the visible edge,
                // so keeping the new trees out of the path does not erase them.
                var edgeCentre = (CellToWorldCenter(boundaryEdge.FirstCell) + CellToWorldCenter(boundaryEdge.SecondCell)) * .5f;
                var visualDepth = Vector2.Distance(edgeCentre, rendererTree.bounds.ClosestPoint(edgeCentre)) / cellWidth;
                depth = Mathf.Min(depth, visualDepth / depthLimit);
                // The shared discovery composite already fades this scenery
                // into black. Do not erase deep trees a second time while the
                // continuous litter backdrop is still visible beneath them.
                var fade = Mathf.Lerp(.65f, 1f, 1f - Mathf.SmoothStep(0, 1, depth));
                var tint = vegetationPresentation.TreeTint;
                var warmth = ((noise >> 16) & 255u) / 255f;
                rendererTree.color = new Color(tint.r * fade * Mathf.Lerp(.94f, 1.08f, warmth),
                    tint.g * fade * Mathf.Lerp(1.03f, .96f, warmth), tint.b * fade * Mathf.Lerp(.98f, .92f, warmth), tint.a);
                rendererTree.flipX = (noise & 1u) != 0;
                rendererTree.sortingLayerID = FloorFeedbackSortingLayerID;
                rendererTree.sortingOrder = GetWallMountedDepthSortingOrder(center.y);
                if (owner.Depth == 1) forestFrontRowCanopies.Add(rendererTree);
                tree.transform.localScale = Vector3.one;
                spawnedWalls.Add(tree);
                renderers[owner.Index].Add(rendererTree);
                if (owner.Depth <= 2) entFireTrees.Add((rendererTree, wallRenderers[owner.Index].Edge));
                // Adjacent underlay tiles share boundary knowledge. A single
                // unseen owner must not punch a black square beside a visible tree.
                var groundEdges = new HashSet<DungeonSurfaceEdge> { wallRenderers[owner.Index].Edge };
                for (var y = -1; y <= 1; y++)
                    for (var x = -1; x <= 1; x++)
                        if (owners.TryGetValue(cell + new Vector2Int(x, y), out var nearby))
                            groundEdges.Add(wallRenderers[nearby.Index].Edge);
                var sharedEdges = groundEdges.ToArray();
                forestBoundaryOwners[rendererTree] = sharedEdges;
            }
            // Soil is continuous independently of which tree candidates fit.
            // A sparse crown layout must not expose square Tilemap edges.
            // Shared patches replace per-tree underlays, reducing renderers.
            foreach (var patch in owners.GroupBy(p => new Vector2Int(
                Mathf.FloorToInt(p.Key.x / 4f), Mathf.FloorToInt(p.Key.y / 4f))))
            {
                var cells = patch.ToArray();
                var first = cells[0].Key;
                var width = Vector2.Distance(CellToWorldCenter(first), CellToWorldCenter(first + Vector2Int.right));
                var bounds = new Bounds(CellToWorldCenter(first), Vector3.one * width);
                var edges = new HashSet<DungeonSurfaceEdge>();
                var minDepth = depthLimit;
                foreach (var item in cells)
                {
                    bounds.Encapsulate(new Bounds(CellToWorldCenter(item.Key), Vector3.one * width));
                    minDepth = Mathf.Min(minDepth, item.Value.Depth);
                    for (var y = -1; y <= 1; y++) for (var x = -1; x <= 1; x++)
                        if (owners.TryGetValue(item.Key + new Vector2Int(x, y), out var nearby))
                            edges.Add(wallRenderers[nearby.Index].Edge);
                }
                var fade = Mathf.Pow(1f - Mathf.SmoothStep(0, 1, (minDepth - 1f) / depthLimit), 1.6f);
                BuildVegetationGround(first, edges.ToArray(), fade, width, bounds);
            }
            BuildForestProgressionGates(renderers);
            BuildForestExitRails(renderers);
            for (var i = 0; i < wallRenderers.Count; i++)
                if (wallRenderers[i].View != null)
                {
                    var pair = wallRenderers[i];
                    var trees = renderers[i]?.ToArray() ?? System.Array.Empty<Renderer>();
                    foreach(var sprite in trees.OfType<SpriteRenderer>())
                        if(!IsForestProgressionGateRenderer(sprite) && !forestExitRails.Contains(sprite) && !forestLowHedgeBaselines.ContainsKey(sprite))RegisterForestCanopy(sprite, forestFrontRowCanopies.Contains(sprite));
                    vegetationRenderers[pair.View] = trees;
                    wallRenderers[i] = (pair.Edge, pair.View, trees);
                    InvalidateWorldPresentation();
                }
        }

        private void BuildForestDividerTree(DungeonSurfaceEdge edge,List<Renderer> renderers)
        {
            var variants=vegetationPresentation.SmallTreeVariants;
            if(variants==null || variants.Length==0)return;
            var noise=VegetationHash(edge.FirstCell+edge.SecondCell);
            var sprite=variants[noise%(uint)variants.Length];
            if(sprite==null)sprite=variants.FirstOrDefault(s=>s!=null);
            if(sprite==null)return;
            var root=new GameObject("Forest divider tree");
            root.transform.SetParent(wallsRoot!=null?wallsRoot:transform,false);
            root.transform.position=(CellToWorldCenter(edge.FirstCell)+CellToWorldCenter(edge.SecondCell))*.5f;
            var tree=root.AddComponent<SpriteRenderer>();
            tree.sprite=sprite;tree.color=vegetationPresentation.TreeTint;tree.flipX=(noise&1u)!=0;
            tree.sortingLayerID=FloorFeedbackSortingLayerID;
            tree.sortingOrder=GetWallMountedDepthSortingOrder(root.transform.position.y);
            forestDividerCanopies.Add(tree);forestFrontRowCanopies.Add(tree);
            forestBoundaryOwners[tree]=new[]{edge};renderers.Add(tree);spawnedWalls.Add(root);
        }

        private bool ForestCrownCoversTrailCore(Sprite sprite, Vector2 feet, float width)
        {
            var crown = sprite.bounds; crown.center += (Vector3)feet;
            var first = WorldToCell(crown.min); var last = WorldToCell(crown.max);
            for (var y = first.y; y <= last.y; y++)
                for (var x = first.x; x <= last.x; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (boundMap.IsWalkable(cell) && crown.Intersects(new Bounds(CellToWorldCenter(cell),
                        new Vector3(width * .6f, width * .6f, 1)))) return true;
                }
            return false;
        }

        private void BuildBoundaryScrub(DungeonSurfaceEdge edge, List<Renderer> renderers, float width)
        {
            var noise = VegetationHash(edge.FirstCell + edge.SecondCell*31);
            var sprite = DungeonVegetationPresentation.SelectDetailSprite(vegetationPresentation.LowShrub,
                vegetationPresentation.LowShrubVariants, noise);
            if (noise%5u is 1u or 2u && vegetationPresentation.DryShrub != null)
                sprite = vegetationPresentation.DryShrub;
            else if (noise%5u >= 3u && vegetationPresentation.WoodlandFern != null)
                sprite = DungeonVegetationPresentation.SelectDetailSprite(vegetationPresentation.WoodlandFern,
                    vegetationPresentation.WoodlandFernVariants, noise);
            if (sprite == null) return;
            var a = CellToWorldCenter(edge.FirstCell); var b = CellToWorldCenter(edge.SecondCell);
            var normal = (b-a).normalized; var tangent = new Vector2(-normal.y, normal.x);
            var extent = Mathf.Abs(tangent.x)*sprite.bounds.size.x + Mathf.Abs(tangent.y)*sprite.bounds.size.y;
            var count = Mathf.Clamp(Mathf.CeilToInt(width / Mathf.Max(.1f, extent*.75f)), 1, 5);
            for (var i = 0; i < count; i++)
            {
                var detailNoise = VegetationHash(edge.FirstCell + edge.SecondCell*31 + Vector2Int.right*i);
                var outward = boundMap.IsWalkable(edge.FirstCell) ? normal : -normal;
                var position = (a+b)*.5f + tangent*((i+.5f)/count-.5f)*width
                    + outward*((.05f + (detailNoise&255u)/255f*.13f)*width)
                    + tangent*((((detailNoise>>8)&255u)/255f-.5f)*.12f*width);
                var scrub = new GameObject("Forest boundary scrub");
                scrub.transform.SetParent(wallsRoot != null ? wallsRoot : transform, false);
                var sr = scrub.AddComponent<SpriteRenderer>(); sr.sprite = sprite;
                scrub.transform.position = (Vector3)position - new Vector3(sprite.bounds.center.x, sprite.bounds.min.y, 0);
                sr.color = Color.Lerp(new Color(.57f,.65f,.45f,1),new Color(.80f,.76f,.59f,1),
                    ((detailNoise>>16)&255u)/255f);
                sr.flipX = (VegetationHash(edge.FirstCell + Vector2Int.right*i)&1u)!=0;
                sr.sortingLayerID = FloorFeedbackSortingLayerID;
                sr.sortingOrder = GetWallMountedDepthSortingOrder(position.y);
                forestPropContacts[sr] = position;
                spawnedWalls.Add(scrub); renderers.Add(sr);
            }
        }

        private void BuildVegetationGround(Vector2Int cell, DungeonSurfaceEdge[] edges, float fade, float width, Bounds groundBounds)
        {
            if (vegetationPresentation.GroundTile is not Tile tile || tile.sprite == null) return;
            var ground = new GameObject("Forest floor under vegetation");
            ground.transform.SetParent(wallsRoot != null ? wallsRoot : transform, false);
            // Overlapping opaque cores bridge Tilemap edges. These static
            // patches depend on boundary geometry, not accepted tree slots.
            var coverage = new Bounds(CellToWorldCenter(cell), Vector3.one * (width*2.4f));
            coverage.Encapsulate(groundBounds.min - Vector3.one*(width*2f));
            coverage.Encapsulate(groundBounds.max + Vector3.one*(width*2f));
            ground.transform.position = coverage.center;
            var renderer = ground.AddComponent<SpriteRenderer>();
            renderer.sprite = tile.sprite;
            var tint = vegetationPresentation.GroundTint;
            renderer.color = new Color(tint.r * fade, tint.g * fade, tint.b * fade, tint.a);
            renderer.sortingLayerID = FloorFeedbackSortingLayerID;
            renderer.sortingOrder = ForestUnderlaySortingOrder(vegetationGround.Count - (forestBackdrop != null ? 1 : 0));
            // Overlap prevents black seams at stepped tile boundaries. This
            // ground is deliberately absent from floor/traversal surfaces.
            ground.transform.localScale = new Vector3(coverage.size.x / tile.sprite.bounds.size.x,
                coverage.size.y / tile.sprite.bounds.size.y, 1);
            // The reused ground sprite is bottom-pivoted. Position its visible
            // bounds at the cell centre, not its import pivot.
            ground.transform.position += coverage.center - renderer.bounds.center;
            var properties = new MaterialPropertyBlock();
            properties.SetVector("_ForestPatchBounds", new Vector4(renderer.bounds.center.x,
                renderer.bounds.center.y, coverage.extents.x, coverage.extents.y));
            renderer.SetPropertyBlock(properties);
            vegetationGroundRenderers.Add(renderer);
            RegisterGroundLightingPresentation(renderer);
            vegetationGround.Add((renderer, edges));
            if (forestBackdrop != null)
                forestBackdrop.sortingOrder = ForestUnderlaySortingOrder(vegetationGround.Count - 1);
            var groundIndex = vegetationGround.Count - 1;
            foreach (var edge in edges)
            {
                if (!vegetationGroundByEdge.TryGetValue(edge, out var indices))
                    vegetationGroundByEdge.Add(edge, indices = new List<int>());
                indices.Add(groundIndex);
            }
            spawnedWalls.Add(ground);
        }

        private void BuildForestGroundCover()
        {
            var sprite = vegetationPresentation.GroundCover;
            if (sprite == null) return;
            foreach (var cell in boundMap.FloorCells.OrderBy(c => c.y).ThenBy(c => c.x))
            {
                var noise = VegetationHash(cell);
                if (noise % 7u != 0 || presentationOccupiedCells.Contains(cell)
                    || forestVisualPathCells.Contains(cell)) continue;
                // Low-frequency clusters leave open mud between patches.
                // Continuous clustering avoids visible four-cell patch borders.
                var cluster = Mathf.PerlinNoise(cell.x * .17f + 73.1f, cell.y * .17f + 19.7f);
                var center = CellToWorldCenter(cell);
                // Share the broad moisture field with the soil and larger
                // undergrowth. Dry patches expose more litter; wet patches
                // keep the existing maximum density, never adding slots.
                var moisture = Mathf.PerlinNoise(center.x * .24f + 41.7f, center.y * .24f + 73.2f);
                var wetness = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.35f, .65f, moisture));
                if (cluster < Mathf.Lerp(.48f, .36f, wetness)) continue;
                var width = Vector2.Distance(center, CellToWorldCenter(cell + Vector2Int.right));
                var offset = new Vector2(((noise >> 8) & 255u) / 255f - .5f,
                    ((noise >> 16) & 255u) / 255f - .5f) * .45f * width;
                const float scale = 1f;
                var coverSprite = DungeonVegetationPresentation.SelectDetailSprite(sprite, vegetationPresentation.GroundCoverVariants, noise ^ 0x52A39u);
                var decoration = CreateDecoration("Forest ground cover", coverSprite, transform,
                    center + offset, GetFloorFeedbackDepthSortingOrder(),
                    Color.Lerp(new Color(.45f, .43f, .29f, .65f),
                        new Color(.29f, .38f, .26f, .85f), wetness),
                    scale, FloorFeedbackSortingLayerID);
                decoration.Cell = cell;
                decoration.Renderer.flipX = (noise & 1u) != 0;
                decoration.Object.transform.localRotation = Quaternion.identity;
                decoration.Object.transform.position += (Vector3)(center + offset - (Vector2)decoration.Renderer.bounds.center);
                presentationDecorations.Add(decoration);
                floorSortedPresentations.Add(decoration.Renderer);
            }
        }

        private List<Vector2Int> SelectForestDetailCells()
        {
            bool Clear(Vector2Int cell, bool besideObstacle)
            {
                if (!boundMap.FloorCells.Contains(cell) || presentationOccupiedCells.Contains(cell)
                    || forestPathObstacles.Contains(cell)) return false;
                for (var y = -1; y <= 1; y++) for (var x = -1; x <= 1; x++)
                {
                    var neighbour = cell + new Vector2Int(x,y);
                    if (forestVisualPathCells.Contains(neighbour)) return false;
                    if (presentationOccupiedCells.Contains(neighbour)
                        && (!besideObstacle || !forestPathObstacles.Contains(neighbour))) return false;
                }
                return true;
            }
            bool Edge(Vector2Int cell)
            {
                for (var y = -1; y <= 1; y++) for (var x = -1; x <= 1; x++)
                    if (!boundMap.FloorCells.Contains(cell + new Vector2Int(x,y))) return true;
                return false;
            }
            // The old boundary slots define the entire decoration budget.
            var cells = boundMap.FloorCells.Where(c => VegetationHash(c + new Vector2Int(37,91)) % 11u == 0
                    && Edge(c) && Clear(c,false)).OrderBy(c => c.y).ThenBy(c => c.x).ToList();
            if (cells.Count == 0 || forestPathObstacles.Count == 0) return cells;
            var interior = new HashSet<Vector2Int>();
            foreach (var obstacle in forestPathObstacles)
                for (var y = -1; y <= 1; y++) for (var x = -1; x <= 1; x++)
                {
                    var candidate = obstacle + new Vector2Int(x,y);
                    if (!Edge(candidate) && Clear(candidate,true)) interior.Add(candidate);
                }
            var limit = Mathf.CeilToInt(cells.Count / 3f);
            var selected = new List<Vector2Int>();
            // One deterministic focus per glade keeps base plants in small
            // compositions rather than giving every obstacle the same ring.
            var focuses = forestPathObstacles.GroupBy(c =>
                { boundMap.TryGetRoomId(c, out var id); return id; })
                .Select(g => g.OrderBy(c => VegetationHash(c)).ThenBy(c => c.y).ThenBy(c => c.x).First())
                .ToArray();
            foreach (var candidate in interior.OrderBy(c => focuses.Min(f => (f-c).sqrMagnitude))
                .ThenBy(c => VegetationHash(c)).ThenBy(c => c.y).ThenBy(c => c.x))
            {
                if (selected.Any(c => (c-candidate).sqrMagnitude < 4)) continue;
                selected.Add(candidate);
                if (selected.Count == limit) break;
            }
            for (var i=0; i<selected.Count; i++) cells[i] = selected[i];
            return cells;
        }

        private void BuildForestEdgeDetails()
        {
            foreach (var cell in SelectForestDetailCells())
            {
                var noise = VegetationHash(cell + new Vector2Int(37, 91));
                // Keep the same sparse slots, but relate species to the broad
                // moisture field used by the floor shader instead of scattering
                // every species independently across the whole forest.
                var center = CellToWorldCenter(cell);
                var moisture = Mathf.PerlinNoise(center.x * .24f + 41.7f, center.y * .24f + 73.2f);
                var wetChance = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.35f, .65f, moisture));
                var wet = ((noise >> 24) & 255u) / 255f < wetChance;
                var choice = (noise >> 3) % 5u;
                var kind = choice == 0 ? 1u : wet
                    ? ((noise & 1024u) != 0 && choice <= 2 ? 6u : choice <= 2 ? 3u : 4u)
                    : (choice <= 2 ? ((noise & 512u) != 0 ? 5u : 0u) : 2u);
                // Nearby slots share a dominant species. Occasional accents
                // preserve variety without the evenly mixed confetti effect.
                var patch = Mathf.PerlinNoise(center.x * .13f + 17.3f, center.y * .13f + 93.1f);
                if ((noise & 3u) != 0)
                    kind = wet ? (patch < .5f ? 3u : 4u)
                        : (patch < .42f ? 2u : patch < .58f ? 0u : 5u);
                // At obstacle bases use compact undergrowth rather than logs
                // or loose rocks. Fixed neighbour order avoids hash-set order
                // changing either the species or the grounding offset.
                var baseCell = cell;
                var besideBase = false;
                for (var y = -1; y <= 1 && !besideBase; y++)
                    for (var x = -1; x <= 1 && !besideBase; x++)
                    {
                        var candidate = cell + new Vector2Int(x,y);
                        if (!forestPathObstacles.Contains(candidate)) continue;
                        baseCell = candidate;
                        besideBase = true;
                    }
                if (besideBase)
                    kind = wet ? ((noise & 512u) == 0 ? 3u : 4u)
                        : ((noise & 512u) == 0 ? 2u : 1u);
                var sprite = kind switch
                {
                    0 => vegetationPresentation.Deadwood,
                    1 => vegetationPresentation.LowShrub,
                    2 => vegetationPresentation.ExposedRoots,
                    3 => vegetationPresentation.WoodlandFungi,
                    4 => vegetationPresentation.WoodlandFern,
                    5 => vegetationPresentation.FallenLog,
                    _ => vegetationPresentation.SmallStone
                };
                var dryShrub = kind == 1 && !wet && (noise & 2048u) != 0
                    && vegetationPresentation.DryShrub != null;
                if (dryShrub) sprite = vegetationPresentation.DryShrub;
                if (sprite == null)
                {
                    kind %= 2u;
                    sprite = kind == 0 ? vegetationPresentation.Deadwood : vegetationPresentation.LowShrub;
                }
                if (sprite == null) continue;
                var variants = kind switch
                {
                    0 => vegetationPresentation.DeadwoodVariants,
                    1 => vegetationPresentation.LowShrubVariants,
                    2 => vegetationPresentation.ExposedRootsVariants,
                    3 => vegetationPresentation.WoodlandFungiVariants,
                    4 => vegetationPresentation.WoodlandFernVariants,
                    5 => vegetationPresentation.FallenLogVariants,
                    _ => vegetationPresentation.SmallStoneVariants
                };
                if (!dryShrub)
                    sprite = DungeonVegetationPresentation.SelectDetailSprite(sprite, variants, noise ^ 0xA7153u);
                var name = kind switch { 0 => "Forest deadwood", 1 => "Forest low shrub", 2 => "Forest exposed roots", 3 => "Forest woodland fungi", 4 => "Forest woodland fern", 5 => "Forest fallen log", _ => "Forest small stone" };
                var tint = kind switch
                {
                    0 => new Color(.53f, .55f, .46f, 1),
                    1 => new Color(.44f, .49f, .38f, 1),
                    2 => new Color(.50f, .52f, .43f, 1),
                    3 => new Color(.55f, .56f, .46f, 1),
                    6 => new Color(.62f, .66f, .56f, 1),
                    _ => new Color(.48f, .53f, .40f, 1)
                };
                // Match the surrounding soil in broad patches, not random
                // per-sprite colours. Keep the dry thorn shrub brown even in
                // the transition toward wetter ground.
                var dryTint = kind switch
                {
                    0 => new Color(.56f, .49f, .38f, 1),
                    1 => new Color(.48f, .45f, .32f, 1),
                    2 => new Color(.54f, .47f, .36f, 1),
                    3 => new Color(.58f, .51f, .40f, 1),
                    6 => new Color(.61f, .59f, .48f, 1),
                    _ => new Color(.50f, .47f, .33f, 1)
                };
                tint = Color.Lerp(dryTint, tint, wetChance);
                if (dryShrub) tint = new Color(.55f,.51f,.43f,1);
                var width = Vector2.Distance(center, CellToWorldCenter(cell + Vector2Int.right));
                var offset = new Vector2(((noise >> 8) & 255u) / 255f - .5f,
                    ((noise >> 16) & 255u) / 255f - .5f) * .25f * width;
                // Pull the existing slot slightly toward the base, keeping
                // its centre inside its own unoccupied cell.
                if (besideBase)
                    offset = Vector2.ClampMagnitude(offset + ((Vector2)(baseCell-cell)).normalized * .18f * width, .3f * width);
                const float scale = 1f;
                var decoration = CreateDecoration(name,
                    sprite, transform, center + offset, GetFloorFeedbackDepthSortingOrder(),
                    tint, scale, FloorFeedbackSortingLayerID);
                decoration.Cell = cell;
                decoration.Object.layer = gameObject.layer;
                decoration.Renderer.flipX = (noise & 1u) != 0;
                var bounds = decoration.Renderer.bounds;
                var anchor = (Vector2)bounds.center;
                var target = center + offset;
                // Upright plants share a ground baseline; different sprite
                // heights must not move their roots toward/away from a trunk.
                // Flat wood, roots and stones retain footprint centring.
                if (kind == 1 || kind == 3 || kind == 4)
                {
                    anchor.y = bounds.min.y;
                    target.y -= .25f * width;
                }
                decoration.Object.transform.position += (Vector3)(target - anchor);
                forestPropContacts[decoration.Renderer] = target;
                presentationDecorations.Add(decoration);
                floorSortedPresentations.Add(decoration.Renderer);
            }
        }

        private readonly Dictionary<DungeonSurfaceEdge, DungeonKnowledge> vegetationEdgeKnowledge = new();
        private readonly Dictionary<DungeonSurfaceEdge, List<int>> vegetationGroundByEdge = new();
        private readonly HashSet<int> dirtyVegetationGround = new();
        private void RefreshForestBoundary(Renderer renderer, DungeonSurfaceEdge fallback)
        {
            var projected = false;
            if (forestBoundaryOwners.TryGetValue(renderer, out var edges))
            {
                foreach (var edge in edges)
                    if (IsFogEdgeVisible(edge)) { projected = true; break; }
            }
            else projected = IsFogEdgeVisible(fallback);
            // Static woodland uses natural lighting, never discovery of an
            // arbitrary owner edge. Only projection/cutaway may hide it.
            // Routes, gates and units retain their separate fog knowledge.
            var knowledge=forestExitRails.Contains(renderer) || renderer is SpriteRenderer hedge && forestLowHedgeBaselines.ContainsKey(hedge)
                ? EdgeKnowledge(fallback):DungeonKnowledge.Visible;
            var hidden = !projected || knowledge==DungeonKnowledge.Unexplored;
            if (renderer.forceRenderingOff != hidden) renderer.forceRenderingOff = hidden;
            TintFog(renderer, knowledge);
        }
        private void RefreshVegetationGround()
        {
            // Resolve owners freshly every refresh. Only underlays whose
            // owners changed need presentation work; distant unchanged forest
            // layers are not visited for each step of the party.
            dirtyVegetationGround.Clear();
            foreach (var edge in FogVegetationEdges())
            {
                // These patches only cover off-map woodland, never routes.
                var next = IsFogEdgeVisible(edge) ? DungeonKnowledge.Visible : DungeonKnowledge.Unexplored;
                if (vegetationEdgeKnowledge.TryGetValue(edge, out var previous) && previous == next) continue;
                vegetationEdgeKnowledge[edge] = next;
                foreach (var index in vegetationGroundByEdge[edge]) dirtyVegetationGround.Add(index);
            }
            foreach (var index in dirtyVegetationGround)
            {
                var ground = vegetationGround[index];
                if (ground.Renderer == null) continue;
                var knowledge = DungeonKnowledge.Unexplored;
                foreach (var edge in ground.Edges)
                {
                    var candidate = vegetationEdgeKnowledge[edge];
                    if (candidate > knowledge) knowledge = candidate;
                    if (knowledge == DungeonKnowledge.Visible) break;
                }
                var hidden = knowledge == DungeonKnowledge.Unexplored;
                if (ground.Renderer.forceRenderingOff != hidden) ground.Renderer.forceRenderingOff = hidden;
                TintFog(ground.Renderer, knowledge);
            }
        }

        private void ClearVegetationGround()
        {
            ClearForestKnowledge();
            ReleaseForestDifficultTerrainMask();
            forestBackdrop = null;
            ClearForestProgressionGates();
            ClearForestExitRails();
            forestLowHedgeBaselines.Clear();
            forestCanopyCells.Clear(); forestCanopyGroups.Clear(); forestCanopyBaselines.Clear(); forestFrontRowCanopies.Clear();
            forestCanopyGroundBounds.Clear();
            forestCanopyShapes.Clear();
            forestTreeFootprints.Clear();
            forestDividerCanopies.Clear();
            forestPassageMap = null; forestPassageTopologyRevision = -1;
            forestPassageCells.Clear(); forestPassageCanopies.Clear();
            forestBoundaryOwners.Clear();
            forestWideTrunks.Clear(); forestLargeRoots.Clear();
            InvalidateForestLightingCoverage();
            forestVisualPathCells.Clear();
            if (forestSurfaceMask != null)
            {
                if (Application.isPlaying) Destroy(forestSurfaceMask); else DestroyImmediate(forestSurfaceMask);
                forestSurfaceMask = null;
            }
            foreach (var ground in vegetationGround) UnregisterLightingPresentation(ground.Renderer);
            vegetationGround.Clear();
            vegetationGroundByEdge.Clear();
            vegetationEdgeKnowledge.Clear();
            dirtyVegetationGround.Clear();
            vegetationGroundRenderers.Clear();
            forestPropContacts.Clear();
            vegetationColliders.Clear();
        }

        private static Matrix4x4 ForestGroundTransform(Sprite sprite, Vector2Int cell)
        {
            // Rotate around the normalized tile centre, never around the
            // imported bottom pivot: floor and fog bounds must stay aligned.
            var noise = VegetationHash(cell);
            var variation = Matrix4x4.TRS(Vector3.zero,
                Quaternion.Euler(0, 0, (noise % 4u) * 90f),
                new Vector3((noise & 4u) == 0 ? 1 : -1, 1, 1));
            return variation * DungeonVegetationPresentation.CenteredGroundTransform(sprite);
        }

        private static uint VegetationHash(Vector2Int cell)
        {
            unchecked
            {
                var value = (uint)(cell.x * 73856093 ^ cell.y * 19349663 ^ 13007);
                value ^= value >> 16; value *= 2246822519u; value ^= value >> 13;
                return value;
            }
        }
    }
}
