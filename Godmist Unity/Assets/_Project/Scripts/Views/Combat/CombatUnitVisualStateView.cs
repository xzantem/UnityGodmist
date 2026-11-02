using System.Collections.Generic;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using UnityEngine;

namespace Godmist.Views.Combat
{
    [DefaultExecutionOrder(11001)]
    public class CombatUnitVisualStateView : MonoBehaviour
    {
        private static Color activeTurnColor => HighlightStyle.Selection;
        private static Color inspectColor => HighlightStyle.Hover;
        private static Color targetColor => HighlightStyle.Hostile;
        private float outlineWidth = 1f / 32f;
        private bool customOutlineWidth;
        private SpriteRenderer[] renderers;
        private Color[] defaults;
        private SpriteRenderer[] outlineRenderers;
        private Material outlineMaterial;
        private MaterialPropertyBlock[] outlineProperties;
        private Mesh enemyFootMesh;
        private MeshRenderer enemyFootMarker;
        private Material enemyFootMaterial;
        private BattleUnit enemyFootUnit;
        private bool hasActiveTurn;
        private Mesh activeFootMesh;
        private MeshRenderer activeFootMarker;
        private Material activeFootMaterial;
        private static readonly Vector2[] OutlineOffsets =
        {
            Vector2.left, Vector2.right, Vector2.up, Vector2.down,
            new Vector2(-1f, -1f), new Vector2(-1f, 1f),
            new Vector2(1f, -1f), new Vector2(1f, 1f)
        };

        private void Awake()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            defaults = new Color[renderers.Length];
            for (var i = 0; i < renderers.Length; i++) defaults[i] = renderers[i].color;
            if (renderers.Length == 0) return;

            var outlineShader = CombatOutlineShader.Load();
            if (outlineShader == null)
            {
                Debug.LogError("Combat visual-state outline shader was not found.", this);
                return;
            }

            outlineMaterial = new Material(outlineShader);
            outlineRenderers = new SpriteRenderer[OutlineOffsets.Length];
            outlineProperties = new MaterialPropertyBlock[OutlineOffsets.Length];
            for (var index = 0; index < OutlineOffsets.Length; index++)
            {
                var outline = new GameObject("Visual State Outline", typeof(SpriteRenderer));
                outline.layer = renderers[0].gameObject.layer;
                outline.transform.SetParent(renderers[0].transform, false);
                outline.transform.localPosition = OutlineOffsets[index] * outlineWidth;
                var renderer = outline.GetComponent<SpriteRenderer>();
                renderer.sortingLayerID = renderers[0].sortingLayerID;
                renderer.sortingOrder = renderers[0].sortingOrder - 1;
                renderer.sharedMaterial = outlineMaterial;
                renderer.enabled = false;
                outlineRenderers[index] = renderer;
                outlineProperties[index] = new MaterialPropertyBlock();
            }
        }

        public void SetOutlineWidth(float width)
        {
            customOutlineWidth = true;
            ApplyOutlineWidth(width);
        }

        private void ApplyOutlineWidth(float width)
        {
            width = Mathf.Max(0.001f, width);
            if (Mathf.Approximately(outlineWidth, width)) return;
            outlineWidth = width;
            if (outlineRenderers == null) return;
            for (var index = 0; index < outlineRenderers.Length; index++)
                if (outlineRenderers[index] != null)
                    outlineRenderers[index].transform.localPosition = OutlineOffsets[index] * outlineWidth;
        }

        private void OnDestroy()
        {
            if (activeFootMesh != null) Release(activeFootMesh);
            if (activeFootMaterial != null) Release(activeFootMaterial);
            if (enemyFootMesh != null) Release(enemyFootMesh);
            if (enemyFootMaterial != null) Release(enemyFootMaterial);
            if (outlineMaterial != null)
            {
                if (Application.isPlaying) Destroy(outlineMaterial);
                else DestroyImmediate(outlineMaterial);
            }
        }

        public void ClearHighlight()
        {
            hasActiveTurn = false;
            if (activeFootMarker != null) activeFootMarker.enabled = false;
            enemyFootUnit = null;
            if (enemyFootMarker != null) enemyFootMarker.enabled = false;
            if (outlineRenderers == null) return;
            foreach (var renderer in outlineRenderers)
                if (renderer != null) renderer.enabled = false;
        }

