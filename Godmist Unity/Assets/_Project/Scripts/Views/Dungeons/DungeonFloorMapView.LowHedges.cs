using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using UnityEngine;
namespace Godmist.Views.Dungeons
{
    public partial class DungeonFloorMapView
    {
        private readonly Dictionary<SpriteRenderer,float> forestLowHedgeBaselines=new();
        private void BuildForestLowHedge(DungeonSurfaceEdge edge,List<Renderer> renderers)
        {
            var first=CellToWorldCenter(edge.FirstCell);var second=CellToWorldCenter(edge.SecondCell);
            var center=(first+second)*.5f;var horizontal=edge.FirstCell.x==edge.SecondCell.x;
            var sprites=vegetationPresentation.LowHedgeSegments;
            var index=horizontal?0:1;
            if((VegetationHash(edge.FirstCell)&1)!=0)index=horizontal?3:13;
            Sprite Art(int i)=>sprites!=null&&i<sprites.Length&&sprites[i]!=null?sprites[i]:vegetationPresentation.LowShrub;
            Add(Art(index),center,new[]{edge},"Crossable forest low hedge");
            GetEdgeEndpoints(edge.CellEdge,out var a,out var b);
            foreach(var vertex in new[]{a,b})
            {
                if(!OwnsLowWallVertex(edge,vertex))continue;
                var owners=WallsAtEndpoint(edge.Level,vertex).Where(e=>boundMap.GetWallType(e)==DungeonWallType.Low).ToArray();
                var arms=0;
                foreach(var owner in owners) {
                    GetEdgeEndpoints(owner.CellEdge,out var p,out var q);var other=p==vertex?q:p;
                    arms|=other.x<vertex.x?1:other.x>vertex.x?2:other.y<vertex.y?4:8;
                }
                var join=arms switch {10=>4,6=>5,5=>6,9=>7,11=>8,14=>9,7=>10,13=>11,15=>12,3 or 12=>-1,_=>2};
                if(join<0)continue;
                var point=(Vector2)floorTilemap.CellToWorld(new Vector3Int(vertex.x,vertex.y,0));
                Add(Art(join),point,owners,"Forest low hedge native join");
            }
            void Add(Sprite sprite,Vector2 feet,DungeonSurfaceEdge[] owners,string name)
            {
                if(sprite==null)return;
                var go=new GameObject(name);go.layer=gameObject.layer;go.transform.SetParent(wallsRoot!=null?wallsRoot:transform,false);
                // Separate native orientations: no rotated lighting or stretched pixels.
                go.transform.position=feet+Vector2.up*.2f;
                var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.color=Color.white;
                var baseline=horizontal?feet.y:feet.y-Vector2.Distance(first,second)*.5f;
                renderer.sortingLayerID=FloorFeedbackSortingLayerID;renderer.sortingOrder=GetStandingObjectDepthSortingOrder(baseline);
                forestLowHedgeBaselines[renderer]=baseline;forestPropContacts[renderer]=new Vector2(feet.x,baseline);
                forestBoundaryOwners[renderer]=owners;spawnedWalls.Add(go);renderers.Add(renderer);
            }
        }
        private void RefreshForestLowHedgeDepthOrders()
        {
            foreach(var pair in forestLowHedgeBaselines)if(pair.Key!=null)
            {pair.Key.sortingLayerID=FloorFeedbackSortingLayerID;pair.Key.sortingOrder=ResolveDepthSortingOrder(pair.Value,false)+2;}
        }
    }
}
