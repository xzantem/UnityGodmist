using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public sealed partial class DungeonMinimapView
    {
        private const int OverviewResolution = 1024;
        private readonly List<Vector2Int> overviewCells = new();
        private readonly HashSet<Vector2Int> overviewCellSet = new();
        private readonly List<(Vector2Int Cell,Vector2Int Direction)> overviewOutlines = new();
        private readonly List<RectState> overviewLayout = new();
        private Texture2D overviewTexture;
        private Color32[] overviewPixels;
        private int overviewWidth, overviewHeight;
        private Vector2 overviewCenter;
        private float overviewScale;
        private bool overviewHasTransientVisibility;
        private readonly HashSet<DungeonTraversalPosition> overviewVisibleNodes = new();
        private int overviewKnownCount=-1,overviewContentsSignature;
        private Transform overviewParent;
        private int overviewSibling;
        private bool overviewWasExpanded, overviewRaycast;
        public bool IsOverviewOpen { get; private set; }
        [SerializeField] private ControllerNavigationScope controllerScope;
        public void CloseOverviewForController() => SetOverviewOpen(false);
        public RectInt OverviewDiscoveredBounds { get; private set; }
        public int OverviewTerrainRefreshCount { get; private set; }
        private int RasterWidth => IsOverviewOpen ? overviewWidth : Size;
        private int RasterHeight => IsOverviewOpen ? overviewHeight : Size;
        private float RasterScale => IsOverviewOpen ? overviewScale : Scale;
        private Color32[] RasterPixels => IsOverviewOpen ? overviewPixels : terrain;
        private Vector2 RasterPoint(Vector2 cell) => new Vector2(RasterWidth*.5f,RasterHeight*.5f)
            + (cell-(IsOverviewOpen?overviewCenter:(Vector2)center))*RasterScale;

        private readonly struct RectState
        {
            private readonly RectTransform rect;
            private readonly Vector2 min,max,pivot,position,size;
            public RectState(RectTransform rect)
            {
                this.rect=rect;min=rect.anchorMin;max=rect.anchorMax;pivot=rect.pivot;
                position=rect.anchoredPosition;size=rect.sizeDelta;
            }
            public void Restore()
            {
                if(rect==null)return;
                rect.anchorMin=min;rect.anchorMax=max;rect.pivot=pivot;
                rect.sizeDelta=size;rect.anchoredPosition=position;
            }
        }

        private void Update()
            => HandleOverviewInput(Input.GetKeyDown(KeyCode.M),(Input.GetKeyDown(KeyCode.Escape) || Godmist.Views.UI.ControllerInput.Cancel));

        private void HandleOverviewInput(bool mapPressed,bool escapePressed)
        {
            if(IsOverviewOpen && (party==null || party.Session==null || party.Session.IsDefeated
                || party.Session.CurrentMode!=GameModeState.Exploration))
            { SetOverviewOpen(false);return; }
            if(DevConsoleView.IsOpen || UIWorkspaceInputRouter.HasFocusedTextInput()
                || UIWorkspaceInputRouter.HasFocusedContextMenu())return;
            if(IsOverviewOpen)
            {
                if(mapPressed||escapePressed)SetOverviewOpen(false);
            }
            else if(mapPressed && party!=null && party.CanAcceptExplorationInput)
                SetOverviewOpen(true);
            if(IsOverviewOpen)UIWorkspaceWindowManager.Instance?.ConsumeContextMenuInput();
        }

        public void SetOverviewOpen(bool open)
        {
            if(IsOverviewOpen==open)return;
            var root=transform as RectTransform;
            if(open)
            {
                var canvas=GetComponentInParent<Canvas>();
                if(floor?.Map==null || map==null || image==null || content==null || markers==null
                    || root==null || canvas==null)return;
                overviewParent=root.parent;overviewSibling=root.GetSiblingIndex();
                overviewWasExpanded=content.activeSelf;overviewRaycast=image.raycastTarget;
                overviewLayout.Clear();
                foreach(var rect in new[]{root,content.transform as RectTransform,image.rectTransform,markers.rectTransform,toggle!=null?toggle.transform as RectTransform:null})
                    if(rect!=null)overviewLayout.Add(new RectState(rect));
                root.SetParent(canvas.transform,false);root.SetAsLastSibling();
                Stretch(root,new Vector2(.06f,.08f),new Vector2(.94f,.92f),Vector2.zero,Vector2.zero);
                Stretch((RectTransform)content.transform,Vector2.zero,Vector2.one,Vector2.zero,new Vector2(0,-28));
                Stretch(image.rectTransform,Vector2.zero,Vector2.one,new Vector2(4,4),new Vector2(-4,-4));
                Stretch(markers.rectTransform,Vector2.zero,Vector2.one,new Vector2(4,4),new Vector2(-4,-4));
                if(toggle!=null)Stretch((RectTransform)toggle.transform,new Vector2(0,1),Vector2.one,new Vector2(0,-28),Vector2.zero);
                content.SetActive(true);image.raycastTarget=true;
                IsOverviewOpen=true;
                if (controllerScope != null) controllerScope.enabled = true;
                root.ForceUpdateRectTransforms();image.rectTransform.ForceUpdateRectTransforms();
                PrepareOverview();
                image.texture=overviewTexture;image.uvRect=new Rect(0,0,1,1);
            }
            else
            {
                IsOverviewOpen=false;
                if (controllerScope != null) controllerScope.enabled = false;
                if (ControllerInput.Active) GetComponentInParent<ControllerInputView>()?.ReturnToWorld();
                if(root!=null && overviewParent!=null) { root.SetParent(overviewParent,false);root.SetSiblingIndex(overviewSibling); }
                foreach(var state in overviewLayout)state.Restore();
                overviewLayout.Clear();
                if(content!=null)content.SetActive(overviewWasExpanded);
                if(image!=null) { image.texture=texture;image.raycastTarget=overviewRaycast;image.uvRect=Viewport(center,center); }
                party?.ClaimWorldPrimaryClick();
                party?.ConsumeMapOverviewInput();
            }
            // The overview owns this frame's input, including the Escape/click
            // that closes it. Background windows must not handle it again.
            UIWorkspaceWindowManager.Instance?.ConsumeContextMenuInput();
            dirty=true;nextRefresh=0;RefreshTitle();
        }

        private static void Stretch(RectTransform rect,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax)
        {
            rect.anchorMin=min;rect.anchorMax=max;rect.pivot=new Vector2(.5f,.5f);
            rect.offsetMin=offsetMin;rect.offsetMax=offsetMax;
        }

        private bool PrepareOverview()
        {
            var size=image.rectTransform.rect.size;
            float aspect=size.y>0?size.x/size.y:1;
            int width=aspect>=1?OverviewResolution:Mathf.Max(1,Mathf.RoundToInt(OverviewResolution*aspect));
            int height=aspect<=1?OverviewResolution:Mathf.Max(1,Mathf.RoundToInt(OverviewResolution/aspect));
            if(overviewTexture!=null && overviewWidth==width && overviewHeight==height)return false;
            DestroyTexture(overviewTexture);overviewWidth=width;overviewHeight=height;
            overviewPixels=new Color32[width*height];
            overviewTexture=new Texture2D(width,height,TextureFormat.RGBA32,false)
                {name="Discovered dungeon overview",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            image.texture=overviewTexture;image.uvRect=new Rect(0,0,1,1);
            return true;
        }

        private void FrameOverview()
        {
            overviewCells.Clear();overviewCellSet.Clear();overviewHasTransientVisibility=false;
            void Add(DungeonTraversalPosition node)
            {
                bool onLevel=node.IsOnStairs?node.Stair.First.Level==level||node.Stair.Second.Level==level:node.Surface.Level==level;
                if(onLevel && floor.Map.TryGetTraversalCell(node,out var cell) && overviewCellSet.Add(cell))overviewCells.Add(cell);
            }
            if(floor.Fog.DebugFogDisabled)
            {
                foreach(var surface in floor.Map.Surfaces)Add(new DungeonTraversalPosition(surface));
                foreach(var stair in floor.Map.Stairs)
                    if(floor.Map.TryGetStairPath(stair,out var path))for(int i=1;i<path.Count-1;i++)Add(new DungeonTraversalPosition(stair,i));
            }
            else
            {
                foreach(var node in floor.Fog.Discovered)Add(node);
                int known=overviewCells.Count;
                foreach(var node in floor.Fog.Visible)Add(node);
                overviewHasTransientVisibility=overviewCells.Count!=known;
            }
            var min=Vector2Int.zero;var max=Vector2Int.zero;
            if(overviewCells.Count>0)
            {
                min=max=overviewCells[0];
                foreach(var cell in overviewCells){min=Vector2Int.Min(min,cell);max=Vector2Int.Max(max,cell);}
            }
            OverviewDiscoveredBounds=new RectInt(min,max-min+Vector2Int.one);
            overviewCells.Sort((a,b)=>a.y!=b.y?a.y.CompareTo(b.y):a.x.CompareTo(b.x));
            overviewOutlines.Clear();
            foreach(var cell in overviewCells)
            {
                var surface=new DungeonSurfacePosition(cell,level);
                if(!floor.Map.IsWalkable(surface))continue;
                foreach(var direction in Directions)
                {
                    var neighbour=new DungeonSurfacePosition(cell+direction,level);
                    if(!floor.Map.IsWalkable(neighbour)||walls.Contains(new DungeonSurfaceEdge(surface,neighbour)))
                        overviewOutlines.Add((cell,direction));
                }
            }
            overviewCenter=((Vector2)min+max)*.5f;
            // Two cells of breathing room, with an upper zoom limit for a lone tile.
            overviewScale=Mathf.Min(32f,Mathf.Min(overviewWidth/(OverviewDiscoveredBounds.width+4f),overviewHeight/(OverviewDiscoveredBounds.height+4f)));
        }

        private void RefreshOverview(int nextLevel,Transform leader,(Vector2 X,Vector2 Y) basis)
        {
            bool resized=PrepareOverview();
            bool changed=dirty || resized || level!=nextLevel || debugRevision!=floor.Fog.DebugRevision;
            bool reframe=changed || overviewKnownCount!=floor.Fog.Discovered.Count;
            int contents=overviewContentsSignature;
            if(changed || fogRevision!=floor.Fog.Revision)
            {
                contents=ReadOverviewContentsSignature();
                changed|=overviewKnownCount!=floor.Fog.Discovered.Count || contents!=overviewContentsSignature
                    || !overviewVisibleNodes.SetEquals(floor.Fog.Visible);
                reframe|=overviewHasTransientVisibility && !overviewVisibleNodes.SetEquals(floor.Fog.Visible);
                if(!reframe)
                    foreach(var node in floor.Fog.Visible)
                        if((node.IsOnStairs?node.Stair.First.Level==nextLevel||node.Stair.Second.Level==nextLevel:node.Surface.Level==nextLevel)
                            && floor.Map.TryGetTraversalCell(node,out var cell) && !overviewCellSet.Contains(cell)){reframe=true;break;}
                fogRevision=floor.Fog.Revision;
            }
            if(changed)
            {
                level=nextLevel;fogRevision=floor.Fog.Revision;debugRevision=floor.Fog.DebugRevision;
                using(MinimapTerrainSample.Auto())
                {
                    if(reframe)FrameOverview();
                    DrawTerrain();overviewTexture.SetPixels32(overviewPixels);overviewTexture.Apply(false,false);
                }
                overviewKnownCount=floor.Fog.Discovered.Count;overviewContentsSignature=contents;
                overviewVisibleNodes.Clear();overviewVisibleNodes.UnionWith(floor.Fog.Visible);
                OverviewTerrainRefreshCount++;dirty=false;RefreshTitle();
            }
            markers.ClearMarkers();
            foreach(var member in party.ActiveMemberViews)DrawActor(member,member.transform==leader?leaderColor:partyColor,false,overviewCenter,basis);
            if(encounters!=null)
                foreach(var trigger in encounters.Triggers)
                    foreach(var enemy in trigger.EnemyViews)DrawActor(enemy,enemyColor,true,overviewCenter,basis);
        }

        private void DrawOverviewTerrainCells(Color32[] pixels)
        {
            // Contiguous equal-knowledge cells share one rectangle. This avoids
            // thousands of tiny row fills on a large remembered floor.
            for(int i=0;i<overviewCells.Count;i++)
            {
                var first=overviewCells[i];var last=first;
                var knowledge=TerrainKnowledge(floor,new DungeonSurfacePosition(first,level));
                if(knowledge==DungeonKnowledge.Unexplored)continue;
                while(i+1<overviewCells.Count && overviewCells[i+1].y==first.y && overviewCells[i+1].x==last.x+1
                    && TerrainKnowledge(floor,new DungeonSurfacePosition(overviewCells[i+1],level))==knowledge)last=overviewCells[++i];
                var start=RasterPoint(first)-Vector2.one*RasterScale*.5f;
                var end=RasterPoint(last)+Vector2.one*RasterScale*.5f;
                int x=Mathf.RoundToInt(start.x),y=Mathf.RoundToInt(start.y);
                Fill(pixels,x,y,Mathf.Max(1,Mathf.RoundToInt(end.x)-x),Mathf.Max(1,Mathf.RoundToInt(end.y)-y),
                    knowledge==DungeonKnowledge.Visible?visibleColor:rememberedColor);
            }
            if(RasterScale<3)return;
            foreach(var edge in overviewOutlines)
            {
                var start=RasterPoint(edge.Cell)-Vector2.one*RasterScale*.5f;
                int x=Mathf.RoundToInt(start.x),y=Mathf.RoundToInt(start.y);
                int width=Mathf.Max(1,Mathf.RoundToInt(start.x+RasterScale)-x),height=Mathf.Max(1,Mathf.RoundToInt(start.y+RasterScale)-y);
                var d=edge.Direction;
                Fill(pixels,x+(d.x>0?width-1:0),y+(d.y>0?height-1:0),d.x==0?width:1,d.y==0?height:1,Color.black);
            }
        }

        private int ReadOverviewContentsSignature()
        {
            unchecked
            {
                int stamp=floor.Map.TraversalRevision;
                foreach(var pair in floor.Fog.Objects)
                    stamp=stamp*31+pair.Key.GetHashCode()+(pair.Value.Active?1:0)+(pair.Value.State?2:0);
                foreach(var obj in floor.Objects)stamp=stamp*31+(obj.IsActive?1:0)+(obj is DoorDungeonObjectInstance door && door.IsOpen?2:0);
                foreach(var body in floor.EnemyBodies)stamp=stamp*31+body.InstanceId.GetHashCode()+(body.IsEmpty?1:0);
                foreach(var edge in floor.Fog.ClosedGates)stamp=stamp*31+edge.GetHashCode();
                return stamp;
            }
        }

        private void OnDisable()
        {
            Godmist.Views.UI.Workspace.DungeonLootWindowView.LootClaimed -= OnLootClaimed;
            Canvas.preWillRenderCanvases -= RefreshProjection;
            SetOverviewOpen(false);
        }
        private static void DestroyTexture(Texture2D value)
        {
            if(value==null)return;
            if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);
        }
    }
}
