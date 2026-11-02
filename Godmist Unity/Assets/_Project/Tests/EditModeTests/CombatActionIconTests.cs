using System;
using System.IO;
using System.Linq;
using Godmist.Views.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Tests.EditMode
{
    public sealed class CombatActionIconTests
    {
        [Test, Explicit("Import new action artwork and bind authored prefab/sandbox through Editor API.")]
        public void ApplyCurrentGodmistIcons() => Godmist.Editor.CombatActionIconSetup.Apply();

        [Test] public void SandboxUsesCurrentIconsAndTheDedicatedEscapeState()
        {
            var previous=SceneManager.GetActiveScene();
            var scene=SceneManager.GetSceneByPath(Godmist.Editor.CombatActionIconSetup.Sandbox);
            var open=scene.IsValid() && scene.isLoaded;
            try
            {
                if(!open)scene=EditorSceneManager.OpenScene(Godmist.Editor.CombatActionIconSetup.Sandbox,OpenSceneMode.Additive);
                var views=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CombatActionButtonsView>(true)).ToArray();
                Assert.That(views,Is.Not.Empty);
                foreach(var view in views)
                {
                    var state=new SerializedObject(view);
                    foreach(var field in new[]{"endTurnButton","consumablesButton","escapeButton"})
                    {
                        var button=(Button)state.FindProperty(field).objectReferenceValue;
                        var icon=button.GetComponentsInChildren<Image>(true).Single(i=>i.name=="IconGlyph");
                        var expected=field=="endTurnButton"?"icon_combat_end_turn-32.png":field=="consumablesButton"?"icon_combat_items-32.png":"icon_combat_escape.png";
                        Assert.That(AssetDatabase.GetAssetPath(icon.sprite),Does.EndWith(expected),field);
                        Assert.That(icon.rectTransform.localRotation,Is.EqualTo(Quaternion.identity),field);
                        if(field=="escapeButton")Assert.That(state.FindProperty("escapeIcon").objectReferenceValue,Is.SameAs(icon));
                    }
                    Assert.That(state.FindProperty("escapeDisabledMaterial").objectReferenceValue,Is.Not.Null);
                }
            }
            finally
            {
                if(!open && scene.IsValid())EditorSceneManager.CloseScene(scene,true);
                if(previous.IsValid() && previous.isLoaded)SceneManager.SetActiveScene(previous);
            }
        }

        [Test] public void AuditAuthoredActionIcons()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatActionPanel.prefab");
            Assert.That(prefab,Is.Not.Null);
            var view=prefab.GetComponentInChildren<CombatActionButtonsView>(true);
            var data=new SerializedObject(view);
            var lines=new System.Collections.Generic.List<string>();
            foreach(var field in new[]{"endTurnButton","consumablesButton","escapeButton"})
            {
                var button=(Button)data.FindProperty(field).objectReferenceValue;
                Assert.That(button,Is.Not.Null,field);
                var actionIcon=button.GetComponentsInChildren<Image>(true).Single(i=>i.name=="ActionIcon");
                Assert.That(actionIcon.sprite,Is.Not.Null,field);
                Assert.That(actionIcon.enabled,Is.True,field);
                var expected=field=="endTurnButton"?"icon_combat_end_turn-32.png":field=="consumablesButton"?"icon_combat_items-32.png":"icon_combat_escape.png";
                Assert.That(AssetDatabase.GetAssetPath(actionIcon.sprite),Does.EndWith(expected),field);
                if(field!="escapeButton")Assert.That(actionIcon.rectTransform.localRotation,Is.EqualTo(Quaternion.identity),field);
                Assert.That(actionIcon.rectTransform.rect.size,Is.EqualTo(new Vector2(32,32)),field);
                Assert.That(actionIcon.sprite.texture.filterMode,Is.EqualTo(FilterMode.Point),field);
                var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(actionIcon.sprite));
                Assert.That(importer.mipmapEnabled,Is.False,field);
                Assert.That(importer.textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed),field);
                foreach(var image in button.GetComponentsInChildren<Image>(true))
                    lines.Add(field+" | "+image.name+" | "+image.enabled+" | "+(image.sprite!=null?image.sprite.name:"<none>")+" | "+AssetDatabase.GetAssetPath(image.sprite)+" | "+image.rectTransform.rect.size+" | "+(image.sprite!=null?image.sprite.texture.filterMode.ToString():"<none>"));
            }
            var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../LocalHandoffs/combat-action-icons-refresh-20261102"));
            Directory.CreateDirectory(output);
            File.WriteAllLines(Path.Combine(output,"authored-icons.txt"),lines);
        }
    }
}
