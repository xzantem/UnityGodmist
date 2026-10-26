using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Exploration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.Dungeons
{
    /// <summary>Read-only HUD projection. Uses saved fog knowledge; never casts visibility rays.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed partial class DungeonMinimapView : MonoBehaviour
    {
        [SerializeField] private RawImage image;
        [SerializeField] private GameObject content;
        [SerializeField] private Button toggle;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text toggleLabel;
        [SerializeField] private DungeonMinimapMarkers markers;
        [SerializeField] private Color bodyColor = new(.8f, .8f, .8f);
        private static readonly Color UnlootedBodyColor = new(.55f, .12f, .2f);
        [SerializeField] private Color obstacleColor = new(.18f, .2f, .23f);
        [SerializeField] private Color chestColor = new(.8f, .55f, .2f);
        [SerializeField] private Color interactionColor = new(.65f, .4f, .8f);
        [SerializeField] private Color visibleColor = new(.48f, .55f, .59f);
        [SerializeField] private Color rememberedColor = new(.25f, .3f, .34f);
        [SerializeField] private Color partyColor = new(.3f, .8f, 1f);
        [SerializeField] private Color leaderColor = new(1f, .8f, .25f);
        [SerializeField] private Color enemyColor = new(1f, .2f, .18f);
        public const int TextureSize = 252;
        private const int Size = TextureSize, ViewSize = 220, Scale = 8;
        private readonly Color32[] terrain = new Color32[Size * Size];
        private readonly HashSet<DungeonSurfaceEdge> walls = new();
        private DungeonFloorInstance floor;
        private DungeonFloorMapView map;
        private ExplorationPartyController party;
        private DungeonEncounterSpawner encounters;
        private Texture2D texture;
        private Vector2Int center;
        private int level, fogRevision = -1, debugRevision = -1;
        private float nextRefresh;
        private bool dirty = true;
        private readonly Vector2[] cameraCells = new Vector2[4];
        public int MinimapObjectKnowledgeQueryCount { get; private set; }
        private static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        public void Bind(DungeonFloorInstance nextFloor, DungeonFloorMapView nextMap,
            ExplorationPartyController nextParty, DungeonEncounterSpawner nextEncounters)
        {
            SetOverviewOpen(false);
            if (party != null && party.DungeonMinimap == this) party.BindDungeonMinimap(null);
            floor = nextFloor; map = nextMap; party = nextParty; encounters = nextEncounters;
            if (party != null) party.BindDungeonMinimap(this);
            walls.Clear();
            if (floor?.Map != null)
                foreach (var edge in floor.Map.SurfaceWallEdges)
                    if (floor.Map.GetWallType(edge) != DungeonWallType.TreeTrunk) walls.Add(edge);
            dirty = true; nextRefresh = 0;
            if (markers != null) { markers.ClearCameraFrame(); markers.Refresh(); }
            RefreshTitle();
        }
        private void Awake()
        {
            texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "Dungeon minimap", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            if (image != null) image.texture = texture;
            if (toggle != null) toggle.onClick.AddListener(Toggle);
            RefreshTitle();
        }
        private void Toggle()
        {
            if (IsOverviewOpen) { SetOverviewOpen(false); return; }
            if (content == null) return;
            content.SetActive(!content.activeSelf);
            dirty = true; nextRefresh = 0;
            RefreshTitle();
        }
        private void RefreshTitle()
        {
            var depth = -(floor?.FloorIndex ?? 0);
            var bottom = -Mathf.Max(0, (party?.Session?.ActiveDungeon?.Floors.Count ?? 1) - 1);
            var floorLabel = bottom < 0 ? $"{depth}/{bottom}" : depth.ToString();
            if (title != null) title.text = IsOverviewOpen ? $"Mapa - poziom {floorLabel}     M / Esc - zamknij" : $"Mapa - poziom {floorLabel}";
            if (toggleLabel != null) toggleLabel.text = IsOverviewOpen ? "X" : content != null && content.activeSelf ? "-" : "+";
        }
        private static readonly Unity.Profiling.ProfilerMarker MinimapLateSample = new("Godmist.Dungeon.Minimap.Late");
        private static readonly Unity.Profiling.ProfilerMarker MinimapTerrainSample = new("Godmist.Dungeon.Minimap.Terrain");
        private static readonly Unity.Profiling.ProfilerMarker MinimapMarkersSample = new("Godmist.Dungeon.Minimap.Markers");
        // Keep early M/Esc input handling, but project after the world camera's
        // LateUpdate (follow, free pan and combat zoom), just before UI rendering.
        private void OnEnable()
        {
            Godmist.Views.UI.Workspace.DungeonLootWindowView.LootClaimed -= OnLootClaimed;
            Godmist.Views.UI.Workspace.DungeonLootWindowView.LootClaimed += OnLootClaimed;
            Canvas.preWillRenderCanvases -= RefreshProjection;
            Canvas.preWillRenderCanvases += RefreshProjection;
        }
        private void OnLootClaimed(string source, string message) => dirty = true;
        private void RefreshProjection()
        {
            using var sample = MinimapLateSample.Auto();
            if (floor?.Map == null || map == null || party == null || texture == null
                || content == null || !content.activeSelf) return;
            var leader = party.ActiveLeaderTransform;
            var basis = ReadCellBasis();
            var camera = map.PresentationCamera;
            bool hasCamera = TryProjectCamera(camera, basis, out var cameraCenter);
            var nextCenter = hasCamera ? map.WorldToCell(cameraCenter) : center;
            var continuousCenter = hasCamera ? ProjectContinuousCell(cameraCenter, nextCenter, basis) : (Vector2)center;
            var nextLevel = map.ViewedLevel;
            if (IsOverviewOpen)
            {
                RefreshOverview(nextLevel,leader,basis);
                RefreshCameraFrame(hasCamera, overviewCenter);
                markers?.Refresh();
                return;
            }
            // Padding permits sub-cell UV scrolling every frame without rerasterizing terrain.
            var outOfPadding = Mathf.Abs(continuousCenter.x - center.x) > 1.5f || Mathf.Abs(continuousCenter.y - center.y) > 1.5f;
            if (dirty || nextLevel != level || outOfPadding || Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + .1f;
                if (dirty || nextCenter != center || nextLevel != level || fogRevision != floor.Fog.Revision || debugRevision != floor.Fog.DebugRevision)
                {
                    center = nextCenter; level = nextLevel;
                    fogRevision = floor.Fog.Revision; debugRevision = floor.Fog.DebugRevision;
                    using (MinimapTerrainSample.Auto())
                    { DrawTerrain(); texture.SetPixels32(terrain); texture.Apply(false, false); }
                    dirty = false; RefreshTitle();
                }
            }
            if (image != null) image.uvRect = Viewport(continuousCenter, center);
            if (markers == null) return;
            using var markerSample = MinimapMarkersSample.Auto();
            markers.ClearMarkers();
            RefreshCameraFrame(hasCamera, continuousCenter);
            foreach (var member in party.ActiveMemberViews) DrawActor(member, member.transform == leader ? leaderColor : partyColor, false, continuousCenter, basis);
            if (encounters != null)
                for (int triggerIndex = 0; triggerIndex < encounters.Triggers.Count; triggerIndex++)
                {
                    var enemies = encounters.Triggers[triggerIndex].EnemyViews;
                    for (int enemyIndex = 0; enemyIndex < enemies.Count; enemyIndex++)
                        DrawActor(enemies[enemyIndex], enemyColor, true, continuousCenter, basis);
                }
            markers.Refresh();
        }

        private bool TryProjectCamera(Camera camera, (Vector2 X, Vector2 Y) basis, out Vector3 cameraCenter)
        {
            cameraCenter = default;
            if (camera == null || !camera.isActiveAndEnabled) return false;
            var plane = new Plane(Vector3.forward, map.CellToWorldCenter(Vector2Int.zero));
            for (var i = 0; i < 4; i++)
            {
                var viewport = new Vector3(i == 1 || i == 2 ? 1 : 0, i >= 2 ? 1 : 0);
                var ray = camera.ViewportPointToRay(viewport);
                if (!plane.Raycast(ray, out var distance)) return false;
                var world = ray.GetPoint(distance);
                cameraCenter += world * .25f;
                cameraCells[i] = ProjectContinuousCell(world, map.WorldToCell(world), basis);
            }
            return true;
        }

        private Vector2 MarkerOffset(Vector2 cell, Vector2 mapCenter)
        {
            var size = image != null ? image.rectTransform.rect.size : new Vector2(ViewSize, ViewSize);
            var pixels = (cell - mapCenter) * RasterScale;
            return Vector2.Scale(pixels, new Vector2(size.x / (IsOverviewOpen ? RasterWidth : ViewSize),
                size.y / (IsOverviewOpen ? RasterHeight : ViewSize)));
        }

        private void RefreshCameraFrame(bool visible, Vector2 mapCenter)
        {
            if (markers == null) return;
            if (!visible || image == null) { markers.ClearCameraFrame(); return; }
            markers.SetCameraFrame(MarkerOffset(cameraCells[0], mapCenter), MarkerOffset(cameraCells[1], mapCenter),
                MarkerOffset(cameraCells[2], mapCenter), MarkerOffset(cameraCells[3], mapCenter));
        }
        public static Rect Viewport(Vector2 position, Vector2Int rasterCenter)
        {
            var offset = (position - rasterCenter) * Scale;
            return new Rect(((Size - ViewSize) * .5f + offset.x) / Size,
                ((Size - ViewSize) * .5f + offset.y) / Size, (float)ViewSize / Size, (float)ViewSize / Size);
        }
        private Vector2 ContinuousCell(Vector3 world)
        {
            var cell = map.WorldToCell(world);
            return ProjectContinuousCell(world, cell, ReadCellBasis());
        }
        private (Vector2 X, Vector2 Y) ReadCellBasis()
        {
            var origin = map.CellToWorldCenter(Vector2Int.zero);
            var dx = map.CellToWorldCenter(Vector2Int.right) - origin;
            var dy = map.CellToWorldCenter(Vector2Int.up) - origin;
            return (dx, dy);
        }
        private Vector2 ProjectContinuousCell(Vector3 world, Vector2Int cell, (Vector2 X, Vector2 Y) basis)
        {
            var relative = (Vector2)world - map.CellToWorldCenter(cell);
            var dx = basis.X; var dy = basis.Y;
            return cell + new Vector2(dx.sqrMagnitude > .0001f ? Vector2.Dot(relative, dx) / dx.sqrMagnitude : 0,
                dy.sqrMagnitude > .0001f ? Vector2.Dot(relative, dy) / dy.sqrMagnitude : 0);
        }

        public static bool CanShowActor(DungeonFloorInstance floor, DungeonTraversalPosition node, int level, bool enemy)
        {
            if (floor?.Map == null || !floor.Map.TryGetTraversalCell(node, out _)) return false;
            var onLevel = node.IsOnStairs ? node.Stair.First.Level == level || node.Stair.Second.Level == level : node.Surface.Level == level;
            return onLevel && (!enemy || floor.Fog.IsVisible(node));
        }
        private void DrawActor(ExplorationCharacterView actor, Color color, bool enemy, Vector2 continuousCenter, (Vector2 X, Vector2 Y) basis)
        {
            if (actor == null || actor.RuntimeCharacter?.LifeState != CharacterLifeState.Alive
                || enemy && !actor.IsVisibleForInspection
                || !CanShowActor(floor, actor.DungeonPosition, level, enemy)) return;
            var position = actor.transform.position;
            var offset=MarkerOffset(ProjectContinuousCell(position,map.WorldToCell(position),basis),continuousCenter);
            markers.AddMarker(offset,color);
        }
        public static DungeonKnowledge TerrainKnowledge(DungeonFloorInstance floor, DungeonSurfacePosition surface)
        {
            if (floor?.Map == null || !floor.Map.IsWalkable(surface)) return DungeonKnowledge.Unexplored;
            return floor.Fog.DebugFogDisabled ? DungeonKnowledge.Visible : floor.Fog.Get(new DungeonTraversalPosition(surface));
        }
        private void DrawTerrain()
        {
            var terrain=RasterPixels;
            Array.Fill(terrain, (Color32)Color.black);
            if(IsOverviewOpen)
            {
                DrawOverviewTerrainCells(terrain);
            }
            else for (var y = -16; y <= 16; y++) for (var x = -16; x <= 16; x++) DrawCell(center+new Vector2Int(x,y));
            void DrawCell(Vector2Int cell)
            {
                var surface = new DungeonSurfacePosition(cell, level);
                var knowledge = TerrainKnowledge(floor, surface);
                if (knowledge == DungeonKnowledge.Unexplored) return;
                var p=RasterPoint(cell)-Vector2.one*RasterScale*.5f;
                int px=Mathf.RoundToInt(p.x),py=Mathf.RoundToInt(p.y);
                int width=Mathf.Max(1,Mathf.RoundToInt(p.x+RasterScale)-px),height=Mathf.Max(1,Mathf.RoundToInt(p.y+RasterScale)-py);
                Fill(terrain, px, py, width, height, knowledge == DungeonKnowledge.Visible ? visibleColor : rememberedColor);
                if(RasterScale<3)return;
                foreach (var dir in Directions)
                {
                    var neighbour = new DungeonSurfacePosition(cell + dir, level);
                    if (floor.Map.IsWalkable(neighbour) && !walls.Contains(new DungeonSurfaceEdge(surface, neighbour))) continue;
                    Fill(terrain, px + (dir.x > 0 ? width - 1 : 0), py + (dir.y > 0 ? height - 1 : 0), dir.x == 0 ? width : 1, dir.y == 0 ? height : 1, Color.black);
                }
            }
            foreach (var stair in floor.Map.Stairs)
            {
                DrawStair(stair.First); DrawStair(stair.Second);
                if (stair.First.Level != level && stair.Second.Level != level) continue;
                if (!floor.Map.TryGetStairPath(stair, out var path)) continue;
                for (var step = 1; step < path.Count - 1; step++)
                    if (floor.Fog.DebugFogDisabled || floor.Fog.Get(new DungeonTraversalPosition(stair, step)) != DungeonKnowledge.Unexplored)
                        Dot(terrain, path[step], new Color(.8f, .7f, .4f), 3);
            }
            foreach (var edge in floor.Map.SurfaceClosedProgressionGateEdges)
                if (edge.Level == level && (floor.Fog.DebugFogDisabled || floor.Fog.IsVisible(new DungeonTraversalPosition(edge.First)) || floor.Fog.IsVisible(new DungeonTraversalPosition(edge.Second))))
                    DrawEdge(edge.CellEdge, new Color(.85f, .55f, .25f));
            foreach (var edge in floor.Fog.ClosedGates)
                if (edge.Level == level && !floor.Fog.DebugFogDisabled && !floor.Fog.IsVisible(new DungeonTraversalPosition(edge.First)) && !floor.Fog.IsVisible(new DungeonTraversalPosition(edge.Second)))
                    DrawEdge(edge.CellEdge, new Color(.85f, .55f, .25f));
            if (level == 0)
                foreach (var obj in floor.Objects)
                {
                    // Knowledge queries can scan an obstacle footprint. A prop
                    // outside the raster cannot affect even its padded pixels.
                    if (!RasterIntersects(ObjectRasterBounds(obj))) continue;
                    MinimapObjectKnowledgeQueryCount++;
                    if (obj is not DoorDungeonObjectInstance door)
                    {
                        if (CanShowObject(floor, obj, level))
                        {
                            var color = obj.ObjectType == DungeonObjectType.Obstacle ? obstacleColor
                                : obj.ObjectType == DungeonObjectType.Chest ? chestColor : interactionColor;
                            if (obj is ObstacleDungeonObjectInstance obstacle) DrawObstacle(obstacle, color);
                            else Dot(terrain, obj.Position, color, 4);
                        }
                        continue;
                    }
                    // Remembered state is authoritative outside current sight (including unseen changes).
                    var seen = Godmist.Systems.Dungeons.DungeonFogSystem.IsObjectVisible(floor, door);
                    floor.Fog.Objects.TryGetValue(door.InstanceId, out var memory);
                    if (!seen && memory == null && !floor.Fog.DebugFogDisabled) continue;
                    var open = seen || floor.Fog.DebugFogDisabled ? door.IsOpen : memory.State;
                    DrawEdge(door.Edge, open ? new Color(.45f, .7f, .45f) : new Color(.85f, .55f, .25f));
                }
            foreach (var body in floor.EnemyBodies)
                if (TryGetBodyMarker(floor, body, level, out var cell))
                {
                    var point=RasterPoint(cell);int x=Mathf.RoundToInt(point.x),y=Mathf.RoundToInt(point.y);
                    for (var offset = -2; offset <= 2; offset++)
                    {
                        var markerColor = body.IsEmpty ? bodyColor : UnlootedBodyColor;
                        Fill(terrain, x + offset, y + offset, 1, 1, markerColor);
                        Fill(terrain, x + offset, y - offset, 1, 1, markerColor);
                    }
                }
        }
        public static bool TryGetBodyMarker(DungeonFloorInstance floor, DungeonEnemyBodyInstance body, int level, out Vector2Int cell)
        {
            cell = default;
            if (floor?.Map == null || body == null) return false;
            var node = floor.TryGetActorPosition("body:" + body.InstanceId, out var stored) ? stored
                : new DungeonTraversalPosition(new DungeonSurfacePosition(body.Position));
            if (!CanShowActor(floor, node, level, false) || !floor.Map.TryGetTraversalCell(node, out cell)) return false;
            return floor.Fog.DebugFogDisabled || floor.Fog.IsVisible(node)
                || (floor.Fog.Bodies.Contains(body.InstanceId) && floor.Fog.Get(node) != DungeonKnowledge.Unexplored);
        }
        public static bool CanShowObject(DungeonFloorInstance floor, DungeonObjectInstance obj, int level)
        {
            // Current object instances live on ground level; do not project them onto raised surfaces.
            if (floor == null || obj == null || level != 0) return false;
            if (floor.Fog.DebugFogDisabled || Godmist.Systems.Dungeons.DungeonFogSystem.IsObjectVisible(floor, obj)) return obj.IsActive;
            return floor.Fog.Objects.TryGetValue(obj.InstanceId, out var known) && known.Active;
        }
        private void DrawObstacle(ObstacleDungeonObjectInstance obstacle, Color color)
        {
            var bounds = ObjectRasterBounds(obstacle);
            Fill(RasterPixels, bounds.X, bounds.Y, bounds.Width, bounds.Height, color);
        }

        private (int X, int Y, int Width, int Height) ObjectRasterBounds(DungeonObjectInstance obj)
        {
            if (obj is DoorDungeonObjectInstance door) return EdgeRasterBounds(door.Edge);
            var point=RasterPoint(obj.Position);int x=Mathf.RoundToInt(point.x),y=Mathf.RoundToInt(point.y);
            if (obj is ObstacleDungeonObjectInstance obstacle)
            {
                var size = obstacle.OccupiedSizeInCells;
                // The authored footprint is a rectangle anchored at its south
                // cell; keep the original inset around the whole footprint.
                int inset=IsOverviewOpen?Mathf.Max(1,Mathf.RoundToInt(RasterScale*.375f)):3;
                return (x - inset, y - inset, Mathf.RoundToInt((size.x - 1) * RasterScale)+inset*2, Mathf.RoundToInt((size.y - 1) * RasterScale)+inset*2);
            }
            return (x - 2, y - 2, 4, 4);
        }

        private bool RasterIntersects((int X, int Y, int Width, int Height) rect)
            => rect.Width > 0 && rect.Height > 0 && rect.X < RasterWidth && rect.Y < RasterHeight
                && rect.X + rect.Width > 0 && rect.Y + rect.Height > 0;

        private (int X, int Y, int Width, int Height) EdgeRasterBounds(DungeonCellEdge edge)
        {
            var delta = edge.SecondCell - edge.FirstCell;
            var p=RasterPoint((Vector2)edge.FirstCell+(Vector2)delta*.5f);
            int x=Mathf.RoundToInt(p.x),y=Mathf.RoundToInt(p.y),length=IsOverviewOpen?Mathf.Max(2,Mathf.RoundToInt(RasterScale*.625f)):5;
            return (x - (delta.x == 0 ? length/2 : 1), y - (delta.y == 0 ? length/2 : 1), delta.x == 0 ? length : 2, delta.y == 0 ? length : 2);
        }
        private void DrawEdge(DungeonCellEdge edge, Color color)
        {
            var rect = EdgeRasterBounds(edge);
            Fill(RasterPixels, rect.X, rect.Y, rect.Width, rect.Height, color);
        }
        private void DrawStair(DungeonSurfacePosition surface)
        {
            if (surface.Level == level && TerrainKnowledge(floor, surface) != DungeonKnowledge.Unexplored)
                Dot(RasterPixels, surface.Cell, new Color(.8f, .7f, .4f), 5);
        }
        private void Dot(Color32[] target, Vector2Int cell, Color color, int size)
        {
            var p=RasterPoint(cell);Fill(target,Mathf.RoundToInt(p.x)-size/2,Mathf.RoundToInt(p.y)-size/2,size,size,color);
        }
        private void Fill(Color32[] target, int x, int y, int width, int height, Color32 color)
        {
            var first = Mathf.Max(0, x); var count = Mathf.Min(RasterWidth, x + width) - first;
            if (count <= 0) return;
            for (var row = Mathf.Max(0, y); row < Mathf.Min(RasterHeight, y + height); row++)
                Array.Fill(target, color, row * RasterWidth + first, count);
        }
        private void OnDestroy()
        {
            Canvas.preWillRenderCanvases -= RefreshProjection;
            SetOverviewOpen(false);
            if(party!=null && party.DungeonMinimap==this)party.BindDungeonMinimap(null);
            if (toggle != null) toggle.onClick.RemoveListener(Toggle);
            if (image != null) image.texture = null;
            DestroyTexture(texture);DestroyTexture(overviewTexture);
        }
    }
}
