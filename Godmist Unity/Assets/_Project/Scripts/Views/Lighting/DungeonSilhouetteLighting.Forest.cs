using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using UnityEngine;
using UnityEngine.Rendering;

namespace Godmist.Views.Lighting
{
    public sealed partial class DungeonSilhouetteLighting
    {
        private MaterialPropertyBlock torchFogProperties;
        private static readonly int CanopyRevealId = Shader.PropertyToID("_ForestCanopy");
        private static readonly int CanopyFadeId = Shader.PropertyToID("_ForestCanopyFade");
        private static readonly int ForestReadabilityProfileId = Shader.PropertyToID("_ForestReadabilityProfile");
        private static readonly int CanopyRootTopId = Shader.PropertyToID("_ForestCanopyRootTopY");
        private static readonly int CanopyCellSizeId = Shader.PropertyToID("_ForestCanopyCellSize");
        private static readonly int CanopyRootBoundsId = Shader.PropertyToID("_ForestCanopyRootBounds");
        private DungeonFloorMap canopyCellMap;
        private float canopyCellHeight = 1;
        private bool ConfigureCanopyReveal(SpriteRenderer renderer, MaterialPropertyBlock block)
        {
            bool canopy = terrain != null && terrain.UsesVegetationPresentation && terrain.IsForestCanopyRenderer(renderer);
            bool changed = SetReceiverFloat(block, CanopyRevealId, canopy ? 1f : 0f);
            changed |= SetReceiverFloat(block, CanopyFadeId, canopy && canopyFades.TryGetValue(renderer, out var fade) ? 1-fade.Alpha : 0);
            if (canopy)
            {
                if (!ReferenceEquals(canopyCellMap, terrain.LightingMap))
                {
                    canopyCellMap = terrain.LightingMap;
                    canopyCellHeight = Vector2.Distance(terrain.CellToWorldCenter(Vector2Int.up), terrain.CellToWorldCenter(Vector2Int.zero));
                }
                var ground = terrain.ForestCanopyGroundBounds(renderer);
                var rootTop = ground.yMax;
                changed |= SetReceiverFloat(block, CanopyRootTopId, rootTop);
                changed |= SetReceiverFloat(block, CanopyCellSizeId, canopyCellHeight);
                changed |= SetReceiverVector(block, CanopyRootBoundsId, new Vector4(ground.xMin, ground.xMax, 1, 0));
            }
            return changed;
        }
        private Sprite forestFlameSprite, forestStandSprite;
        private DungeonFloorInstance forestFixtureFloor;
        private readonly SpriteRenderer[] fixtureCanopies = new SpriteRenderer[4];
        private readonly Dictionary<SpriteRenderer, Torch> arenaFireLights = new();
        private readonly List<SpriteRenderer> expiredArenaFireLights = new();
        public int ActiveArenaFireLightCount => activeTorches.Count(t => t.arenaFire);
        public int ForestStaticLightCount => torches.Count(t => !t.carried) * (terrain != null && terrain.UsesVegetationPresentation ? 1 : 0);
        public const float ForestFixtureSpacing = 9f;

        public static bool HasForestFixtureSpace(Vector2 candidate, IEnumerable<Vector2> existing)
            => !existing.Any(p => (p-candidate).sqrMagnitude < ForestFixtureSpacing * ForestFixtureSpacing);

        private bool IsForestFixtureRoom(Vector2Int cell)
        {
            var floor = terrain.FogFloor;
            if (floor == null || !terrain.LightingMap.TryGetRoomId(cell, out var id)) return false;
            var room = floor.Rooms.FirstOrDefault(r => r.RoomId == id);
            return room != null && room.RoomType != DungeonRoomType.Final && room.RoomType != DungeonRoomType.Corridor;
        }

