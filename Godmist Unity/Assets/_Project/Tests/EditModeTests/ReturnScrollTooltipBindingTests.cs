using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class ReturnScrollTooltipBindingTests
    {
        [Serializable] class State { public string[] ids, values; }
        static State Capture(GameObject root)
        {
            var components = root.GetComponentsInChildren<Component>(true).Where(c => c != null).ToArray();
            return new State { ids = components.Select(c => GlobalObjectId.GetGlobalObjectIdSlow(c).ToString()).ToArray(),
                values = components.Select(EditorJsonUtility.ToJson).ToArray() };
        }

        [Test, Explicit("Native currency-library repair at the owning Combat HUD prefab; no scene overrides.")]
        public void AuthorCombatHudTooltipCurrencyReference()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EquipmentTooltipUiSetup.CombatHudPath);
            Assert.That(prefab, Is.Not.Null);
            var folder = Path.GetFullPath("../LocalHandoffs/return-scroll-20261103");
            Directory.CreateDirectory(folder);
            var before = Capture(prefab);
            File.WriteAllText(Path.Combine(folder, "binding-prefab-before.json"), JsonUtility.ToJson(before, true));
            EquipmentTooltipUiSetup.ConfigureCombatHudCurrency();
            var after = Capture(prefab);
            CollectionAssert.AreEqual(before.ids, after.ids, "No hierarchy or component identity changes.");
            Assert.That(before.values.Zip(after.values, (a, b) => a != b).Count(v => v), Is.EqualTo(1));
            File.WriteAllText(Path.Combine(folder, "binding-prefab-after.json"), JsonUtility.ToJson(after, true));
            EquipmentTooltipUiSetup.ConfigureCombatHudCurrency();
            CollectionAssert.AreEqual(Capture(prefab).values, after.values, "Idempotent prefab authoring.");
        }
    }
}