        private void OnDisable() => ClearHighlight();

        private static bool CanRenderFeedback(SpriteRenderer source) => source != null
            && source.enabled && !source.forceRenderingOff && source.color.a > .01f;

        private void LateUpdate()
        {
            // Awareness/fog and depth sorting settle in the actor's LateUpdate.
            // Recheck afterward so a marker cannot linger for a frame in fog.
            if (renderers != null)
            {
                RefreshEnemyFootMarker(enemyFootUnit);
                RefreshActiveFootMarker(enemyFootUnit);
            }
            if (outlineRenderers == null || renderers == null || renderers.Length == 0)
                return;
            var source = renderers[0];
            if (source == null) return;
            for (var index = 0; index < outlineRenderers.Length; index++)
            {
                var outline = outlineRenderers[index];
                if (outline == null) continue;
                if (StatusEffectSystem.IsInvisibleToPlayer(enemyFootUnit) || !CanRenderFeedback(source))
                    outline.enabled = false;
                outline.sortingLayerID = source.sortingLayerID;
                outline.sortingOrder = source.sortingOrder - 1;
            }
        }

        public void Refresh(BattleUnit unit, bool isActive, bool isHovered, bool isTargeting, bool preserveBodyColors = false, Color? targetAccent = null)
        {
            if (!isActiveAndEnabled) { ClearHighlight(); return; }
            if (renderers == null) Awake();
            if (!customOutlineWidth && renderers.Length > 0 && renderers[0].sprite != null)
                ApplyOutlineWidth(1f / Mathf.Max(1f, renderers[0].sprite.pixelsPerUnit));
            enemyFootUnit = unit;
            RefreshEnemyFootMarker(unit);
            // A downed unit can remain in the view while its persistent downed pose
            // is playing, and a turn transition can briefly leave ActiveUnit stale.
            // Neither case is an active-turn visual state. Hover/target feedback is
            // still allowed to identify a downed revive target.
            hasActiveTurn = isActive
                && !StatusEffectSystem.IsInvisibleToPlayer(unit)
                && unit?.Character?.LifeState == Godmist.Core.Enums.CharacterLifeState.Alive;
            RefreshActiveFootMarker(unit);
            var accent = isHovered ? (isTargeting ? targetAccent ?? targetColor : inspectColor) : Color.clear;
            if (StatusEffectSystem.IsInvisibleToPlayer(unit)) accent = Color.clear;
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                if (!preserveBodyColors) renderers[i].color = defaults[i];
            }
            if (outlineRenderers == null) return;
            // Hover and target colours stay readable; control effects have their own icons.
            var outlineColor = accent;
            for (var index = 0; index < outlineRenderers.Length; index++)
            {
                var outlineRenderer = outlineRenderers[index];
                outlineRenderer.sprite = renderers[0].sprite;
                outlineRenderer.flipX = renderers[0].flipX;
                outlineRenderer.flipY = renderers[0].flipY;
                outlineRenderer.sortingLayerID = renderers[0].sortingLayerID;
                outlineRenderer.sortingOrder = renderers[0].sortingOrder - 1;
                outlineRenderer.enabled = accent.a > 0f && CanRenderFeedback(renderers[0]);
                if (!outlineRenderer.enabled) continue;

                var properties = outlineProperties[index];
                properties.SetColor("_OutlineColor", outlineColor);
                outlineRenderer.SetPropertyBlock(properties);
            }
        }

        private static void Release(Object resource)
        {
            if (Application.isPlaying) Destroy(resource); else DestroyImmediate(resource);
        }

        private void RefreshActiveFootMarker(BattleUnit unit)
        {
            var source = renderers.Length > 0 ? renderers[0] : null;
            var show = hasActiveTurn && unit?.Character?.LifeState == Godmist.Core.Enums.CharacterLifeState.Alive
                && !StatusEffectSystem.IsInvisibleToPlayer(unit)
                && CanRenderFeedback(source);
            if (!show) { if (activeFootMarker != null) activeFootMarker.enabled = false; return; }
            if (activeFootMarker == null)
                activeFootMarker = CreateFootMarker("Active turn foot ring", true, out activeFootMesh, out activeFootMaterial);
            activeFootMarker.sortingLayerID = source.sortingLayerID;
            activeFootMarker.sortingOrder = source.sortingOrder - 1;
            activeFootMarker.enabled = true;
        }

        private void RefreshEnemyFootMarker(BattleUnit unit)
        {
            var source = renderers.Length > 0 ? renderers[0] : null;
            var show = unit != null && !unit.IsPlayerSide
                && !StatusEffectSystem.IsInvisibleToPlayer(unit)
                && unit.Character?.LifeState == Godmist.Core.Enums.CharacterLifeState.Alive
                && CanRenderFeedback(source);
            if (!show) { if (enemyFootMarker != null) enemyFootMarker.enabled = false; return; }
            if (enemyFootMarker == null)
                enemyFootMarker = CreateFootMarker("Enemy foot arc", false, out enemyFootMesh, out enemyFootMaterial);
            enemyFootMarker.sortingLayerID = source.sortingLayerID;
            enemyFootMarker.sortingOrder = source.sortingOrder - 1;
            enemyFootMarker.forceRenderingOff = false;
            enemyFootMarker.enabled = true;
        }

        // Pixel geometry at the same 32-pixel cell scale as floor feedback. Built once,
        // with a dark keyline and restrained metal highlights; no glowing smooth ring.
        private MeshRenderer CreateFootMarker(string name, bool active, out Mesh mesh, out Material material)
        {
            var pixels = new HashSet<Vector2Int>();
            for (var y = -6; y <= 5; y++)
            for (var x = -12; x <= 12; x++)
            {
                var ax = Mathf.Abs(x); var ay = Mathf.Abs(y);
                var outer = ax <= 12 && ay <= 5 && ax + 2 * ay <= 19;
                var inner = ax <= 11 && ay <= 4 && ax + 2 * ay <= 17;
                if (active ? outer && !inner && !(y > 1 && ax < 6)
                    : y < -1 && outer && !inner)
                    pixels.Add(new Vector2Int(x, y));
            }
            if (active)
            {
                pixels.Add(new Vector2Int(0, -7)); pixels.Add(new Vector2Int(-1, -6));
                pixels.Add(new Vector2Int(0, -6)); pixels.Add(new Vector2Int(1, -6));
            }
            var shadow = new HashSet<Vector2Int>();
            foreach (var pixel in pixels)
                for (var y = -1; y <= 1; y++) for (var x = -1; x <= 1; x++)
                {
                    var neighbour = pixel + new Vector2Int(x, y);
                    if (!pixels.Contains(neighbour)) shadow.Add(neighbour);
                }
            var vertices = new List<Vector3>(); var colors = new List<Color>(); var indices = new List<int>();
            void Pixel(Vector2Int pixel, Color color)
            {
                const float size = 1f / 32f;
                var left = (pixel.x - .5f) * size; var bottom = (pixel.y - .5f) * size;
                var index = vertices.Count;
                vertices.Add(new Vector3(left, bottom)); vertices.Add(new Vector3(left, bottom + size));
                vertices.Add(new Vector3(left + size, bottom + size)); vertices.Add(new Vector3(left + size, bottom));
                for (var i = 0; i < 4; i++) colors.Add(color);
                indices.Add(index); indices.Add(index + 1); indices.Add(index + 2);
                indices.Add(index); indices.Add(index + 2); indices.Add(index + 3);
            }
            foreach (var pixel in shadow) Pixel(pixel, HighlightStyle.Ink);
            var tint = active ? HighlightStyle.Selection : HighlightStyle.Hostile;
            foreach (var pixel in pixels)
                Pixel(pixel, pixel.y <= -4 ? tint : Color.Lerp(HighlightStyle.Ink, tint, .8f));
            mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(indices, 0);
            mesh.uv = new Vector2[vertices.Count]; mesh.RecalculateBounds();
            var node = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            node.layer = gameObject.layer; node.transform.SetParent(transform, false);
            node.GetComponent<MeshFilter>().sharedMesh = mesh;
            material = new Material(Shader.Find("Sprites/Default")) { mainTexture = Texture2D.whiteTexture };
            var renderer = node.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            return renderer;
        }
    }
}
