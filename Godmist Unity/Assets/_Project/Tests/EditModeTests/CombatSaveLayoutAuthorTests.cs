using System;
using System.IO;
using System.Linq;
using Godmist.Views.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Godmist.Tests.EditMode
{
    public sealed class CombatSaveLayoutAuthorTests
    {
        [Serializable] sealed class State {public string[] ids,values;}
        static State Capture(Scene scene)
        {
            var components=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Component>(true))
                .Where(c=>c!=null).OrderBy(c=>GlobalObjectId.GetGlobalObjectIdSlow(c).ToString()).ToArray();
            return new State {ids=components.Select(c=>GlobalObjectId.GetGlobalObjectIdSlow(c).ToString()).ToArray(),
                values=components.Select(c=>EditorJsonUtility.ToJson(c)).ToArray()};
        }
        [Test,Explicit("Native authoring of the existing pause reason label in both production scenes.")]
        public void AuthorPauseReasonInsidePanel()
        {
            var folder=Path.GetFullPath("../LocalHandoffs/combat-save-20261103");Directory.CreateDirectory(folder);
            foreach(var path in GodmistMainMenuSetup.PauseScenes)
            {
                var name=Path.GetFileNameWithoutExtension(path);var beforePath=Path.Combine(folder,name+"-layout-before.json");
                Assert.That(File.Exists(beforePath),Is.False,"Keep the original before-state evidence.");
                var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                var pause=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PauseMenuView>(true)).Single();
                var before=Capture(scene);File.WriteAllText(beforePath,JsonUtility.ToJson(before,true));
                GodmistMainMenuSetup.AuthorPauseReason(pause);
                EditorSceneManager.MarkSceneDirty(scene);Assert.That(EditorSceneManager.SaveScene(scene),Is.True);
                var after=Capture(scene);File.WriteAllText(Path.Combine(folder,name+"-layout-after.json"),JsonUtility.ToJson(after,true));
                Assert.That(after.ids,Is.EqualTo(before.ids),"The update must preserve every scene component identity.");
                GodmistMainMenuSetup.AuthorPauseReason(pause);
                Assert.That(Capture(scene).values,Is.EqualTo(after.values),"Focused authoring must be idempotent.");
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }
    }
}
