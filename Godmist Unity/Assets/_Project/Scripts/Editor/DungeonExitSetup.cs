using System;
using System.IO;
using Godmist.Views.Dungeons;
using Godmist.Views.UI.Workspace;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    [InitializeOnLoad]
    public static class DungeonExitSetup
    {
        private static readonly string Request=Path.GetFullPath(Path.Combine(Application.dataPath,"../../LocalHandoffs/dungeon-exit-20261023/apply.request"));
        static DungeonExitSetup()=>EditorApplication.update+=Tick;
        private static void Tick()
        {
            if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorUtility.scriptCompilationFailed)return;
            File.Delete(Request);
            try
            {
                ForestExitGateSpriteImporter.ApplyProfiles();
                const string path="Assets/_Project/Prefabs/Dungeon/PF_DungeonExitView.prefab";
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var state=new SerializedObject(root.GetComponent<DungeonExitView>());
                    state.FindProperty("confirmationPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<ItemActionPopupView>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_ItemActionPopup.prefab");
                    state.ApplyModifiedPropertiesWithoutUndo();
                    foreach(var label in root.GetComponentsInChildren<TMPro.TMP_Text>(true)) UnityEngine.Object.DestroyImmediate(label.gameObject);
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
                File.WriteAllText(Path.ChangeExtension(Request,".state.txt"),"APPLIED "+DateTime.UtcNow.ToString("O"));
            }
            catch(Exception e){File.WriteAllText(Path.ChangeExtension(Request,".state.txt"),e.ToString());Debug.LogException(e);}
        }
    }
}