        private void BuildForestFixtures()
        {
            var floor = terrain.FogFloor;
            if (floor == null || ReferenceEquals(forestFixtureFloor, floor)) return;
            forestFixtureFloor = floor;
            forestStandSprite = Resources.Load<Sprite>("Godmist/ForestArt/Fixtures/forest-brazier-32ppu");
            if (forestStandSprite == null) { Debug.LogError("Missing native forest brazier sprite",this); return; }
            var placed = new List<Vector2>();
            // Existing camp compositions already contain a hearth, bedroll and
            // crate. Lighting their hearth does not add blockers or new loot.
            foreach (var item in floor.Objects.OrderBy(o => o.Definition.Id == "object.forest.cold_campfire.prototype" ? 0 : 1).ThenBy(o => o.Position.y).ThenBy(o => o.Position.x))
            {
                var id = item.Definition.Id;
                bool camp = id == "object.forest.cold_campfire.prototype";
                bool stand = id == "object.forest.ruined_pillar.prototype" || id == "object.forest.broken_crate.prototype";
                if ((!camp && !stand) || !IsForestFixtureRoom(item.Position)) continue;
                var position = terrain.CellToWorldCenter(item.Position);
                if (!camp)
                {
                    var beside = new[]{Vector2Int.right,Vector2Int.left,Vector2Int.down,Vector2Int.up}
                        .Select(d=>item.Position+d).Where(c=>terrain.LightingMap.IsWalkable(c)
                            && !floor.Objects.OfType<ObstacleDungeonObjectInstance>().Any(o=>o.IsActive && o.BlocksMovement && o.OccupiesCell(c))).Take(1).ToArray();
                    if (beside.Length == 0) continue;
                    position = terrain.CellToWorldCenter(beside[0]);
                }
                if (!HasForestFixtureSpace(position, placed)) continue;
                // Only half of abandoned hearths are still burning.
                if (camp && placed.Count > 0 && ((item.Position.x * 31 + item.Position.y * 17) & 1) != 0) continue;
                var mount = objects?.SpawnedObjects.FirstOrDefault(v => v != null && v.ObjectInstanceId == item.InstanceId);
                AddForestFixture(position, new DungeonSurfacePosition(terrain.WorldToCell(position)), camp, camp && mount != null ? mount.GetComponent<SortingGroup>() : null);
                placed.Add(position);
            }
            // Grounded free-standing fixtures beside the trail replace tree stickers.
            foreach (var anchor in terrain.ForestTorchAnchors())
            {
                var cell = anchor.cell.Cell;
                if (!IsForestFixtureRoom(cell) || !HasForestFixtureSpace(anchor.position,placed)) continue;
                if (unchecked((cell.x*31+cell.y*17)&7)!=0) continue;
                var fixtureCell = FindForestFixtureCell(cell, floor);
                var fixturePosition = terrain.CellToWorldCenter(fixtureCell);
                if (!HasForestFixtureSpace(fixturePosition, placed)) continue;
                AddForestFixture(fixturePosition,new DungeonSurfacePosition(fixtureCell),false);
                placed.Add(fixturePosition);
            }
        }

        private Vector2Int FindForestFixtureCell(Vector2Int start, DungeonFloorInstance floor)
        {
            var best = start; var bestScore = float.PositiveInfinity;
            var pending = new Queue<(Vector2Int cell, int depth)>();
            var visited = new HashSet<Vector2Int> { start }; pending.Enqueue((start,0));
            var directions = new[] {Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left};
            while (pending.Count > 0)
            {
                var item = pending.Dequeue(); var position = terrain.CellToWorldCenter(item.cell);
                if (!floor.Objects.OfType<ObstacleDungeonObjectInstance>().Any(o => o.IsActive && o.BlocksMovement && o.OccupiesCell(item.cell)))
                {
                    var bounds = forestStandSprite.bounds; bounds.center += (Vector3)position;
                    var covered = terrain.CollectForestCanopies(item.cell, bounds, position.y, fixtureCanopies);
                    var score = covered*100 + item.depth;
                    if (score < bestScore) { bestScore = score; best = item.cell; }
                }
                if (item.depth >= 2) continue;
                foreach (var direction in directions)
                {
                    var next = item.cell+direction;
                    if (terrain.LightingMap.CanMoveBetween(item.cell,next) && visited.Add(next))
                        pending.Enqueue((next,item.depth+1));
                }
            }
            return best;
        }

        private void RefreshLightSources(Vector2 cameraPosition, float sampledTime)
        {
            using (SourceFixturesSample.Auto())
            {
                if (!ReferenceEquals(torchMap, terrain.LightingMap))
                { ClearTorchFixtures(); torchMap = terrain.LightingMap; }
                if (torches.Count == 0) BuildTorches();
                if (terrain.UsesVegetationPresentation && terrain.FogFloor != null
                    && !ReferenceEquals(forestFixtureFloor, terrain.FogFloor)) BuildForestFixtures();
            }
            activeTorches.Clear();
            var leader = party.ActiveLeaderTransform;
            int layer = terrain.FloorFeedbackSortingLayerID;
            foreach (var torch in torches)
            {
                if (torch.carried)
                {
                    if (leader == null) { torch.view.enabled = false; continue; }
                    torch.position = (Vector2)leader.position + Vector2.right * .3f;
                    torch.view.transform.position = torch.position + Vector2.up * .65f;
                }
                RefreshTorchPresentation(torch, layer);
                using (SourceCandidatesSample.Auto())
                {
                    // Reject distant sources before querying layered terrain.
                    // Hidden fixtures may still illuminate visible ground.
                    var radius = torchRadius * torch.radiusScale + 1f;
                    if (DistanceSquaredToRect(torch.position, cameraGroundBounds) > radius * radius
                        || !torch.carried && terrain.IsSurfaceOccluded(torch.cell)) continue;
                    SelectLight(torch, cameraPosition);
                }
            }
            using (ArenaSourcesSample.Auto()) SelectArenaFireLights(cameraPosition);
            WriteLightInputs(sampledTime);
        }

