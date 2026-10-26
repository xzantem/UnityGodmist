using System;
using System.Linq;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Godmist.Editor
{
    public static class GalduryteSocketingUiSetup
    {
        private static UIWorkspaceStylePalette palette;
        private static Scrollbar scrollbarSource;
        [MenuItem("Godmist/Content/UI/Configure Galduryte Socketing")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var root = PrefabUtility.LoadPrefabContents(TradeUiSetup.PrefabPath);
            try
            {
                palette = root.GetComponent<UIWorkspaceStyleApplier>().Palette;
                scrollbarSource = root.GetComponentInChildren<MaterialCraftingWindowView>(true)
                    .GetComponentsInChildren<ScrollRect>(true).First(s => s.verticalScrollbar != null).verticalScrollbar;
                var view = root.GetComponentInChildren<GalduryteSocketingWindowView>(true);
                var body = Ref<GameObject>(view,"serviceRoot").transform;
                var templates=Node(body,"SocketingTemplates",typeof(LayoutElement));templates.GetComponent<LayoutElement>().ignoreLayout=true;templates.gameObject.SetActive(false);
                foreach(var field in new[]{"equipmentSlotTemplate","galduryteSlotTemplate"})
                {
                    var slot=Ref<BlacksmithCraftingSlotView>(view,field);
                    if(slot.transform.parent!=templates){slot=Object.Instantiate(slot,templates,false);slot.name=field;slot.gameObject.SetActive(false);Wire(view,field,slot);}
                    Slot(slot);
                }
                var rowSource=Ref<GalduryteSocketRowView>(view,"socketRowTemplate");
                if(rowSource.transform.parent!=templates){rowSource=Object.Instantiate(rowSource,templates,false);rowSource.name="socketRowTemplate";rowSource.gameObject.SetActive(false);Wire(view,"socketRowTemplate",rowSource);}
                Row(rowSource);
                Rect(body,Vector2.zero,Vector2.one,new Vector2(16,0),new Vector2(-16,0));body.GetComponent<HorizontalLayoutGroup>().spacing=28;
                var left=body.Find("AssemblyColumn");var right=body.Find("SummaryColumn");
                Element(left,432,-1,0,1);left.GetComponent<LayoutElement>().preferredWidth=500;Element(right,330,-1,1,1);
                left.GetComponent<VerticalLayoutGroup>().spacing=22;right.GetComponent<VerticalLayoutGroup>().spacing=6;
                Catalog(view,left.Find("BaseSection"),"Wyposażenie","equipmentGrid","equipmentScroll","equipmentCountText","equipmentEmptyText",180,3);
                Catalog(view,left.Find("GalduryteSection"),"Galduryty","galduryteGrid","galduryteScroll","galduryteCountText","galduryteEmptyText",140,2);
                var divider=Node(right,"ColumnDivider",typeof(Image),typeof(LayoutElement));divider.GetComponent<LayoutElement>().ignoreLayout=true;Rect(divider,Vector2.zero,new Vector2(0,1),new Vector2(-14,0),new Vector2(-13,0));divider.GetComponent<Image>().color=new Color32(107,109,87,255);divider.GetComponent<Image>().raycastTarget=false;
                var preview=right.Find("PreviewSection");preview.SetAsFirstSibling();Section(preview,"Podgląd",128,0);Wire(view,"previewSection",preview.gameObject);
                var layout=preview.Find("Content").GetComponent<VerticalLayoutGroup>();layout.padding=new RectOffset();layout.spacing=0;
                var result=Ref<BlacksmithCraftingSlotView>(view,"previewSlot");Slot(result);Element(result.transform,80,80,0,0);
                var header=result.transform.parent;Element(header,-1,96,1,0);header.GetComponent<HorizontalLayoutGroup>().childAlignment=TextAnchor.UpperLeft;
                var name=Ref<TextMeshProUGUI>(view,"previewNameText");Text(name,23);Element(name.transform,-1,-1,1,0);name.overflowMode=TextOverflowModes.Ellipsis;name.transform.SetAsFirstSibling();
                var infoLayout=name.transform.parent.GetComponent<VerticalLayoutGroup>();infoLayout.childControlWidth=infoLayout.childControlHeight=true;infoLayout.childForceExpandWidth=true;infoLayout.childForceExpandHeight=false;infoLayout.childAlignment=TextAnchor.UpperLeft;infoLayout.spacing=4;infoLayout.padding=new RectOffset();
                var input=name.GetComponent<TMP_InputField>();if(input!=null)input.enabled=false;
                var info=Ref<TextMeshProUGUI>(view,"previewSummaryText");Text(info,17);Element(info.transform,-1,64,1,0);info.transform.SetAsLastSibling();
                var details=right.Find("SocketsSection");details.SetSiblingIndex(1);Section(details,"",120,1);details.Find("SectionTitle").gameObject.SetActive(false);
                var host=details.Find("Content");Rect(host,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);host.Find("SocketScroll")?.gameObject.SetActive(false);
                Vertical(host,8);
                var candidate=Ref<GameObject>(view,"candidateSection")?.transform??Node(host,"CandidateSection",typeof(VerticalLayoutGroup));
                candidate.SetParent(host,false);candidate.SetAsFirstSibling();Vertical(candidate,8);Element(candidate,-1,-1,1,0);Wire(view,"candidateSection",candidate.gameObject);
                Heading(candidate,"Do osadzenia");
                var candidateRow=Ref<GalduryteSocketRowView>(view,"candidateRow");
                if(candidateRow==null){candidateRow=Object.Instantiate(rowSource,candidate,false);candidateRow.name="CandidateRow";candidateRow.gameObject.SetActive(true);Wire(view,"candidateRow",candidateRow);}
                Row(candidateRow);
                var installed=Ref<GameObject>(view,"installedSection")?.transform??Node(host,"InstalledSection",typeof(VerticalLayoutGroup));
                installed.SetParent(host,false);Vertical(installed,8);Element(installed,-1,80,1,1);Wire(view,"installedSection",installed.gameObject);Heading(installed,"Obecnie osadzone");
                var oldScroll=Ref<ScrollRect>(view,"detailsScroll");
                var list=Ref<Transform>(view,"socketList");
                if(oldScroll!=null)
                {
                    oldScroll.transform.SetParent(installed,false);
                    if(oldScroll.content!=null&&oldScroll.content!=list)oldScroll.content.gameObject.SetActive(false);
                }
                var scroll=Scroll(installed,"SocketDetailsScroll",list);Element(scroll.transform,-1,44,1,1);Wire(view,"detailsScroll",scroll);
                Vertical(list,14);Element(list,-1,-1,1,0);
                var cost=right.Find("CostSection");cost.SetSiblingIndex(2);Section(cost,"Koszt osadzenia",58,0);Wire(view,"costSection",cost.gameObject);
                var costHost=cost.Find("Content");foreach(var group in costHost.GetComponents<LayoutGroup>())group.enabled=false;
                Rect(costHost,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
                var costHeading=cost.Find("SectionTitle").GetComponent<TMP_Text>();Text(costHeading,21);Rect(costHeading.transform,new Vector2(0,1),Vector2.one,new Vector2(0,-28),new Vector2(-140,0));
                var price=Ref<TextMeshProUGUI>(view,"costText");Text(price,24);price.alignment=TextAlignmentOptions.Left;Rect(price.transform,Vector2.one,Vector2.one,new Vector2(-96,-32),Vector2.zero);
                var coin=Node(costHost,"GoldIcon",typeof(Image)).GetComponent<Image>();coin.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Icons/ResourceRewards/gold-32.png");coin.color=Color.white;coin.preserveAspect=true;coin.raycastTarget=false;Rect(coin.transform,Vector2.one,Vector2.one,new Vector2(-128,-28),new Vector2(-104,-4));
                var wallet=Node(costHost,"WalletText",typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();Text(wallet,17);Rect(wallet.transform,new Vector2(0,1),Vector2.one,new Vector2(0,-54),new Vector2(0,-32));Wire(view,"walletText",wallet);
                var status=Ref<TextMeshProUGUI>(view,"statusText");Text(status,17);Element(status.transform,-1,24,1,0);status.transform.SetAsLastSibling();
                var button=Ref<Button>(view,"socketButton");Button(button);Element(button.transform,-1,44,1,0);button.transform.SetAsLastSibling();button.transform.Find("Border")?.gameObject.SetActive(false);button.transform.Find("Icon")?.gameObject.SetActive(false);Text(button.GetComponentInChildren<TMP_Text>(true),21);
                PrefabUtility.SaveAsPrefabAsset(root,TradeUiSetup.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }
        private static void Catalog(GalduryteSocketingWindowView view,Transform section,string title,string gridField,string scrollField,string countField,string emptyField,float height,float flex)
        {
            Section(section,title,height,flex);
            var grid=Ref<Transform>(view,gridField);var host=section.Find("Content");var scroll=Scroll(host,title+"Scroll",grid);Wire(view,scrollField,scroll);Grid(grid.GetComponent<GridLayoutGroup>(),5,new Vector2(72,72));
            host.Find("IdentificationScroll")?.gameObject.SetActive(false);
            var count=Node(section,"ItemCount",typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();Text(count,18);count.alignment=TextAlignmentOptions.Right;Rect(count.transform,new Vector2(1,1),Vector2.one,new Vector2(-64,-28),Vector2.zero);Wire(view,countField,count);
            var empty=Node(host,"EmptyText",typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();Text(empty,18);Rect(empty.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);empty.alignment=TextAlignmentOptions.TopLeft;Wire(view,emptyField,empty);
        }
        private static void Vertical(Transform t,float spacing)
        {
            var layout=t.GetComponent<VerticalLayoutGroup>()??t.gameObject.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset();layout.spacing=spacing;layout.childAlignment=TextAnchor.UpperLeft;
            layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
        }
        private static void Heading(Transform parent,string label)
        {
            var t=Node(parent,"Heading",typeof(TextMeshProUGUI));Text(t.GetComponent<TMP_Text>(),23);t.GetComponent<TMP_Text>().text=label;Element(t,-1,28,1,0);t.SetAsFirstSibling();
        }
        private static void Row(GalduryteSocketRowView row)
        {
            Element(row.transform,-1,-1,1,0);var layout=row.GetComponent<HorizontalLayoutGroup>();layout.padding=new RectOffset();layout.spacing=12;layout.childAlignment=TextAnchor.UpperLeft;
            layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=false;layout.childForceExpandHeight=false;
            var icon=Ref<BlacksmithCraftingSlotView>(row,"iconSlot");Slot(icon);Element(icon.transform,64,64,0,0);icon.transform.SetAsFirstSibling();
            var title=Ref<TextMeshProUGUI>(row,"titleText");Text(title,21);Element(title.transform,-1,-1,1,0);title.transform.SetAsFirstSibling();
            var effect=Ref<TextMeshProUGUI>(row,"effectText");Text(effect,18);Element(effect.transform,-1,-1,1,0);effect.transform.SetAsLastSibling();
            Vertical(title.transform.parent,4);Element(title.transform.parent,-1,-1,1,0);
        }
        private static void Grid(GridLayoutGroup grid,int columns,Vector2 cell)
        {
            grid.cellSize = cell; grid.spacing = new Vector2(10,10);grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;grid.childAlignment = TextAnchor.UpperLeft;grid.padding = new RectOffset();
        }
        private static ScrollRect Scroll(Transform host, string name, Transform content)
        {
            var node = Node(host, name, typeof(ScrollRect)); Rect(node, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var viewport = Node(node, "Viewport", typeof(Image), typeof(RectMask2D));
            viewport.GetComponent<Image>().color = Color.clear;
            Rect(viewport, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-14, 0));
            content.SetParent(viewport, false); Rect(content, new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero);
            ((RectTransform)content).pivot = new Vector2(.5f, 1);
            var fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = node.GetComponent<ScrollRect>(); scroll.content = (RectTransform)content; scroll.viewport = viewport;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 24;
            var bar = node.Find("Scrollbar")?.GetComponent<Scrollbar>();
            if (bar == null) { bar = Object.Instantiate(scrollbarSource, node, false); bar.name = "Scrollbar"; }
            Rect(bar.transform, new Vector2(1, 0), Vector2.one, new Vector2(-10, 0), Vector2.zero);
            scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
        }
        private static void Section(Transform section, string title, float min, float flex)
        {
            Disable(section.gameObject); var image = section.GetComponent<Image>(); if (image != null) image.enabled = false;
            section.Find("Border")?.gameObject.SetActive(false); Element(section, -1, min, 1, flex);
            var heading = section.Find("SectionTitle").GetComponent<TMP_Text>(); Text(heading, 23); heading.text = title;
            Rect(heading.transform, new Vector2(0, 1), Vector2.one, new Vector2(0, -28), Vector2.zero);
            var content = section.Find("Content"); if (content != null) Rect(content, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -32));
        }
        private static void Slot(BlacksmithCraftingSlotView slot)
        {
            var button = Ref<Button>(slot, "button"); Disable(button.gameObject); button.transition = Selectable.Transition.None;
            var surface = button.GetComponent<Image>(); surface.sprite = palette.GetArtwork("item_frame"); surface.type = Image.Type.Sliced; surface.color = Color.white;
            var border = Ref<Image>(slot, "border"); Disable(border.gameObject); Rect(border.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); border.raycastTarget = false;
            var icon = Ref<Image>(slot, "icon"); Rect(icon.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-32, -32), new Vector2(32, 32)); icon.preserveAspect = true; icon.raycastTarget = false;
        }
        private static void Button(Button button)
        {
            Disable(button.gameObject); var image = button.GetComponent<Image>(); image.sprite = palette.GetArtwork("tab_normal"); image.type = Image.Type.Sliced; image.color = Color.white;
            button.targetGraphic = image; image.overrideSprite = null; button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { highlightedSprite = palette.GetArtwork("tab_hover"), selectedSprite = image.sprite,
                pressedSprite = palette.GetArtwork("tab_pressed"), disabledSprite = palette.GetArtwork("tab_disabled") };
        }
        private static void Disable(GameObject go)
        {
            foreach (var style in go.GetComponents<UIWorkspaceStyleApplier>())
            {
                // OnValidate still runs on disabled ExecuteAlways components during prefab authoring.
                // Clear only this tab's local targets so it cannot overwrite the authored values.
                foreach (var target in new[] { "surfaceImage", "borderImage", "text", "selectable" }) Wire(style, target, null);
                style.enabled = false;
            }
        }
        private static void Text(TMP_Text text, int size) { Disable(text.gameObject); text.font = palette.Font; text.fontSize = size; text.enableAutoSizing = false; text.color = new Color32(223,215,184,255); text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false; }
        private static void Element(Transform t, float w, float h, float fw, float fh) { var e = t.GetComponent<LayoutElement>() ?? t.gameObject.AddComponent<LayoutElement>(); e.minWidth = e.preferredWidth = w; e.minHeight = e.preferredHeight = h; e.flexibleWidth = fw; e.flexibleHeight = fh; }
        private static RectTransform Node(Transform parent, string name, params Type[] types) { var node = parent.Find(name) as RectTransform; if (node != null) return node; var go = new GameObject(name, typeof(RectTransform)); go.layer = 5; go.transform.SetParent(parent, false); foreach (var type in types) go.AddComponent(type); return (RectTransform)go.transform; }
        private static void Rect(Transform t, Vector2 min, Vector2 max, Vector2 lo, Vector2 hi) { var r = (RectTransform)t; r.anchorMin = min; r.anchorMax = max; r.pivot = new Vector2(.5f,.5f); r.offsetMin = lo; r.offsetMax = hi; }
        private static T Ref<T>(Object owner, string field) where T : Object => (T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;
        private static void Wire(Object owner, string field, Object value) { var so = new SerializedObject(owner); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
