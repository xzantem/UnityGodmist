using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public partial class DungeonFloorMapView
    {
        private readonly List<(SpriteRenderer Renderer,DungeonSurfaceEdge[] Edges,bool Remembered)> forestProgressionGates=new();
        private readonly HashSet<Renderer> forestProgressionGateRenderers=new();
        private readonly HashSet<DungeonSurfaceEdge> forestRememberedGateEdges=new();
        private readonly HashSet<DungeonSurfaceEdge> forestActualGateEdges=new(),forestNextRememberedGateEdges=new();
        private readonly Dictionary<DungeonSurfaceEdge,Collider2D> forestGateMovementBoundaries=new();
        private bool IsForestProgressionGateRenderer(Renderer renderer)=>forestProgressionGateRenderers.Contains(renderer);

        private void BuildForestProgressionGates(List<Renderer>[] renderers)
        {
            var indices=new Dictionary<DungeonSurfaceEdge,int>();
            for(var i=0;i<wallRenderers.Count;i++)indices[wallRenderers[i].Edge]=i;
            foreach(var edges in GroupForestGateEdges(boundMap.SurfaceClosedProgressionGateEdges))
            {
                if(!indices.TryGetValue(edges[0],out var owner))continue;
                var renderer=CreateForestProgressionGate(edges,false,wallRenderers[owner].View.gameObject.layer);
                renderers[owner].Add(renderer);
            }
        }
        private SpriteRenderer CreateForestProgressionGate(DungeonSurfaceEdge[] edges,bool remembered,int layer)
        {
            var root=new GameObject(remembered?"Remembered Forest log barrier":"Forest log progression barrier");
            root.layer=layer;root.transform.SetParent(wallsRoot!=null?wallsRoot:transform,false);
            var center=Vector3.zero;
            foreach(var edge in edges)center+=(Vector3)((CellToWorldCenter(edge.FirstCell)+CellToWorldCenter(edge.SecondCell))*.5f);
            center/=edges.Length;root.transform.position=center;
            var horizontal=edges[0].FirstCell.x==edges[0].SecondCell.x;
            if(!horizontal)root.transform.localRotation=Quaternion.Euler(0,0,90);
            var renderer=root.AddComponent<SpriteRenderer>();renderer.sprite=DungeonForestObjectVisuals.Load("barrier");
            renderer.drawMode=SpriteDrawMode.Tiled;renderer.tileMode=SpriteTileMode.Continuous;
            var cellSize=Vector2.Distance(CellToWorldCenter(Vector2Int.up),CellToWorldCenter(Vector2Int.zero));
            renderer.size=new Vector2(edges.Length*cellSize,renderer.sprite.rect.height/renderer.sprite.pixelsPerUnit);
            renderer.sortingLayerID=FloorFeedbackSortingLayerID;
            renderer.sortingOrder=GetWallMountedDepthSortingOrder(center.y);
            renderer.forceRenderingOff=true;
            forestProgressionGates.Add((renderer,edges,remembered));forestProgressionGateRenderers.Add(renderer);
            RegisterLightingPresentation(renderer);spawnedWalls.Add(root);
            return renderer;
        }
        private static IEnumerable<DungeonSurfaceEdge[]> GroupForestGateEdges(IEnumerable<DungeonSurfaceEdge> source)
        {
            var remaining=new HashSet<DungeonSurfaceEdge>(source);
            while(remaining.Count>0)
            {
                var first=remaining.OrderBy(e=>e.Level).ThenBy(e=>e.FirstCell.y).ThenBy(e=>e.FirstCell.x).First();
                var direction=first.FirstCell.x==first.SecondCell.x?Vector2Int.right:Vector2Int.up;
                var group=new List<DungeonSurfaceEdge>();var pending=new Queue<DungeonSurfaceEdge>();pending.Enqueue(first);remaining.Remove(first);
                while(pending.Count>0)
                {
                    var edge=pending.Dequeue();group.Add(edge);
                    foreach(var sign in new[]{-1,1})
                    {
                        var next=new DungeonSurfaceEdge(new DungeonCellEdge(edge.FirstCell+direction*sign,edge.SecondCell+direction*sign),edge.Level);
                        if(remaining.Remove(next))pending.Enqueue(next);
                    }
                }
                yield return group.ToArray();
            }
        }
        private void RefreshForestProgressionGates()
        {
            if(FogFloor==null||boundMap==null)return;
            var actual=forestActualGateEdges;actual.Clear();actual.UnionWith(boundMap.SurfaceClosedProgressionGateEdges);
            var remembered=forestNextRememberedGateEdges;remembered.Clear();
            foreach(var edge in FogFloor.Fog.ClosedGates)if(!actual.Contains(edge))remembered.Add(edge);
            if(!forestRememberedGateEdges.SetEquals(remembered))
            {
                for(var i=forestProgressionGates.Count-1;i>=0;i--)
                {
                    var gate=forestProgressionGates[i];if(!gate.Remembered)continue;
                    forestProgressionGateRenderers.Remove(gate.Renderer);UnregisterLightingPresentation(gate.Renderer);
                    if(gate.Renderer!=null){if(Application.isPlaying)Destroy(gate.Renderer.gameObject);else DestroyImmediate(gate.Renderer.gameObject);}
                    forestProgressionGates.RemoveAt(i);
                }
                forestRememberedGateEdges.Clear();forestRememberedGateEdges.UnionWith(remembered);
                foreach(var edges in GroupForestGateEdges(remembered))CreateForestProgressionGate(edges,true,gameObject.layer);
            }
            foreach(var gate in forestProgressionGates)
            {
                var knowledge=DungeonKnowledge.Unexplored;var knownClosed=false;var visible=false;var actualClosed=false;
                foreach(var edge in gate.Edges)
                {
                    var k=EdgeKnowledge(edge);if(k>knowledge)knowledge=k;
                    knownClosed|=FogFloor.Fog.ClosedGates.Contains(edge);
                    actualClosed|=actual.Contains(edge);
                    visible|=IsFogEdgeVisible(edge);
                }
                gate.Renderer.forceRenderingOff=knowledge==DungeonKnowledge.Unexplored||!visible
                    ||(gate.Remembered?FogFloor.Fog.DebugFogDisabled:!actualClosed||!knownClosed&&!FogFloor.Fog.DebugFogDisabled);
                TintFog(gate.Renderer,gate.Remembered?DungeonKnowledge.Remembered:knowledge);
            }
        }
        private void ClearForestProgressionGates()
        {
            foreach(var gate in forestProgressionGates)if(gate.Renderer!=null)UnregisterLightingPresentation(gate.Renderer);
            forestProgressionGates.Clear();forestProgressionGateRenderers.Clear();forestRememberedGateEdges.Clear();
            forestActualGateEdges.Clear();forestNextRememberedGateEdges.Clear();
            forestGateMovementBoundaries.Clear();
        }
    }
}
