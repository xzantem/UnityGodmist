using System;
using System.IO;
using System.Linq;
using Godmist.Data.Dungeons;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Godmist.Editor
{
    public static class ExplorationPresentationSetup
    {
        public const string Output="../LocalHandoffs/exploration-presentation-20261026";
        public const string HedgePath="Assets/_Project/Art/ForestPrototype/hedge-segments-v2.png";
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before importing presentation assets.");
            Directory.CreateDirectory(Output);
            foreach(var file in new[]{CombatEconomyPolishSetup.TrackerPath,"Assets/_Project/ScriptableObjects/Dungeons/Forest/DVP_Forest_Prototype.asset"})
            {
                var backup=Path.Combine(Output,"serialized-before",file);Directory.CreateDirectory(Path.GetDirectoryName(backup));
                if(!File.Exists(backup))File.Copy(file,backup);
            }
            ImportHedges();CombatEconomyPolishSetup.BuildTracker();
        }
        static void ImportHedges()
        {
            var source=new Texture2D(2,2,TextureFormat.RGBA32,false);var atlas=new Texture2D(128,128,TextureFormat.RGBA32,false);
            try {
                source.LoadImage(File.ReadAllBytes(Path.Combine(Output,"hedge-atlas-source.png")));
                var result=new Color32[128*128];var pixels=source.GetPixels32();
                for(var index=0;index<16;index++)
                {
                    var column=index%4;var row=index/4;
                    var left=column*source.width/4;var right=(column+1)*source.width/4;
                    var bottom=(3-row)*source.height/4;var top=(4-row)*source.height/4;
                    var x0=right;var x1=left;var y0=top;var y1=bottom;
                    for(var y=bottom;y<top;y++)for(var x=left;x<right;x++)if(pixels[y*source.width+x].a>127)
                    {x0=Math.Min(x0,x);x1=Math.Max(x1,x);y0=Math.Min(y0,y);y1=Math.Max(y1,y);}
                    if(x1<x0)throw new InvalidOperationException("Empty generated hedge cell "+index);
                    var w=x1-x0+1;var h=y1-y0+1;
                    // Straight north/south segments use the interior leaf canopy.
                    // Exposed stems and rounded ends belong to endpoint sprites,
                    // otherwise a long run repeats a separate shrub every cell.
                    if(index is 1 or 13) {y0+=h/5;h=h*7/10;}
                    var targetW=32;var targetH=32;
                    if(index is 0 or 3 or 14)targetH=16;
                    if(index is 1 or 13)targetW=16;
                    if(index is 2 or 15)targetW=targetH=20;
                    var targetX=column*32+(32-targetW)/2;var targetY=(3-row)*32+(32-targetH)/2;
                    for(var y=0;y<targetH;y++)for(var x=0;x<targetW;x++)
                    {
                        var c=pixels[(y0+Math.Min(h-1,(int)((y+.5f)*h/targetH)))*source.width+x0+Math.Min(w-1,(int)((x+.5f)*w/targetW))];
                        c.a=c.a>=128?(byte)255:(byte)0;result[(targetY+y)*128+targetX+x]=c;
                    }
                }
                // Match the wall graph exactly. Reuse authored horizontal and
                // vertical leaf texture for each arm, without rotating pixels
                // or trusting a generated junction's orientation.
                var masks=new[]{(4,10),(5,6),(6,5),(7,9),(8,11),(9,14),(10,7),(11,13),(12,15)};
                foreach(var (index,mask) in masks)
                {
                    var targetX=index%4*32;var targetY=(3-index/4)*32;
                    for(var y=0;y<32;y++)for(var x=0;x<32;x++)
                    {
                        var c=new Color32(0,0,0,0);
                        if((y>=16&&(mask&8)!=0)||(y<16&&(mask&4)!=0))c=result[(96+y)*128+32+x];
                        var horizontal=result[(96+y)*128+x];
                        if(((x>=16&&(mask&2)!=0)||(x<16&&(mask&1)!=0))&&horizontal.a>0)c=horizontal;
                        result[(targetY+y)*128+targetX+x]=c;
                    }
                }
                atlas.SetPixels32(result);atlas.Apply();File.WriteAllBytes(HedgePath,atlas.EncodeToPNG());
            } finally {Object.DestroyImmediate(source);Object.DestroyImmediate(atlas);}
            AssetDatabase.ImportAsset(HedgePath,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(HedgePath);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
            importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;
            var sprites=new SpriteMetaData[16];
            for(var i=0;i<16;i++)sprites[i]=new SpriteMetaData{name="hedge_"+i.ToString("00"),rect=new Rect(i%4*32,(3-i/4)*32,32,32),alignment=9,pivot=new Vector2(.5f,.5f)};
            importer.spritesheet=sprites;importer.SaveAndReimport();
            var definition=AssetDatabase.LoadAssetAtPath<DungeonVegetationPresentation>("Assets/_Project/ScriptableObjects/Dungeons/Forest/DVP_Forest_Prototype.asset");
            var data=new SerializedObject(definition);var refs=data.FindProperty("<LowHedgeSegments>k__BackingField");refs.arraySize=16;
            var imported=AssetDatabase.LoadAllAssetsAtPath(HedgePath).OfType<Sprite>().OrderBy(s=>s.name).ToArray();
            if(imported.Length!=16)throw new InvalidOperationException("Hedge importer did not expose sixteen segments.");
            for(var i=0;i<16;i++)refs.GetArrayElementAtIndex(i).objectReferenceValue=imported[i];
            data.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(definition);
        }
    }
}
