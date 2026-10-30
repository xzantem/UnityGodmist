using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Views.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace Godmist.Editor
{
    public static class BarFillMaskSetup
    {
        public const string PrefabFolder = "Assets/_Project/Prefabs/UI";
        private static string Snapshot(GameObject root) => string.Join("\n",root.GetComponentsInChildren<Component>(true)
            .Where(component=>component!=null).Select(EditorJsonUtility.ToJson));
        private static bool IsBarSprite(Image image) => image!=null && image.sprite!=null && image.sprite.name is "slider_track" or "bar_frame";
        public static List<string> Apply(string evidence)
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode before authoring bar masks.");
            Directory.CreateDirectory(evidence);var changed=new List<string>();
            var paths=AssetDatabase.FindAssets("t:Prefab",new[]{PrefabFolder}).Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path=>path.EndsWith("PF_UIW_Slider.prefab",StringComparison.Ordinal)?0:1).ThenBy(path=>path).ToList();
            foreach(var path in paths)
            {
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var before=Snapshot(root);
                    // A base slider can acquire the component after an older variant was fixed.
                    foreach(var node in root.GetComponentsInChildren<Transform>(true))
                    {
                        var masks=node.GetComponents<UIBarFillMask>();
                        if(masks.Length>1)foreach(var duplicate in masks.Where(PrefabUtility.IsAddedComponentOverride))UnityEngine.Object.DestroyImmediate(duplicate);
                        var clips=node.GetComponents<RectMask2D>();
                        if(clips.Length>1)foreach(var duplicate in clips.Where(PrefabUtility.IsAddedComponentOverride))UnityEngine.Object.DestroyImmediate(duplicate);
                    }
                    foreach(var slider in root.GetComponentsInChildren<Slider>(true))
                    {
                        if(slider.fillRect==null || slider.handleRect!=null)continue;
                        var background=slider.transform.Find("Background")?.GetComponent<Image>();
                        var fill=slider.fillRect.GetComponent<Image>();
                        var border=slider.transform.Find("GothicBarFrame")?.GetComponent<Image>();
                        if(background==null || fill==null)continue;
                        if(border!=null && IsBarSprite(border) && (border.enabled || background.sprite==null || background.sprite.name=="bar_background"))
                        {background.sprite=border.sprite;background.color=Color.white;}
                        if(!IsBarSprite(background))continue;
                        UIBarFillMask.Install(background,fill,slider.fillRect.parent as RectTransform,border);
                    }
                    foreach(var track in root.GetComponentsInChildren<Image>(true))
                    {
                        if(!IsBarSprite(track))continue;
                        var fill=track.transform.Find("Fill")?.GetComponent<Image>();
                        if(fill!=null)UIBarFillMask.Install(track,fill);
                    }
                    foreach(var view in root.GetComponentsInChildren<CombatVitalsView>(true))
                    {
                        var data=new SerializedObject(view);
                        var health=(Slider)data.FindProperty("healthSlider").objectReferenceValue;
                        var shield=(Slider)data.FindProperty("shieldSlider").objectReferenceValue;
                        var mask=health!=null?health.GetComponent<UIBarFillMask>():null;
                        if(shield==null||mask==null)continue;
                        mask.SetShieldOverlay((RectTransform)shield.transform);
                        var shieldPresentation=shield.GetComponent<UIBarFillMask>();
                        if(shieldPresentation!=null)shieldPresentation.enabled=false;
                        var area=shield.fillRect?.parent as RectTransform;
                        if(area!=null){if(area.GetComponent<RectMask2D>()==null)area.gameObject.AddComponent<RectMask2D>();area.anchorMin=Vector2.zero;area.anchorMax=Vector2.one;area.offsetMin=area.offsetMax=Vector2.zero;}
                        foreach(var image in shield.GetComponentsInChildren<Image>(true))
                            if(image.rectTransform!=shield.fillRect)image.enabled=false;
                    }
                    foreach(var mask in root.GetComponentsInChildren<UIBarFillMask>(true))if(mask.enabled)mask.Apply();
                    if(before==Snapshot(root))continue;
                    var backup=Path.Combine(evidence,path.Replace('/','_'));
                    if(!File.Exists(backup))File.Copy(path,backup);
                    PrefabUtility.SaveAsPrefabAsset(root,path);changed.Add(path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            File.WriteAllLines(Path.Combine(evidence,"changed-prefabs.txt"),changed);
            return changed;
        }
    }
}
