using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Views.UI.Workspace;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Godmist.Views.UI
{
    /// <summary>A presentation of the real downed counter; never advances combat time.</summary>
    [DefaultExecutionOrder(11002)]
    public sealed class CombatDownedIndicatorView : MonoBehaviour
    {
        public const int SegmentCount = 3;
        public const float WorldPixelsPerUnit = 96f;
        private readonly Image[] fills = new Image[SegmentCount];
        private BattleUnit unit;
        private SpriteRenderer body;
        private Canvas worldCanvas;
        private RectTransform content;
        private Image skull;
        private bool portraitPresentation;
        private RectTransform portraitRect;
        private Image readabilityMatte;
        private Image barFrame;
        private Vector2 portraitPanelSize;
        private CombatSimpleTooltipTrigger tooltip;
        private int displayedTurns = -1;
        private readonly Dictionary<Sprite, Bounds> poseBounds = new();
        private readonly List<Vector2> shapePoints = new();
        private Color warning = new(.82f, .62f, .22f);
        private Color danger = new(.78f, .20f, .18f);
        private static readonly Color Spent = new(.12f, .12f, .14f, 1);
        public int RemainingTurns => Mathf.Clamp(unit?.DownedTurnsRemaining ?? 0, 0, SegmentCount);
        public bool IsVisible => content != null && content.gameObject.activeInHierarchy;
        public static string TooltipBody(int turns) => $"Pozostałe tury: {Mathf.Clamp(turns,0,SegmentCount)}/{SegmentCount}.";
        public bool ContainsScreenPoint(Vector2 point, Camera camera) => IsVisible
            && RectTransformUtility.RectangleContainsScreenPoint(content, point, camera);

        public static bool ShouldShow(BattleUnit target) => target?.IsPlayerSide == true
            && target.Character?.LifeState == CharacterLifeState.Downed && target.DownedTurnsRemaining > 0;

        public static void UpdateWorld(ref CombatDownedIndicatorView indicator, SpriteRenderer renderer,
            BattleUnit target, UIWorkspaceStylePalette palette)
        {
            if (indicator == null && ShouldShow(target) && renderer != null)
            {
                indicator = Create(renderer.transform.parent ?? renderer.transform, palette);
                indicator.body = renderer;
                indicator.worldCanvas = indicator.gameObject.AddComponent<Canvas>();
                indicator.worldCanvas.renderMode = RenderMode.WorldSpace;
                indicator.worldCanvas.overrideSorting = true;
                indicator.worldCanvas.sortingOrder = 32000;
                indicator.transform.localScale = Vector3.one / WorldPixelsPerUnit;
            }
            indicator?.Bind(target);
        }

        public static void UpdatePortrait(ref CombatDownedIndicatorView indicator, Image portrait,
            BattleUnit target, UIWorkspaceStylePalette palette)
        {
            if (indicator == null && ShouldShow(target) && portrait != null)
            {
                indicator = Create(portrait.transform, palette, true);
                indicator.portraitRect = portrait.rectTransform;
                var rect = (RectTransform)indicator.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
                rect.pivot = new Vector2(.5f, 0);
                rect.anchoredPosition = new Vector2(0, 4);
                // The workspace row owns both portrait and countdown hover.
                // A second child source competed with its live rich-unit card.
                if (portrait.GetComponentInParent<CharacterInfoPartyPortraitView>() == null)
                    indicator.tooltip = indicator.content.gameObject.AddComponent<CombatSimpleTooltipTrigger>();
                indicator.content.GetComponent<Image>().raycastTarget = true;
            }
            indicator?.Bind(target);
        }

        private static CombatDownedIndicatorView Create(Transform parent, UIWorkspaceStylePalette palette, bool portrait = false)
        {
            var node = new GameObject("DownedCountdown", typeof(RectTransform));
            node.layer = parent.gameObject.layer;
            node.transform.SetParent(parent, false);
            var rect = (RectTransform)node.transform;
            rect.sizeDelta = portrait ? new Vector2(104, 60) : new Vector2(64, 38);
            rect.pivot = new Vector2(.5f, 0);
            node.AddComponent<LayoutElement>().ignoreLayout = true;
            var view = node.AddComponent<CombatDownedIndicatorView>();
            view.Build(palette, portrait);
            return view;
        }

        private void Build(UIWorkspaceStylePalette palette, bool portrait)
        {
            portraitPresentation = portrait;
            if (palette != null)
            {
                warning = palette.GetAccentColor(UIWorkspaceAccentStyle.Warning);
                danger = palette.GetAccentColor(UIWorkspaceAccentStyle.Negative);
            }
            // A small opaque backing keeps the mark readable over grass and portrait detail.
            var panelSize = portrait ? new Vector2(104, 60) : new Vector2(64, 38);
            var backing = MakeImage("Countdown", transform, panelSize, new Vector2(0, panelSize.y * .5f));
            content = backing.rectTransform;
            content.anchorMin = content.anchorMax = new Vector2(.5f, 0);
            backing.color = new Color(.025f, .025f, .03f, .84f);
            backing.sprite = palette?.GetArtwork("tooltip_frame");
            if (backing.sprite != null) { backing.type = Image.Type.Sliced; backing.pixelsPerUnitMultiplier = 3; backing.color = Color.white; }

            {
                // The skin's frame has a transparent centre. Give the countdown its
                // own dark surface so ivory bone and spent segments cannot blend into clothing.
                readabilityMatte = MakeImage("ReadabilityMatte", content, panelSize - Vector2.one * 8, Vector2.zero);
                var surface = palette != null ? palette.GetSurfaceColor(UIWorkspaceSurfaceStyle.Tooltip) : new Color(.04f, .035f, .045f);
                surface.a = 1;
                readabilityMatte.color = surface;
            }
            skull = MakeImage("Skull", content, portrait ? new Vector2(40, 40) : new Vector2(24, 24),
                portrait ? new Vector2(0, 8) : new Vector2(0, 6));
            skull.sprite = palette?.GetArtwork("status_downed_skull");
            skull.preserveAspect = true;
            skull.enabled = skull.sprite != null;

            var frame = MakeImage("GodmistBarFrame", content, portrait ? new Vector2(96, 16) : new Vector2(60, 12),
                portrait ? new Vector2(0, -19) : new Vector2(0, -11));
            barFrame = frame;
            frame.sprite = palette?.GetArtwork("slider_track");
            frame.type = frame.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            frame.pixelsPerUnitMultiplier = 2;
            frame.color = frame.sprite != null ? Color.white : new Color(.46f, .36f, .19f);
            for (var i = 0; i < SegmentCount; i++)
            {
                fills[i] = MakeImage("Turn" + (i + 1), frame.transform, portrait ? new Vector2(26, 8) : new Vector2(14, 6),
                    new Vector2((i - 1) * (portrait ? 29 : 17), 0));
                fills[i].sprite = palette?.GetArtwork("fill_runtime");
            }
            content.gameObject.SetActive(false);
        }

        private void FitPortrait()
        {
            if (portraitRect == null) return;
            var size = new Vector2(Mathf.Clamp(portraitRect.rect.width - 8, 32, 104),
                Mathf.Clamp(portraitRect.rect.height - 8, 32, 60));
            if (size == portraitPanelSize) return;
            portraitPanelSize = size;
            ((RectTransform)transform).sizeDelta = size;
            content.sizeDelta = size;
            content.anchoredPosition = new Vector2(0, size.y * .5f);
            readabilityMatte.rectTransform.sizeDelta = size - Vector2.one * 8;
            var skullSize = Mathf.Min(40, size.y - 20, size.x - 12);
            skull.rectTransform.sizeDelta = Vector2.one * skullSize;
            skull.rectTransform.anchoredPosition = new Vector2(0, 8);
            barFrame.rectTransform.sizeDelta = new Vector2(size.x - 8, 16);
            barFrame.rectTransform.anchoredPosition = new Vector2(0, 11 - size.y * .5f);
            var segmentWidth = (size.x - 26) / SegmentCount;
            for (var i = 0; i < SegmentCount; i++)
            {
                fills[i].rectTransform.sizeDelta = new Vector2(segmentWidth, 8);
                fills[i].rectTransform.anchoredPosition = new Vector2((i - 1) * (segmentWidth + 3), 0);
            }
        }

        private static Image MakeImage(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            node.layer = parent.gameObject.layer;
            node.transform.SetParent(parent, false);
            var image = node.GetComponent<Image>();
            image.raycastTarget = false;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, .5f);
            image.rectTransform.sizeDelta = size;
            image.rectTransform.anchoredPosition = position;
            return image;
        }

        public void Bind(BattleUnit target)
        {
            unit = target;
            Refresh();
        }

        private void LateUpdate()
        {
            Refresh();
            if (!IsVisible || worldCanvas == null || body == null) return;
            var visibleBounds = VisiblePoseBounds();
            transform.position = new Vector3(visibleBounds.center.x,
                visibleBounds.max.y + 1f/32, body.transform.position.z);
            transform.rotation = Quaternion.identity;
            var parentScale = transform.parent.lossyScale;
            transform.localScale = new Vector3(1f / (WorldPixelsPerUnit * Mathf.Max(.001f, Mathf.Abs(parentScale.x))),
                1f / (WorldPixelsPerUnit * Mathf.Max(.001f, Mathf.Abs(parentScale.y))), 1);
            worldCanvas.sortingLayerID = body.sortingLayerID;
        }

        private Bounds VisiblePoseBounds()
        {
            var sprite = body.sprite;
            if (sprite == null) return body.bounds;
            if (!poseBounds.TryGetValue(sprite, out var local))
            {
                if (!Godmist.Views.Characters.CombatPoseResolver.TryGetVisibleBounds(sprite,out local))
                {
                    local = sprite.bounds;
                    var found = false;
                    // Imported alpha outlines cover ordinary sprites without
                    // readable textures. Runtime poses use the catalog above.
                    for (var shape = 0; shape < sprite.GetPhysicsShapeCount(); shape++)
                    {
                        shapePoints.Clear();
                        sprite.GetPhysicsShape(shape, shapePoints);
                        foreach (var point in shapePoints)
                        {
                            if (!found) { local = new Bounds(point,Vector3.zero); found = true; }
                            else local.Encapsulate(point);
                        }
                    }
                }
                poseBounds.Add(sprite,local);
            }
            var result = new Bounds();
            for (var corner = 0; corner < 4; corner++)
            {
                var point = new Vector3((corner&1)==0 ? local.min.x : local.max.x,
                    (corner&2)==0 ? local.min.y : local.max.y, 0);
                if (body.flipX) point.x = -point.x;
                if (body.flipY) point.y = -point.y;
                point = body.transform.TransformPoint(point);
                if (corner == 0) result = new Bounds(point,Vector3.zero); else result.Encapsulate(point);
            }
            return result;
        }

        private void Refresh()
        {
            if (content == null) return;
            var visible = ShouldShow(unit) && (worldCanvas == null || body != null && body.enabled && !body.forceRenderingOff && body.gameObject.activeInHierarchy);
            if (content.gameObject.activeSelf != visible) content.gameObject.SetActive(visible);
            if (!visible) return;
            if (portraitPresentation) FitPortrait();
            var turns = RemainingTurns;
            if (displayedTurns == turns) return;
            displayedTurns = turns;
            var tint = turns == 1 ? danger : warning;
            for (var i = 0; i < SegmentCount; i++) fills[i].color = i < turns ? tint : Spent;
            skull.color = turns == 1 && !portraitPresentation ? new Color(1, .7f, .6f) : Color.white;
            tooltip?.Initialize(null, "Powalony", TooltipBody(turns));
        }
    }

}
