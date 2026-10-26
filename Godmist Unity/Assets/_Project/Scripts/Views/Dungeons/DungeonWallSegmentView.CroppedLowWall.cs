using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public partial class DungeonWallSegmentView
    {
        private float nativeLowWallClipTop=float.PositiveInfinity;
        public bool ConfigureCroppedLowWall(DungeonCellEdge edge, Sprite fullWall, Vector2 groundCenter,
            float clipMinX=float.NegativeInfinity,float clipMaxX=float.PositiveInfinity,float clipMaxY=float.PositiveInfinity)
        {
            if(fullWall==null || fullWall.rect.height<=8)return false;
            var source=GetComponent<SpriteRenderer>();if(source==null)return false;
            nativeLowWallClipTop=clipMaxY;
            ClearNativeLowWallJoins();
            var horizontal=edge.FirstCell.x==edge.SecondCell.x;
            if(catacombsLowWallSource!=fullWall || catacombsLowWallFace==null)
            {
                ReleaseLowWallSprites();catacombsLowWallSource=fullWall;
                var rect=fullWall.rect;rect.y+=8;rect.height-=8;
                // The native side profile is an eight-pixel strip of the same
                // masonry; do not squash a whole horizontal tile into its width.
                if(!horizontal){rect.x=rect.center.x-4;rect.width=8;}
                catacombsLowWallFace=Sprite.Create(fullWall.texture,rect,new Vector2(.5f,0),fullWall.pixelsPerUnit,0,SpriteMeshType.FullRect);
                catacombsLowWallFace.name="Native wall minus bottom 8 pixels";
                catacombsLowWallFace.hideFlags=HideFlags.DontSave;
            }
            if(catacombsLowWallVisualRoot==null)
            {
                var root=new GameObject("Cropped masonry low wall");root.transform.SetParent(transform,false);
                catacombsLowWallVisualRoot=root.transform;
            }
            var scale=transform.lossyScale;
            catacombsLowWallVisualRoot.localScale=new Vector3(1/Mathf.Abs(scale.x),1/Mathf.Abs(scale.y),1);
            // Cancel scale in the parent's own axes before cancelling its
            // rotation on the sprite. Swapping this order flattens horizontal
            // walls when the legacy root is rotated with nonuniform scale.
            catacombsLowWallVisualRoot.localRotation=Quaternion.identity;
            if(catacombsLowWallRenderer==null)
            {
                var go=new GameObject("Cropped native low wall");go.transform.SetParent(catacombsLowWallVisualRoot,false);
                catacombsLowWallRenderer=go.AddComponent<SpriteRenderer>();
            }
            foreach(var renderer in GetComponentsInChildren<SpriteRenderer>(true))renderer.enabled=renderer==catacombsLowWallRenderer;
            var view=catacombsLowWallRenderer;
            view.transform.rotation=Quaternion.identity;view.transform.localScale=Vector3.one;
            view.transform.position=groundCenter+(horizontal?Vector2.zero:Vector2.down*VisualLength*.5f);
            view.sprite=catacombsLowWallFace;view.drawMode=SpriteDrawMode.Tiled;view.tileMode=SpriteTileMode.Continuous;
            view.size=horizontal?new Vector2(VisualLength,catacombsLowWallFace.rect.height/fullWall.pixelsPerUnit)
                :new Vector2(8/fullWall.pixelsPerUnit,VisualLength+.5f);
            var visual=view.bounds;
            var minX=Mathf.Max(visual.min.x,clipMinX);var maxX=Mathf.Min(visual.max.x,clipMaxX);
            if(horizontal && (minX>visual.min.x || maxX<visual.max.x))
            {
                view.size=new Vector2(Mathf.Max(1/fullWall.pixelsPerUnit,maxX-minX),view.size.y);
                view.transform.position=new Vector3((minX+maxX)*.5f,view.transform.position.y,view.transform.position.z);
            }
            if(!horizontal && visual.max.y>clipMaxY)
                view.size=new Vector2(view.size.x,Mathf.Max(1/fullWall.pixelsPerUnit,clipMaxY-view.transform.position.y));
            view.color=Color.white;view.sharedMaterial=source.sharedMaterial;
            view.sortingLayerID=source.sortingLayerID;view.sortingOrder=source.sortingOrder;
            view.maskInteraction=source.maskInteraction;view.gameObject.layer=gameObject.layer;
            return true;
        }

        private void ClearNativeLowWallJoins()
        {
            foreach(var renderer in lowWallJunctionRenderers)
                if(renderer!=null){if(Application.isPlaying)Destroy(renderer.gameObject);else DestroyImmediate(renderer.gameObject);}
            foreach(var sprite in lowWallJunctionSprites)DestroyRuntimeCornerSprite(sprite);
            lowWallJunctionRenderers.Clear();lowWallJunctionSprites.Clear();
        }

        internal void ConfigureNativeLowWallJoins(Sprite side,Sprite north,Vector2 first,Vector2 second,
            int firstArms,int secondArms,bool ownFirst,bool ownSecond)
        {
            if(catacombsLowWallRenderer==null||side==null||north==null)return;
            var horizontal=Mathf.Abs(first.y-second.y)<.001f;
            var ppu=north.pixelsPerUnit;
            var capDepth=8/ppu;
            var height=(north.rect.height-8)/ppu;
            var faceHeight=height-capDepth;
            var half=capDepth*.5f;
            var body=catacombsLowWallRenderer;
            var min=Vector2.Min(first,second);var max=Vector2.Max(first,second);
            if(horizontal)
            {
                // Extend the face under the terminal/turn cap, but retain the
                // existing strict clips where it meets a full-height wall.
                var leftOwn=first.x<second.x?ownFirst:ownSecond;
                var rightOwn=first.x<second.x?ownSecond:ownFirst;
                var bounds=body.bounds;
                var left=leftOwn?min.x-half:bounds.min.x;
                var right=rightOwn?max.x+half:bounds.max.x;
                body.transform.position=new Vector3((left+right)*.5f,body.transform.position.y,body.transform.position.z);
                body.size=new Vector2(right-left,height);
            }
            else
            {
                // The vertical strip is the top surface, elevated by the same
                // face as a horizontal wall. It must not continue to the floor.
                var bottom=min.y+height-half;
                var top=Mathf.Min(max.y+height-half,nativeLowWallClipTop);
                if(catacombsLowWallSideCap==null)
                {
                    catacombsLowWallSideCap=Sprite.Create(side.texture,side.rect,new Vector2(.5f,0),side.pixelsPerUnit,0,SpriteMeshType.FullRect);
                    catacombsLowWallSideCap.hideFlags=HideFlags.DontSave;
                }
                body.sprite=catacombsLowWallSideCap;
                body.transform.position=new Vector3(first.x,bottom,body.transform.position.z);
                body.size=new Vector2(capDepth,Mathf.Max(1/ppu,top-bottom));
                var southArms=first.y<second.y?firstArms:secondArms;
                var southOwn=first.y<second.y?ownFirst:ownSecond;
                if(southOwn && (southArms&7)==0)
                {
                    var rect=north.rect;rect.y+=8;rect.height-=16;rect.x=rect.center.x-4;rect.width=8;
                    AddPiece("Native low wall south face",Crop(rect),new Vector2(min.x,min.y),new Vector2(capDepth,faceHeight),0);
                }
            }
            AddEndpoint(first,firstArms,ownFirst);
            AddEndpoint(second,secondArms,ownSecond);

            void AddEndpoint(Vector2 vertex,int arms,bool owns)
            {
                if(!owns||arms==3||arms==12)return; // no cap across a straight continuation
                var capCenter=vertex+Vector2.up*(height-half);
                var patch=CreateDedicatedCornerSprite(side,north,vertex,body.bounds,body.bounds,
                    CornerMiterRole.NorthCap,(arms&1)!=0?1:-1,(arms&4)!=0?1:-1,arms);
                cornerMiterSprites.Remove(patch);lowWallJunctionSprites.Add(patch);
                var renderer=AddPiece("Native low wall cap junction "+arms,patch,capCenter-Vector2.up*half,
                    Vector2.one*capDepth,1,false);
                // Corner sprites are centred; ordinary UV crops use bottom pivots.
                renderer.transform.position=capCenter;
            }

            Sprite Crop(Rect rect)
            {
                var sprite=Sprite.Create(north.texture,rect,new Vector2(.5f,0),ppu,0,SpriteMeshType.FullRect);
                sprite.hideFlags=HideFlags.DontSave;lowWallJunctionSprites.Add(sprite);return sprite;
            }
            SpriteRenderer AddPiece(string name,Sprite sprite,Vector2 position,Vector2 size,int order,bool tiled=true)
            {
                var go=new GameObject(name);go.transform.SetParent(catacombsLowWallVisualRoot,false);
                go.transform.rotation=Quaternion.identity;go.transform.localScale=Vector3.one;go.transform.position=position;
                var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=sprite;
                renderer.drawMode=tiled?SpriteDrawMode.Tiled:SpriteDrawMode.Simple;renderer.size=size;
                renderer.color=body.color;renderer.sharedMaterial=body.sharedMaterial;
                renderer.sortingLayerID=body.sortingLayerID;renderer.sortingOrder=body.sortingOrder+order;
                renderer.maskInteraction=body.maskInteraction;go.layer=body.gameObject.layer;
                lowWallJunctionRenderers.Add(renderer);return renderer;
            }
        }
    }
}
