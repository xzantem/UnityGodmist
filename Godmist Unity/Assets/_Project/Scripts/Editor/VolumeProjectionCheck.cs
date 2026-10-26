using System;
using System.IO;
using System.Linq;
using Godmist.Views.Lighting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class VolumeProjectionCheck
{
    public static void Run()
    {
        var sprite=AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Characters/Sprites/VS22/jerzy-front-64-v1.png").OfType<Sprite>().First();
        var mesh=SilhouetteVolumeMesh.Build(sprite);
        var material=new Material(Shader.Find("Godmist/Dungeon/Animated Silhouette Volume Mask"));
        var target=new RenderTexture(256,256,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear) {filterMode=FilterMode.Point};
        target.Create(); var commands=new CommandBuffer();
        var block=new MaterialPropertyBlock();
        var uv=sprite.uv; float depth=sprite.bounds.size.x*SilhouetteVolumeMesh.DepthToWidth*.5f;
        block.SetTexture("_MainTex",sprite.texture);
        block.SetMatrix("_ShadowVP",GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-4,4,-4,4,-1,1),true));
        block.SetVector("_FootLocal",new Vector4(0,sprite.bounds.min.y,0,0));
        block.SetVector("_Scale",Vector4.one);
        block.SetVector("_ContactLine",Vector4.zero);
        block.SetVector("_Anchor",Vector4.zero);
        block.SetFloat("_VolumeHalfDepth",depth); block.SetFloat("_DepthPixels",depth*sprite.pixelsPerUnit);
        block.SetVector("_UVBounds",new Vector4(uv.Min(p=>p.x),uv.Min(p=>p.y),uv.Max(p=>p.x),uv.Max(p=>p.y)));
        var combined=new Texture2D(256*4,256*2,TextureFormat.RGBA32,false);
        var previous=RenderTexture.active;
        try
        {
            for(int angle=0;angle<8;angle++)
            {
                float radians=angle*Mathf.PI/4;
                block.SetVector("_HeightBasis",new Vector4(Mathf.Cos(radians),Mathf.Sin(radians)*.75f,0,0));
                commands.Clear(); commands.SetRenderTarget(target); commands.ClearRenderTarget(false,true,Color.black);
                commands.DrawMesh(mesh,Matrix4x4.identity,material,0,0,block);
                Graphics.ExecuteCommandBuffer(commands); RenderTexture.active=target;
                var tex=new Texture2D(256,256,TextureFormat.RGBA32,false); tex.ReadPixels(new Rect(0,0,256,256),0,0); tex.Apply();
                var pixels=tex.GetPixels32(); int area=pixels.Count(p=>p.r>127);
                if(area<30) throw new Exception("Volume projection collapsed at angle "+angle*45+" area="+area);
                if(angle==0)
                {
                    block.SetFloat("_VolumeHalfDepth",0);
                    commands.Clear(); commands.SetRenderTarget(target); commands.ClearRenderTarget(false,true,Color.black);
                    commands.DrawMesh(mesh,Matrix4x4.identity,material,0,0,block); Graphics.ExecuteCommandBuffer(commands);
                    tex.ReadPixels(new Rect(0,0,256,256),0,0); tex.Apply();
                    int planarArea=tex.GetPixels32().Count(p=>p.r>127);
                    if(planarArea>=area/2) throw new Exception("Test failed to reproduce edge-on planar collapse");
                    Debug.Log("PLANAR COLLAPSE REPRODUCED area="+planarArea+" volume="+area);
                    block.SetFloat("_VolumeHalfDepth",depth);
                }
                combined.SetPixels32(angle%4*256,angle/4*256,256,256,pixels);
                Debug.Log("VOLUME ANGLE PASS degrees="+angle*45+" area="+area);
                UnityEngine.Object.DestroyImmediate(tex);
            }
            combined.Apply(); Directory.CreateDirectory("../output/asset-review/realtime-shadows/dungeon");
            File.WriteAllBytes("../output/asset-review/realtime-shadows/dungeon/volume-eight-angles.png",combined.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active=previous; commands.Release(); target.Release();
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(combined);
            UnityEngine.Object.DestroyImmediate(material); UnityEngine.Object.DestroyImmediate(mesh);
        }
    }
}