        private void RefreshTorchPresentation(Torch torch, int layer)
        {
            using var sample = TorchPresentationSample.Auto();
            var view = torch.view;
            // Sorting and canopy queries only matter when the fixture can be
            // drawn. Recompute before enabling it, so camera/fog returns and
            // changing canopy/owner sorting keep the original appearance.
            var knowledge = torch.carried ? Godmist.Runtime.Dungeons.DungeonKnowledge.Visible
                : terrain.Knowledge(new Godmist.Runtime.Dungeons.DungeonTraversalPosition(torch.cell));
            bool visible = IntersectsCamera(view.bounds, 0)
                && knowledge != Godmist.Runtime.Dungeons.DungeonKnowledge.Unexplored
                && (torch.carried || !terrain.IsSurfaceOccluded(torch.cell));
            var brightness = Godmist.Views.Dungeons.DungeonFogBlend.Brightness(knowledge);
            if (!Mathf.Approximately(torch.fogBrightness, brightness))
            {
                torch.fogBrightness = brightness;
                torchFogProperties ??= new MaterialPropertyBlock();
                view.GetPropertyBlock(torchFogProperties);
                torchFogProperties.SetFloat("_FogBrightness", brightness);
                view.SetPropertyBlock(torchFogProperties);
                if (torch.companions != null)
                {
                    if (torch.companionBaseColors == null)
                    {
                        torch.companionBaseColors = new Color[torch.companions.Length];
                        for (var i=0;i<torch.companions.Length;i++) torch.companionBaseColors[i]=torch.companions[i].color;
                    }
                    for (var i=0;i<torch.companions.Length;i++)
                    {
                        var c=torch.companionBaseColors[i];
                        torch.companions[i].color=new Color(c.r*brightness,c.g*brightness,c.b*brightness,c.a);
                    }
                }
            }
            int order = 0;
            if (visible)
            {
                order = ResolveTorchSortingOrder(torch); TorchSortingQueryCount++;
                if (view.sortingLayerID != layer) view.sortingLayerID = layer;
                if (view.sortingOrder != order) view.sortingOrder = order;
            }
            if (view.enabled != visible) view.enabled = visible;
            if (torch.companions == null) return;
            foreach (var companion in torch.companions)
            {
                if (visible)
                {
                    if (companion.sortingLayerID != layer) companion.sortingLayerID = layer;
                    if (companion.sortingOrder != order) companion.sortingOrder = order;
                }
                if (companion.enabled != visible) companion.enabled = visible;
            }
        }

        private void SelectLight(Torch torch, Vector2 cameraPosition)
        {
            float priority = (torch.position-cameraPosition).sqrMagnitude;
            int insert = activeTorches.Count;
            while (insert > 0 && (activeTorches[insert-1].position-cameraPosition).sqrMagnitude > priority) insert--;
            if (insert >= MaxLights) return;
            activeTorches.Insert(insert, torch);
            if (activeTorches.Count > MaxLights) activeTorches.RemoveAt(MaxLights);
        }

        private void SelectArenaFireLights(Vector2 cameraPosition)
        {
            expiredArenaFireLights.Clear();
            foreach (var flame in arenaFireLights.Keys) if (flame == null) expiredArenaFireLights.Add(flame);
            foreach (var flame in expiredArenaFireLights) arenaFireLights.Remove(flame);
            foreach (var source in terrain.ArenaFireLightSources())
            {
                const float radiusScale = 1.3f;
                var radius = torchRadius*radiusScale+1;
                if (DistanceSquaredToRect(source.position, cameraGroundBounds) > radius*radius
                    || terrain.IsSurfaceOccluded(source.cell)) continue;
                if (!arenaFireLights.TryGetValue(source.flame, out var light))
                {
                    light = new Torch { view=source.flame, arenaFire=true, radiusScale=radiusScale,
                        phase=Mathf.Repeat(source.position.x*1.73f + source.position.y*3.17f, Mathf.PI*2) };
                    arenaFireLights.Add(source.flame, light);
                }
                light.position = source.position; light.cell = source.cell;
                // Several shifted trees can share a single small ground area.
                // Spend one slot there, keeping the nearest representative,
                // rather than all eight lights on the same side of the arena.
                var represented = false;
                for (var i = 0; i < activeTorches.Count; i++)
                {
                    var current = activeTorches[i];
                    if (!current.arenaFire || (current.position-source.position).sqrMagnitude >= 9f) continue;
                    if ((current.position-cameraPosition).sqrMagnitude <= (source.position-cameraPosition).sqrMagnitude)
                    { represented = true; break; }
                }
                if (represented) continue;
                for (var i = activeTorches.Count-1; i >= 0; i--)
                    if (activeTorches[i].arenaFire && (activeTorches[i].position-source.position).sqrMagnitude < 9f)
                        activeTorches.RemoveAt(i);
                SelectLight(light, cameraPosition);
            }
        }

