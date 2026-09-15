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
    public sealed class DungeonMinimapView : MonoBehaviour
    {
        [SerializeField] private RawImage image;
        [SerializeField] private GameObject content;
        [SerializeField] private Button toggle;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text toggleLabel;
        [SerializeField] private DungeonMinimapMarkers markers;
        [SerializeField] private Color bodyColor = new(.8f, .8f, .8f);
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
        private static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        public void Bind(DungeonFloorInstance nextFloor, DungeonFloorMapView nextMap,
            ExplorationPartyController nextParty, DungeonEncounterSpawner nextEncounters)
        {
            floor = nextFloor; map = nextMap; party = nextParty; encounters = nextEncounters;
            walls.Clear();
            if (floor?.Map != null) walls.UnionWith(floor.Map.SurfaceWallEdges);
            dirty = true; nextRefresh = 0;
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
            if (content == null) return;
            content.SetActive(!content.activeSelf);
            dirty = true; nextRefresh = 0;
            RefreshTitle();
        }
        private void RefreshTitle()
        {
            if (title != null) title.text = $"Mapa - poziom {level}";
            if (toggleLabel != null) toggleLabel.text = content != null && content.activeSelf ? "-" : "+";
        }
        private void LateUpdate()
        {
            if (floor?.Map == null || map == null || party == null || texture == null
                || content == null || !content.activeSelf) return;
            var leader = party.ActiveLeaderTransform;
            if (leader == null) return;
            var continuousCenter = ContinuousCell(leader.position);
            var nextCenter = map.WorldToCell(leader.position);
            var nextLevel = map.ViewedLevel;
            // Padding permits sub-cell UV scrolling every frame without rerasterizing terrain.
            var outOfPadding = Mathf.Abs(continuousCenter.x - center.x) > 1.5f || Mathf.Abs(continuousCenter.y - center.y) > 1.5f;
            if (dirty || nextLevel != level || outOfPadding || Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + .1f;
                if (dirty || nextCenter != center || nextLevel != level || fogRevision != floor.Fog.Revision || debugRevision != floor.Fog.DebugRevision)
                {
                    center = nextCenter; level = nextLevel;
                    fogRevision = floor.Fog.Revision; debugRevision = floor.Fog.DebugRevision;
                    DrawTerrain(); texture.SetPixels32(terrain); texture.Apply(false, false);
                    dirty = false; RefreshTitle();
                }
            }
            if (image != null) image.uvRect = Viewport(continuousCenter, center);
            if (markers == null) return;
            markers.ClearMarkers();
            foreach (var member in party.ActiveMemberViews) DrawActor(member, member.transform == leader ? leaderColor : partyColor, false, continuousCenter);
            if (encounters != null)
                foreach (var trigger in encounters.Triggers)
                    foreach (var enemy in trigger.EnemyViews) DrawActor(enemy, enemyColor, true, continuousCenter);
            markers.Refresh();
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
            var relative = (Vector2)world - map.CellToWorldCenter(cell);
            var origin = map.CellToWorldCenter(Vector2Int.zero);
            var dx = map.CellToWorldCenter(Vector2Int.right) - origin;
            var dy = map.CellToWorldCenter(Vector2Int.up) - origin;
            return cell + new Vector2(dx.sqrMagnitude > .0001f ? Vector2.Dot(relative, dx) / dx.sqrMagnitude : 0,
                dy.sqrMagnitude > .0001f ? Vector2.Dot(relative, dy) / dy.sqrMagnitude : 0);
        }

        public static bool CanShowActor(DungeonFloorInstance floor, DungeonTraversalPosition node, int level, bool enemy)
        {
            if (floor?.Map == null || !floor.Map.TryGetTraversalCell(node, out _)) return false;
            var onLevel = node.IsOnStairs ? node.Stair.First.Level == level || node.Stair.Second.Level == level : node.Surface.Level == level;
            return onLevel && (!enemy || floor.Fog.IsVisible(node));
        }
        private void DrawActor(ExplorationCharacterView actor, Color color, bool enemy, Vector2 continuousCenter)
        {
            if (actor == null || actor.RuntimeCharacter?.LifeState != CharacterLifeState.Alive
                || !CanShowActor(floor, actor.DungeonPosition, level, enemy)) return;
            markers.AddMarker((ContinuousCell(actor.transform.position) - continuousCenter) * Scale, color);
        }
        public static DungeonKnowledge TerrainKnowledge(DungeonFloorInstance floor, DungeonSurfacePosition surface)
        {
            if (floor?.Map == null || !floor.Map.IsWalkable(surface)) return DungeonKnowledge.Unexplored;
            return floor.Fog.DebugFogDisabled ? DungeonKnowledge.Visible : floor.Fog.Get(new DungeonTraversalPosition(surface));
        }
        private void DrawTerrain()
        {
            Array.Fill(terrain, (Color32)Color.black);
            for (var y = -16; y <= 16; y++) for (var x = -16; x <= 16; x++)
            {
                var cell = center + new Vector2Int(x, y);
                var surface = new DungeonSurfacePosition(cell, level);
                var knowledge = TerrainKnowledge(floor, surface);
                if (knowledge == DungeonKnowledge.Unexplored) continue;
                var px = Size / 2 + x * Scale - Scale / 2;
                var py = Size / 2 + y * Scale - Scale / 2;
                Fill(terrain, px, py, Scale, Scale, knowledge == DungeonKnowledge.Visible ? visibleColor : rememberedColor);
                foreach (var dir in Directions)
                {
                    var neighbour = new DungeonSurfacePosition(cell + dir, level);
                    if (floor.Map.IsWalkable(neighbour) && !walls.Contains(new DungeonSurfaceEdge(surface, neighbour))) continue;
                    Fill(terrain, px + (dir.x > 0 ? Scale - 1 : 0), py + (dir.y > 0 ? Scale - 1 : 0), dir.x == 0 ? Scale : 1, dir.y == 0 ? Scale : 1, Color.black);
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
                    if (obj is not DoorDungeonObjectInstance door)
                    {
                        if (CanShowObject(floor, obj, level))
                        {
                            var color = obj.ObjectType == DungeonObjectType.Obstacle ? obstacleColor
                                : obj.ObjectType == DungeonObjectType.Chest ? chestColor : interactionColor;
                            Dot(terrain, obj.Position, color, obj.ObjectType == DungeonObjectType.Obstacle ? 6 : 4);
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
                    var x = Size / 2 + (cell.x - center.x) * Scale;
                    var y = Size / 2 + (cell.y - center.y) * Scale;
                    for (var offset = -2; offset <= 2; offset++)
                    {
                        Fill(terrain, x + offset, y + offset, 1, 1, bodyColor);
                        Fill(terrain, x + offset, y - offset, 1, 1, bodyColor);
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
        private void DrawEdge(DungeonCellEdge edge, Color color)
        {
            var delta = edge.SecondCell - edge.FirstCell;
            var x = Size / 2 + (edge.FirstCell.x - center.x) * Scale + delta.x * Scale / 2;
            var y = Size / 2 + (edge.FirstCell.y - center.y) * Scale + delta.y * Scale / 2;
            Fill(terrain, x - (delta.x == 0 ? 2 : 1), y - (delta.y == 0 ? 2 : 1), delta.x == 0 ? 5 : 2, delta.y == 0 ? 5 : 2, color);
        }
        private void DrawStair(DungeonSurfacePosition surface)
        {
            if (surface.Level == level && TerrainKnowledge(floor, surface) != DungeonKnowledge.Unexplored)
                Dot(terrain, surface.Cell, new Color(.8f, .7f, .4f), 5);
        }
        private void Dot(Color32[] target, Vector2Int cell, Color color, int size)
            => Fill(target, Size / 2 + (cell.x - center.x) * Scale - size / 2, Size / 2 + (cell.y - center.y) * Scale - size / 2, size, size, color);
        private static void Fill(Color32[] target, int x, int y, int width, int height, Color32 color)
        {
            for (var row = Mathf.Max(0, y); row < Mathf.Min(Size, y + height); row++)
                for (var col = Mathf.Max(0, x); col < Mathf.Min(Size, x + width); col++) target[row * Size + col] = color;
        }
        private void OnDestroy()
        {
            if (toggle != null) toggle.onClick.RemoveListener(Toggle);
            if (image != null) image.texture = null;
            if (texture != null) Destroy(texture);
        }
    }
}
