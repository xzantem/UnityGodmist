using System.IO;
using Godmist.Data.Characters;
using NUnit.Framework;
using UnityEditor;
namespace Godmist.Tests.EditMode
{
    public sealed class EnemyAttackDataMigrationTests
    {
        [Test]
        public void EveryDefinitionUsesDirectFractionalAttackDataWithoutTheObsoleteMultiplier()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:CharacterDefinition", new[] { "Assets/_Project/ScriptableObjects" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
                var data = new SerializedObject(definition);
                Assert.That(data.FindProperty("<AttackMultiplier>k__BackingField"), Is.Null, path);
                Assert.That(File.ReadAllText(path), Does.Not.Contain("<AttackMultiplier>"), path);
                Assert.That(data.FindProperty("<BaseStats>k__BackingField").FindPropertyRelative("<MinAttack>k__BackingField").propertyType,
                    Is.EqualTo(SerializedPropertyType.Float));
            }
        }
    }
}
