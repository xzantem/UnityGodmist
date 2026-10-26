using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using UnityEngine;

namespace Godmist.Views.Combat
{
    [DefaultExecutionOrder(11001)]
    public class CombatUnitVisualStateView : MonoBehaviour
    {
        [SerializeField] private Color activeTurnColor = new(1f, .78f, .22f, 1f);
        [SerializeField] private Color inspectColor = new(.5f, .86f, 1f, 1f);
        [SerializeField] private Color targetColor = new(1f, .34f, .28f, 1f);
        [SerializeField, Min(0.001f)] private float outlineWidth = .06f;
        [SerializeField, Min(0f)] private float pulseSpeed = 2.5f;
        private SpriteRenderer[] renderers;
        private Color[] defaults;
        private SpriteRenderer[] outlineRenderers;
        private Material outlineMaterial;
        private MaterialPropertyBlock[] outlineProperties;
        private Mesh enemyFootMesh;
        private MeshRenderer enemyFootMarker;
        private Material enemyFootMaterial;
        private BattleUnit enemyFootUnit;
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

            var outlineShader = Shader.Find("Godmist/Combat/Solid Sprite Silhouette");
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

        public void SetPulseSpeed(float speed) => pulseSpeed = Mathf.Max(0f, speed);

        public void SetOutlineWidth(float width)
        {
            outlineWidth = Mathf.Max(0.001f, width);
            if (outlineRenderers == null) return;
            for (var index = 0; index < outlineRenderers.Length; index++)
                if (outlineRenderers[index] != null)
                    outlineRenderers[index].transform.localPosition = OutlineOffsets[index] * outlineWidth;
        }

        private void OnDestroy()
        {
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
            enemyFootUnit = null;
            if (enemyFootMarker != null) enemyFootMarker.enabled = false;
            if (outlineRenderers == null) return;
            foreach (var renderer in outlineRenderers)
                if (renderer != null) renderer.enabled = false;
        }

        private void LateUpdate()
        {
            // Awareness/fog and depth sorting settle in the actor's LateUpdate.
            // Recheck afterward so a marker cannot linger for a frame in fog.
            if (renderers != null) RefreshEnemyFootMarker(enemyFootUnit);
            if (outlineRenderers == null || renderers == null || renderers.Length == 0)
                return;
            var source = renderers[0];
            if (source == null) return;
            for (var index = 0; index < outlineRenderers.Length; index++)
            {
                var outline = outlineRenderers[index];
                if (outline == null) continue;
                if (StatusEffectSystem.IsInvisibleToPlayer(enemyFootUnit) || !source.enabled || source.forceRenderingOff)
                    outline.enabled = false;
                outline.sortingLayerID = source.sortingLayerID;
                outline.sortingOrder = source.sortingOrder - 1;
            }
        }

