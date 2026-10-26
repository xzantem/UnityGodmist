using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public partial class DungeonFloorMapView
    {
        private readonly HashSet<Renderer> forestExitRails = new();
        private Sprite forestExitRailSprite;

        // Return the wicket's short wings along the real, closed room boundary.
        // Per-cell pieces retain the existing fog owner and native pixel scale.
        private void BuildForestExitRails(List<Renderer>[] renderers)
        {
            if(forestTransitions.Count==0) return;
            var gate=DungeonForestObjectVisuals.Load("exit-gate-v2");
            if(forestExitRailSprite==null)
            {
                var rect=gate.rect;rect.x+=4;rect.y+=24;rect.width=30;rect.height=23;
                forestExitRailSprite=Sprite.Create(gate.texture,rect,new Vector2(.5f,0),gate.pixelsPerUnit,0,SpriteMeshType.FullRect);
                forestExitRailSprite.name="Exit fence rail (native crop)";
            }
            var owners=wallRenderers.Select((p,i)=>(p.Edge,i)).ToDictionary(p=>p.Edge,p=>p.i);
            foreach(var entry in forestTransitions)
            {
                var threshold=entry.Value;
                var inside=WorldToCell(forestTransitionApproaches[entry.Key]);
                if(!boundMap.TryGetRoomId(inside,out var room))continue;
                var normal=threshold.y>forestTransitionApproaches[entry.Key].y?Vector2Int.up:Vector2Int.down;
                bool Boundary(Vector2Int c)=>boundMap.TryGetRoomId(c,out var id)&&id==room&&!boundMap.IsWalkable(c+normal);
                var left=inside;var right=inside;
                while(Boundary(left+Vector2Int.left))left+=Vector2Int.left;
                while(Boundary(right+Vector2Int.right))right+=Vector2Int.right;
                var facadeLeft=left;var facadeRight=right;
                // The rasterised rim steps down at rounded corners. Carry the
                // rails a little into solid woodland so they meet trunks rather
                // than stopping at the last cell of that short horizontal lip.
                for(var i=0;i<3;i++)
                {
                    if(!boundMap.IsWalkable(left+Vector2Int.left) && !boundMap.IsWalkable(left+Vector2Int.left+normal))left+=Vector2Int.left;
                    if(!boundMap.IsWalkable(right+Vector2Int.right) && !boundMap.IsWalkable(right+Vector2Int.right+normal))right+=Vector2Int.right;
                }
                var width=Vector2.Distance(CellToWorldCenter(inside),CellToWorldCenter(inside+Vector2Int.right));
                var cellSize=CellToWorldCenter(Vector2Int.one)-CellToWorldCenter(Vector2Int.zero);
                var leftStop=CellToWorldCenter(left).x-width*.5f;
                var rightStop=CellToWorldCenter(right).x+width*.5f;
                var rootLine=threshold.y+normal.y*.25f*cellSize.y;
                foreach(var footprint in forestTreeFootprints.Values)
                {
                    var origin=CellToWorldCenter(footprint.min)-cellSize*.5f;
                    var bounds=new Rect(origin,Vector2.Scale(footprint.size,cellSize));
                    if(rootLine<bounds.yMin || rootLine>bounds.yMax)continue;
                    var trunk=bounds.center.x;
                    if(trunk<threshold.x-1.8f)leftStop=Mathf.Max(leftStop,trunk);
                    if(trunk>threshold.x+1.8f)rightStop=Mathf.Min(rightStop,trunk);
                }
                for(var x=left.x;x<=right.x;x++)
                {
                    var cell=new Vector2Int(x,inside.y);
                    var ownerCell=new Vector2Int(Mathf.Clamp(x,facadeLeft.x,facadeRight.x),inside.y);
                    var edge=new DungeonSurfaceEdge(new DungeonCellEdge(ownerCell,ownerCell+normal));
                    if(!owners.TryGetValue(edge,out var owner))continue;
                    var center=CellToWorldCenter(cell);
                    var a=Mathf.Max(center.x-width*.5f,leftStop);var b=Mathf.Min(center.x+width*.5f,rightStop);
                    if(b<=threshold.x) b=Mathf.Min(b,threshold.x-1.8f);
                    else a=Mathf.Max(a,threshold.x+1.8f);
                    if(b-a<.01f)continue;
                    var go=new GameObject("Forest exit boundary rail");
                    go.layer=wallRenderers[owner].View.gameObject.layer;
                    go.transform.SetParent(wallsRoot!=null?wallsRoot:transform,false);
                    go.transform.position=new Vector3((a+b)*.5f,threshold.y,0);
                    var rail=go.AddComponent<SpriteRenderer>();rail.sprite=forestExitRailSprite;
                    rail.drawMode=SpriteDrawMode.Tiled;rail.tileMode=SpriteTileMode.Continuous;
                    rail.size=new Vector2(b-a,forestExitRailSprite.rect.height/forestExitRailSprite.pixelsPerUnit);
                    rail.color=new Color(.82f,.83f,.78f,1);
                    rail.sortingLayerID=FloorFeedbackSortingLayerID;
                    rail.sortingOrder=GetStandingObjectDepthSortingOrder(threshold.y);
                    forestExitRails.Add(rail);forestBoundaryOwners[rail]=new[]{edge};
                    renderers[owner].Add(rail);spawnedWalls.Add(go);
                    RegisterGroundLightingPresentation(rail,false,true);
                }
            }
        }

        // Tree/prop registration changes the map's compact depth lanes after
        // these static renderers are created. Refresh only on a depth rebuild,
        // like boundary crowns; never leave the original build-time order behind.
        private void RefreshForestExitRailDepthOrders()
        {
            foreach (var rail in forestExitRails)
                if (rail != null)
                {
                    rail.sortingLayerID = FloorFeedbackSortingLayerID;
                    rail.sortingOrder = ResolveDepthSortingOrder(rail.transform.position.y, false) + 2;
                }
        }

        private void ClearForestExitRails()
        {
            foreach(var rail in forestExitRails)if(rail is SpriteRenderer sr)UnregisterLightingPresentation(sr);
            forestExitRails.Clear();
        }
        private void ReleaseForestExitRailSprite()
        {
            ClearForestExitRails();
            if(forestExitRailSprite==null)return;
            if(Application.isPlaying)Destroy(forestExitRailSprite);else DestroyImmediate(forestExitRailSprite);
            forestExitRailSprite=null;
        }
    }
}
