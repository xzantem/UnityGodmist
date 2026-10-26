using System.Linq;
using System.Reflection;
using Godmist.Views.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Godmist.Core.Enums;

namespace Godmist.Tests.EditMode
{
    public partial class GlossaryMechanicsTests
    {
        [Test]
        public void RageAuthoredAndFallbackExplainGraceAndControlExceptions()
        {
            var glossary=AssetDatabase.LoadAssetAtPath<CombatTooltipGlossary>("Assets/_Project/ScriptableObjects/UI/CombatTooltipGlossary.asset");
            Assert.That(glossary.TryGet("rage",out var authored),Is.True);
            var fallback=CombatTooltipGlossary.BuiltInEntries.Single(e=>e.Id=="rage");
            Assert.That(authored.Description,Is.EqualTo(fallback.Description));
            foreach(var phrase in new[]{"Pierwsza pełna", "na końcu drugiej", "resetuje licznik", "nie zwiększa licznika", "unieruchomienia", "6 sekund"})
                Assert.That(authored.Description,Does.Contain(phrase));
        }

        [TestCase("mana", "pozostała")]
        [TestCase("rage", "+10")]
        [TestCase("momentum", "połową")]
        [TestCase("momentum", "+2, +4, +6")]
        [TestCase("momentum", "maksymalnie 50")]
        [TestCase("momentum", "6 sekund")]
        [TestCase("momentum", "tura bez ruchu odbiera 10")]
        [TestCase("speed", "częstotliwość")]
        [TestCase("downed", "1 HP")]
        [TestCase("status-tolerance", "współdzielą")]
        [TestCase("dot-resistance", "×0,5")]
        [TestCase("rear-attack", "90°")]
        [TestCase("resistance", "DoT")]
        public void ProductionGlossaryExplainsActualMechanics(string id, string expected)
        {
            var glossary = AssetDatabase.LoadAssetAtPath<CombatTooltipGlossary>("Assets/_Project/ScriptableObjects/UI/CombatTooltipGlossary.asset");
            Assert.That(glossary.TryGet(id, out var entry), Is.True);
            Assert.That(entry.Description, Does.Contain(expected));
        }

        [Test]
        public void ProductionEntriesHaveDescriptionsAndRetainStatusIcons()
        {
            var glossary = AssetDatabase.LoadAssetAtPath<CombatTooltipGlossary>("Assets/_Project/ScriptableObjects/UI/CombatTooltipGlossary.asset");
            var serialized = new SerializedObject(glossary); var entries = serialized.FindProperty("entries");
            for (var i=0; i<entries.arraySize; i++)
                Assert.That(entries.GetArrayElementAtIndex(i).FindPropertyRelative("Description").stringValue, Is.Not.Null.And.Not.Empty,
                    entries.GetArrayElementAtIndex(i).FindPropertyRelative("Id").stringValue);
            Assert.That(glossary.TryGet("bleed", out var bleed), Is.True);
            Assert.That(bleed.Icon, Is.Not.Null);
        }

        [TestCase("Regeneracja many", "resource-regeneration")]
        [TestCase("regeneracji zasobu", "resource-regeneration")]
        [TestCase("Regeneracja zdrowia", "regeneration")]
        [TestCase("DoT", "dot")]
        public void AmbiguousTermsLinkToTheRightMechanic(string text, string id)
        {
            var glossary = AssetDatabase.LoadAssetAtPath<CombatTooltipGlossary>("Assets/_Project/ScriptableObjects/UI/CombatTooltipGlossary.asset");
            var linked=glossary.Linkify(text);
            Assert.That(linked, Does.Contain("<link=\""+id+"\">"));
            if(id=="resource-regeneration") Assert.That(linked, Does.Not.Contain("<link=\"regeneration\">"));
        }

        [TestCase(ResourceType.Mana, "mana")]
        [TestCase(ResourceType.Rage, "rage")]
        [TestCase(ResourceType.Momentum, "momentum")]
        public void ResourceBarDoesNotOpenAnAdditionalTooltip(ResourceType type,string id)
        {
            var go=new GameObject("resource tooltip test");
            try
            {
                var trigger=go.AddComponent<CombatSimpleTooltipTrigger>();
                var tooltip=go.AddComponent<Godmist.Views.UI.Workspace.UIWorkspaceTooltipView>();
                trigger.InitializeResource(tooltip,new ResourcePool(type,12,100,15));
                trigger.OnPointerEnter(null);
                Assert.That(typeof(Godmist.Views.UI.Workspace.UIWorkspaceTooltipView).GetField("pendingShow",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(tooltip),Is.Null);
                Assert.That(typeof(Godmist.Views.UI.Workspace.UIWorkspaceTooltipView).GetField("visible",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(tooltip),Is.False);
            }
            finally { Object.DestroyImmediate(go); }
        }
        [TestCase("Assets/_Project/Prefabs/UI/CombatHUD/PF_CombatPartyMember.prefab")]
        [TestCase("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatHud.prefab")]
        [TestCase("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatVitals.prefab")]
        public void ProductionResourceBarsRetainCompatibleTriggerWiring(string path)
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);var checkedBars=0;
            foreach(var view in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(view==null)continue;
                var property=new SerializedObject(view).FindProperty("resourceSlider");
                if(property?.objectReferenceValue is not UnityEngine.UI.Slider slider)continue;
                Assert.That(slider.GetComponent<CombatSimpleTooltipTrigger>(),Is.Not.Null,path);
                Assert.That(slider.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Any(g=>g.raycastTarget),Is.True,path);
                checkedBars++;
            }
            Assert.That(checkedBars,Is.GreaterThan(0));
        }

    }
}