        public void Refresh(BattleUnit unit, bool isActive, bool isHovered, bool isTargeting, bool preserveBodyColors = false)
        {
            if (renderers == null) Awake();
            enemyFootUnit = unit;
            RefreshEnemyFootMarker(unit);
            // A downed unit can remain in the view while its persistent downed pose
            // is playing, and a turn transition can briefly leave ActiveUnit stale.
            // Neither case is an active-turn visual state. Hover/target feedback is
            // still allowed to identify a downed revive target.
            var hasActiveTurn = isActive
                && !StatusEffectSystem.IsInvisibleToPlayer(unit)
                && unit?.Character?.LifeState == Godmist.Core.Enums.CharacterLifeState.Alive;
            var accent = isHovered ? (isTargeting ? targetColor : inspectColor) : hasActiveTurn ? activeTurnColor : Color.clear;
            if (StatusEffectSystem.IsInvisibleToPlayer(unit)) accent = Color.clear;
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                if (!preserveBodyColors) renderers[i].color = defaults[i];
            }
            if (outlineRenderers == null) return;
            // The colour deliberately reaches the unmodified accent.  The easing keeps it
            // there longer than at the black end, so the unit reads as gold/blue/red rather
            // than as a permanently dim outline.
            var rawPulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * pulseSpeed);
            var pulse = Mathf.Pow(rawPulse, .45f);
            var outlineColor = Color.Lerp(Color.black, accent, pulse);
            if (unit != null && StatusEffectSystem.IsHardCrowdControlled(unit))
                outlineColor = Color.Lerp(Color.black, outlineColor, .55f);
            for (var index = 0; index < outlineRenderers.Length; index++)
            {
                var outlineRenderer = outlineRenderers[index];
                outlineRenderer.sprite = renderers[0].sprite;
                outlineRenderer.flipX = renderers[0].flipX;
                outlineRenderer.flipY = renderers[0].flipY;
                outlineRenderer.sortingLayerID = renderers[0].sortingLayerID;
                outlineRenderer.sortingOrder = renderers[0].sortingOrder - 1;
                outlineRenderer.enabled = accent.a > 0f && renderers[0].enabled && !renderers[0].forceRenderingOff;
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

        private void RefreshEnemyFootMarker(BattleUnit unit)
        {
            var source = renderers.Length > 0 ? renderers[0] : null;
            var show = unit != null && !unit.IsPlayerSide
                && !StatusEffectSystem.IsInvisibleToPlayer(unit)
                && unit.Character?.LifeState == Godmist.Core.Enums.CharacterLifeState.Alive
                && source != null && source.enabled && !source.forceRenderingOff && source.color.a > .01f;
            if (!show) { if (enemyFootMarker != null) enemyFootMarker.enabled = false; return; }
            if (enemyFootMarker == null)
            {
                // A small, static lower arc at the feet; no full ring, pulse,
                // extra texture or per-frame mesh rebuild.
                var node = new GameObject("Enemy foot arc", typeof(MeshFilter), typeof(MeshRenderer));
                node.layer = gameObject.layer;
                node.transform.SetParent(transform, false);
                const int segments = 12;
                var vertices = new Vector3[(segments + 1) * 2];
                var colors = new Color[vertices.Length];
                var indices = new int[segments * 6];
                for (var i = 0; i <= segments; i++)
                {
                    var angle = Mathf.Lerp(200f, 340f, i / (float)segments) * Mathf.Deg2Rad;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    vertices[i * 2] = Vector2.Scale(direction, new Vector2(.34f, .14f));
                    vertices[i * 2 + 1] = Vector2.Scale(direction, new Vector2(.31f, .11f));
                    colors[i * 2] = colors[i * 2 + 1] = new Color(.70f, .21f, .17f, .7f);
                    if (i == segments) continue;
                    var t = i * 6; var v = i * 2;
                    indices[t] = v; indices[t + 1] = v + 1; indices[t + 2] = v + 2;
                    indices[t + 3] = v + 2; indices[t + 4] = v + 1; indices[t + 5] = v + 3;
                }
                enemyFootMesh = new Mesh { name = "Enemy foot arc" };
                enemyFootMesh.vertices = vertices; enemyFootMesh.colors = colors;
                enemyFootMesh.uv = new Vector2[vertices.Length];
                enemyFootMesh.triangles = indices; enemyFootMesh.RecalculateBounds();
                node.GetComponent<MeshFilter>().sharedMesh = enemyFootMesh;
                enemyFootMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = Texture2D.whiteTexture };
                enemyFootMarker = node.GetComponent<MeshRenderer>();
                enemyFootMarker.sharedMaterial = enemyFootMaterial;
                enemyFootMarker.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                enemyFootMarker.receiveShadows = false;
            }
            enemyFootMarker.sortingLayerID = source.sortingLayerID;
            enemyFootMarker.sortingOrder = source.sortingOrder - 1;
            enemyFootMarker.forceRenderingOff = false;
            enemyFootMarker.enabled = true;
        }
    }
}
