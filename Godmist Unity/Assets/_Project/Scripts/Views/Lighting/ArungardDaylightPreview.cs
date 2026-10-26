using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Views.Lighting;
using UnityEngine;
using UnityEngine.Rendering;

// Isolated city-art preview. Inputs are explicit authoring references, never runtime discovery.
[DefaultExecutionOrder(11000)]
public sealed class ArungardDaylightPreview : MonoBehaviour
{
    [Serializable] public struct Caster { public SpriteRenderer source; public float heightOffset; }
    [Serializable] public struct Actor { public SpriteRenderer source; public float slope,offset; }
    public Caster[] scenery=Array.Empty<Caster>();
    public Actor[] actors=Array.Empty<Actor>();
    public BoxCollider2D[] contacts=Array.Empty<BoxCollider2D>();
    public Shader projectionShader,contactShader,overlayShader;
    public MeshRenderer overlay;
    public Vector2 sunlightProjection=new(-.55f,-.32f);
    public Color daylight=new(1.02f,1f,.96f,1);
    public float strength=.43f;
    public int StaticBuildCount {get;private set;}
    public int LastActorCount {get;private set;}
    public int CachedMeshCount=>flat.Count+volumes.Count;
    public RenderTexture ShadowMask=>mask;
    public bool showShadows=true;
    public Vector4 maskBounds=new(-8,-8,48,48);
    public Vector4[] decorativeContacts=Array.Empty<Vector4>();
    readonly Dictionary<Sprite,Mesh> flat=new(),volumes=new();
    readonly Dictionary<Sprite,Vector4> uvBounds=new();
    readonly List<Mesh> contactMeshes=new();
    RenderTexture mask,staticMask;
    Material projection,contact,receiver;
    CommandBuffer commands;
    MaterialPropertyBlock properties;
    Matrix4x4 vp;
    Material originalOverlay;
    void LateUpdate()
    {
        if(overlay==null||projectionShader==null||contactShader==null||overlayShader==null)return;
        if(mask==null)Create();
        Shader.SetGlobalColor("_CityDaylightTint",daylight);
        commands.Clear();commands.CopyTexture(staticMask,mask);commands.SetRenderTarget(mask);
        LastActorCount=0;
        foreach(var actor in actors)
        {
            var sr=actor.source;
            if(sr==null||!sr.enabled||sr.forceRenderingOff||!sr.gameObject.activeInHierarchy||sr.sprite==null)continue;
            Draw(sr,0,true,actor.slope,actor.offset);LastActorCount++;
            // A compact ground contact under the feet, on the same 32PPU mask grid.
            properties.Clear();properties.SetMatrix("_ShadowVP",vp);
            commands.DrawMesh(footMesh,Matrix4x4.Translate(sr.transform.position),contact,0,0,properties);
        }
        Graphics.ExecuteCommandBuffer(commands);
        receiver.SetFloat("_Strength",showShadows?strength:0);
    }
    Mesh footMesh;
    void Create()
    {
        int pixels=Mathf.RoundToInt(maskBounds.z*32);
        var format=SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8)?RenderTextureFormat.R8:RenderTextureFormat.ARGB32;
        RenderTexture Make(string name)
        {
            var rt=new RenderTexture(pixels,pixels,0,format,RenderTextureReadWrite.Linear){name=name,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,antiAliasing=1};rt.Create();return rt;
        }
        mask=Make("City current shadow mask");staticMask=Make("City cached scenery shadow mask");
        projection=new Material(projectionShader);contact=new Material(contactShader);receiver=new Material(overlayShader);
        originalOverlay=overlay.sharedMaterial;overlay.sharedMaterial=receiver;
        receiver.SetTexture("_ShadowMask",mask);receiver.SetVector("_GroundBounds",maskBounds);
        receiver.SetColor("_ShadowColor",new Color(.065f,.085f,.12f,1));
        commands=new CommandBuffer{name="Arungard daylight silhouette mask"};properties=new MaterialPropertyBlock();
        vp=GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(maskBounds.x,maskBounds.x+maskBounds.z,maskBounds.y,maskBounds.y+maskBounds.w,-1,1),true);
        footMesh=Ellipse(.23f,.075f);
        commands.SetRenderTarget(staticMask);commands.ClearRenderTarget(false,true,Color.clear);
        foreach(var caster in scenery)if(caster.source!=null&&caster.source.sprite!=null)Draw(caster.source,caster.heightOffset,false,0,0);
        foreach(var box in contacts)
        {
            if(box==null)continue;
            var b=box.bounds;var mesh=ContactEdges(b);contactMeshes.Add(mesh);
            properties.Clear();properties.SetMatrix("_ShadowVP",vp);commands.DrawMesh(mesh,Matrix4x4.identity,contact,0,0,properties);
        }
        foreach(var bounds in decorativeContacts){var mesh=ContactEdges(new Bounds(new Vector3(bounds.x,bounds.y,0),new Vector3(bounds.z,bounds.w,0)));contactMeshes.Add(mesh);properties.Clear();properties.SetMatrix("_ShadowVP",vp);commands.DrawMesh(mesh,Matrix4x4.identity,contact,0,0,properties);}
        Graphics.ExecuteCommandBuffer(commands);StaticBuildCount++;
    }
    void Draw(SpriteRenderer sr,float offset,bool volume,float slope,float lineOffset)
    {
        var sprite=sr.sprite;var cache=volume?volumes:flat;
        if(!cache.TryGetValue(sprite,out var mesh))
        {
            if(volume)mesh=SilhouetteVolumeMesh.Build(sprite);
            else
            {
                mesh=new Mesh{name="City flat silhouette",hideFlags=HideFlags.DontSave};
                mesh.vertices=sprite.vertices.Select(v=>new Vector3(v.x,v.y)).ToArray();mesh.uv=sprite.uv;
                mesh.triangles=sprite.triangles.Select(v=>(int)v).ToArray();mesh.RecalculateBounds();
            }
            cache.Add(sprite,mesh);
        }
        if(!uvBounds.TryGetValue(sprite,out var uv))
        {var a=sprite.uv;uv=new Vector4(a.Min(v=>v.x),a.Min(v=>v.y),a.Max(v=>v.x),a.Max(v=>v.y));uvBounds.Add(sprite,uv);}
        Vector2 scale=new(sr.transform.TransformVector(Vector3.right).x*(sr.flipX?-1:1),sr.transform.TransformVector(Vector3.up).y*(sr.flipY?-1:1));
        var foot=sr.transform.position-Vector3.up*offset;
        properties.Clear();properties.SetTexture("_MainTex",sprite.texture);properties.SetMatrix("_ShadowVP",vp);
        properties.SetVector("_Anchor",foot);properties.SetVector("_FootLocal",new Vector4(0,-offset,0,0));
        properties.SetVector("_Scale",scale);properties.SetVector("_HeightBasis",sunlightProjection);
        properties.SetVector("_ContactLine",new Vector4(slope,lineOffset,0,0));properties.SetVector("_UVBounds",uv);
        float depth=volume?sprite.bounds.size.x*SilhouetteVolumeMesh.DepthToWidth*.5f:0;
        properties.SetFloat("_VolumeHalfDepth",depth*Mathf.Abs(scale.x));properties.SetFloat("_DepthPixels",depth*sprite.pixelsPerUnit);
        properties.SetFloat("_Weight",.65f);commands.DrawMesh(mesh,Matrix4x4.identity,projection,0,0,properties);
    }
    static Mesh ContactEdges(Bounds b)
    {
        var vertices=new List<Vector3>();var tris=new List<int>();
        void Rect(float x,float y,float w,float h)
        {
            int n=vertices.Count;vertices.Add(new(x,y));vertices.Add(new(x+w,y));vertices.Add(new(x+w,y+h));vertices.Add(new(x,y+h));
            tris.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
        }
        const float edge=2f/32;
        if(b.size.x<.8f)
        {Rect(b.min.x-edge,b.min.y-edge,b.size.x+edge*2,Mathf.Min(b.size.y,.2f)+edge*2);}
        else
        {
            Rect(b.min.x-edge,b.min.y-edge,b.size.x+edge*2,edge*2);
            Rect(b.min.x-edge,b.min.y,edge*2,b.size.y);
            Rect(b.max.x-edge,b.min.y,edge*2,b.size.y);
        }
        var mesh=new Mesh{name="City ground contact",hideFlags=HideFlags.DontSave};mesh.SetVertices(vertices);mesh.SetTriangles(tris,0);mesh.RecalculateBounds();return mesh;
    }
    static Mesh Ellipse(float rx,float ry)
    {
        var vertices=new List<Vector3>{Vector3.zero};var tris=new List<int>();
        for(int i=0;i<16;i++){float a=i*Mathf.PI*2/16;vertices.Add(new Vector3(Mathf.Round(Mathf.Cos(a)*rx*32)/32,Mathf.Round(Mathf.Sin(a)*ry*32)/32));}
        for(int i=0;i<16;i++)tris.AddRange(new[]{0,i+1,(i+1)%16+1});
        var mesh=new Mesh{name="City actor contact",hideFlags=HideFlags.DontSave};mesh.SetVertices(vertices);mesh.SetTriangles(tris,0);mesh.RecalculateBounds();return mesh;
    }
    void OnDisable()
    {
        Shader.SetGlobalColor("_CityDaylightTint",Color.white);
        if(overlay!=null&&overlay.sharedMaterial==receiver)overlay.sharedMaterial=originalOverlay;
        commands?.Release();commands=null;
        if(mask!=null){mask.Release();Destroy(mask);mask=null;}if(staticMask!=null){staticMask.Release();Destroy(staticMask);staticMask=null;}
        foreach(var mesh in flat.Values)Destroy(mesh);foreach(var mesh in volumes.Values)Destroy(mesh);foreach(var mesh in contactMeshes)Destroy(mesh);
        flat.Clear();volumes.Clear();uvBounds.Clear();contactMeshes.Clear();
        if(footMesh!=null)Destroy(footMesh);if(projection!=null)Destroy(projection);if(contact!=null)Destroy(contact);if(receiver!=null)Destroy(receiver);
    }
}
