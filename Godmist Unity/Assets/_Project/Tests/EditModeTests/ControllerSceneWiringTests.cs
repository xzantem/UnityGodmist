using System.Collections.Generic;
using System.Linq;
using Godmist.Editor;
using Godmist.Views.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Godmist.Tests.EditMode
{
    public sealed class ControllerSceneWiringTests
    {
        [TestCase("Assets/_Project/Scenes/Bootstrap/Bootstrap.unity")]
        [TestCase("Assets/_Project/Scenes/City/Arungard.unity")]
        [TestCase("Assets/_Project/Scenes/Sandbox/DungeonSandbox.unity")]
        [TestCase("Assets/_Project/Scenes/Sandbox/CombatSandbox.unity")]
        [TestCase("Assets/_Project/Scenes/Sandbox/ExplorationSandbox.unity")]
        public void ProductionSceneHasOneWiredInputOwnerAndOwnedUiScopes(string path)
        {
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
                Assert.That(all.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)), Is.Zero);
                var owners = all.Select(t => t.GetComponent<ControllerInputView>()).Where(c => c != null).ToArray();
                Assert.That(owners.Length, Is.EqualTo(1));
                var owner = owners[0];
                var data = new SerializedObject(owner);
                Assert.That(data.FindProperty("actions").objectReferenceValue,
                    Is.SameAs(AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControllerSupportSetup.ActionsPath)));
                Assert.That(data.FindProperty("events").objectReferenceValue, Is.Not.Null);
                Assert.That(data.FindProperty("keyboardView").objectReferenceValue, Is.Not.Null);
                Assert.That(data.FindProperty("cursor").objectReferenceValue, Is.Not.Null);
                var selectors = (Godmist.Views.UI.Workspace.PartyRailMenuView)data.FindProperty("selectorMenu").objectReferenceValue;
                Assert.That(selectors, Is.Not.Null);
                Assert.That(selectors.gameObject.activeSelf, Is.False);
                Assert.That(selectors.GetComponent<ControllerNavigationScope>().IsModal, Is.True);
                if (all.Any(t => t.GetComponent<CombatActionButtonsView>() != null))
                {
                    Assert.That(data.FindProperty("combatActions").objectReferenceValue, Is.Not.Null);
                    foreach (var buttons in all.Select(t => t.GetComponent<CombatActionButtonsView>()).Where(value => value != null))
                        Assert.That(new SerializedObject(buttons).FindProperty("targetSelectionController").objectReferenceValue,
                            Is.SameAs(data.FindProperty("targeting").objectReferenceValue));
                }
                var module = (InputSystemUIInputModule)data.FindProperty("module").objectReferenceValue;
                Assert.That(module, Is.Not.Null);
                Assert.That(module.actionsAsset, Is.SameAs(data.FindProperty("actions").objectReferenceValue));
                foreach (var action in new[] { module.point, module.move, module.submit, module.cancel,
                             module.leftClick, module.rightClick, module.middleClick, module.scrollWheel })
                    Assert.That(action.action.actionMap.asset, Is.SameAs(module.actionsAsset), action.name);
                Assert.That(module.submit.action.name, Is.EqualTo("KeyboardSubmit"));
                Assert.That(module.move.action.name, Is.EqualTo("KeyboardNavigate"));
                Assert.That(module.pointerBehavior, Is.EqualTo(UIPointerBehavior.SingleUnifiedPointer));
                foreach (var scope in all.Select(t => t.GetComponent<ControllerNavigationScope>()).Where(c => c != null))
                    Assert.That(scope.GetComponentInParent<ControllerInputView>(), Is.SameAs(owner), scope.name);
                foreach (var canvas in all.Select(t => t.GetComponent<Canvas>()).Where(c => c != null && c.isRootCanvas && c.renderMode != RenderMode.WorldSpace))
                    Assert.That(canvas.GetComponentInParent<ControllerInputView>(), Is.SameAs(owner), canvas.name);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void UiPrefabsHaveNoDuplicateNavigationAndFocusDoesNotParticipateInLayout()
        {
            var errors = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/UI" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.GetComponent<CombatConsumableSelectionView>() != null
                        && transform.GetComponent<Godmist.Views.UI.Workspace.UIWorkspaceWindow>() == null
                        && transform.GetComponent<ControllerNavigationScope>() != null)
                        errors.Add(path + ": content helper must not own modal input focus");
                    if (transform.GetComponents<ControllerNavigationItem>().Length > 1) errors.Add(path + ": duplicate adapter on " + transform.name);
                    var frames = transform.Cast<Transform>().Where(t => t.name == "ControllerFocus").ToArray();
                    if (frames.Length > 1) errors.Add(path + ": duplicate focus frame on " + transform.name);
                    foreach (var frame in frames)
                    {
                        if (frame.GetComponent<LayoutElement>()?.ignoreLayout != true) errors.Add(path + ": focus changes layout on " + transform.name);
                        if (frame.GetComponent<Graphic>()?.raycastTarget != false) errors.Add(path + ": focus intercepts clicks on " + transform.name);
                    }
                }
            }
            Assert.That(errors, Is.Empty, string.Join("\n", errors));
        }

        [Test]
        public void KeyboardUsesModalScopeAndHasAllAuthoredKeysAndTargets()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Bootstrap/Bootstrap.unity");
            try
            {
                var keyboard = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ControllerKeyboardView>(true)).Single();
                Assert.That(keyboard.gameObject.activeSelf, Is.False);
                Assert.That(keyboard.GetComponent<ControllerNavigationScope>().IsModal, Is.True);
                var tokens = new HashSet<string>();
                foreach (var key in keyboard.GetComponentsInChildren<ControllerKeyboardKey>(true))
                {
                    var data = new SerializedObject(key);
                    Assert.That(data.FindProperty("keyboard").objectReferenceValue, Is.SameAs(keyboard));
                    Assert.That(data.FindProperty("label").objectReferenceValue, Is.Not.Null);
                    tokens.Add(data.FindProperty("token").stringValue);
                }
                foreach (var token in new[] { "a", "ą", "ź", "0", "9", "shift", "space", "backspace", "clear" })
                    Assert.That(tokens.Contains(token), Is.True, token);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