        private void AddForestFixture(Vector2 position, DungeonSurfacePosition cell, bool camp, SortingGroup mountGroup = null, SpriteRenderer mountSprite = null)
        {
            var go = new GameObject(camp ? "Forest campfire flame" : "Forest freestanding brazier");
            go.transform.SetParent(transform, false);
            // Forest fixtures have their own grounded stand; hearths use only
            // the flame crop from this dedicated forest art.
            go.transform.position = position + Vector2.up * (camp ? .125f : 0);
            var renderer = go.AddComponent<SpriteRenderer>();
            if (camp && forestFlameSprite == null)
            {
                var rect = forestStandSprite.rect;
                var flameHeight = Mathf.Ceil(rect.height * .30f);
                rect.y += rect.height - flameHeight; rect.height = flameHeight;
                forestFlameSprite = Sprite.Create(forestStandSprite.texture, rect, new Vector2(.5f, 0), 32, 0, SpriteMeshType.FullRect);
                forestFlameSprite.name = "Forest hearth flame (forest brazier art)";
            }
            renderer.sprite = camp ? forestFlameSprite : forestStandSprite;
            if (!camp) RegisterGroundContactPresentation(renderer);
            renderer.sharedMaterial = flameMaterial;
            renderer.color = Color.white;
            renderer.sortingLayerID = terrain.FloorFeedbackSortingLayerID;
            renderer.sortingOrder = mountGroup != null ? mountGroup.sortingOrder + 1
                : mountSprite != null ? mountSprite.sortingOrder + 1
                : terrain.GetStandingObjectDepthSortingOrder(position.y) + 1;
            float phase = Mathf.Repeat(cell.Cell.x * 1.73f + cell.Cell.y * 3.17f, Mathf.PI * 2);
            var block = new MaterialPropertyBlock(); block.SetFloat("_Phase", phase);
            block.SetFloat("_ForestFixture",1); block.SetFloat("_FlameBase",camp ? 0 : .68f);
            renderer.SetPropertyBlock(block);
            SpriteRenderer[] companions = null;
            if (camp)
            {
                // Three authored flame fragments fill the hearth without
                // enlarging pixels or spending three shadow-map light slots.
                companions = new SpriteRenderer[2];
                for (int i = 0; i < companions.Length; i++)
                {
                    var child = new GameObject("Hearth side flame");
                    child.transform.SetParent(go.transform, false);
                    child.transform.localPosition = new Vector3(i == 0 ? -.125f : .125f, -.03125f, 0);
                    var flame = child.AddComponent<SpriteRenderer>();
                    flame.sprite = forestFlameSprite; flame.sharedMaterial = flameMaterial;
                    flame.color = renderer.color;
                    flame.sortingLayerID = renderer.sortingLayerID; flame.sortingOrder = renderer.sortingOrder;
                    block.SetFloat("_Phase", phase + (i+1)*1.1f); flame.SetPropertyBlock(block);
                    companions[i] = flame;
                }
            }
            torches.Add(new Torch { position = position, cell = cell, view = renderer, phase = phase, radiusScale = camp ? .72f : .65f, companions = companions, mountGroup = mountGroup, mountSprite = mountSprite, forestFixture = true });
        }

        private int ResolveTorchSortingOrder(Torch torch)
        {
            // A fixture is grounded scenery: overlapping crowns must obey the
            // same baseline depth as rocks and actors, not force it in front.
            return torch.mountGroup != null ? torch.mountGroup.sortingOrder + 1
                : torch.mountSprite != null ? torch.mountSprite.sortingOrder + 1
                : torch.forestFixture ? terrain.GetStandingObjectDepthSortingOrder(torch.position.y) + 1
                : terrain.GetWallMountedDepthSortingOrder(torch.position.y);
        }

        private void ClearTorchFixtures()
        {
            foreach (var torch in torches)
                if (torch.view != null) DestroyLightingObject(torch.view.gameObject);
            torches.Clear(); activeTorches.Clear();
            arenaFireLights.Clear();
            expiredArenaFireLights.Clear();
            ClearForestFixtures();
        }

        private void ClearForestFixtures()
        {
            if (forestFlameSprite != null) DestroyLightingObject(forestFlameSprite);
            forestFlameSprite = null;
            forestFixtureFloor = null;
        }
    }
}
