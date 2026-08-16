using Godmist.Data.Characters;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public static class CharacterDefinitionPresetCreator
    {
        [MenuItem("Assets/Create/Godmist/Characters/Character Definition/Empty")]
        private static void CreateEmpty()
        {
            CreatePreset("Empty", string.Empty, new CharacterPreset());
        }

        [MenuItem("Assets/Create/Godmist/Characters/Character Definition/Warrior")]
        private static void CreateWarrior()
        {
            CreatePreset("Warrior", "class.warrior", new CharacterPreset
            {
                MaxHealth = 375f, MinAttack = 24f, MaxAttack = 32f, Dodge = 10f,
                PhysicalDefense = 12f, MagicalDefense = 8f, Speed = 40f, MaxResource = 50f,
                GrowthMaxHealth = 12.5f, GrowthMinAttack = 0.7f, GrowthMaxAttack = 0.95f,
                GrowthDodge = 0.05f, GrowthPhysicalDefense = 0.45f,
                GrowthMagicalDefense = 0.3f
            });
        }

        [MenuItem("Assets/Create/Godmist/Characters/Character Definition/Scout")]
        private static void CreateScout()
        {
            CreatePreset("Scout", "class.scout", new CharacterPreset
            {
                MaxHealth = 300f, MinAttack = 18f, MaxAttack = 36f, Dodge = 17f,
                PhysicalDefense = 8f, MagicalDefense = 4f, Speed = 55f, MaxResource = 200f,
                GrowthMaxHealth = 10f, GrowthMinAttack = 0.8f, GrowthMaxAttack = 1.1f,
                GrowthDodge = 0.08f, GrowthPhysicalDefense = 0.33f,
                GrowthMagicalDefense = 0.2f
            });
        }

        [MenuItem("Assets/Create/Godmist/Characters/Character Definition/Mage")]
        private static void CreateMage()
        {
            CreatePreset("Mage", "class.mage", new CharacterPreset
            {
                MaxHealth = 250f, MinAttack = 27f, MaxAttack = 36f, Dodge = 6f,
                PhysicalDefense = 6f, MagicalDefense = 12f, Speed = 45f,
                GrowthMaxHealth = 7.5f, GrowthMinAttack = 1f, GrowthMaxAttack = 1.3f,
                GrowthDodge = 0.04f, GrowthPhysicalDefense = 0.25f,
                GrowthMagicalDefense = 0.5f
            });
        }

        [MenuItem("Assets/Create/Godmist/Characters/Character Definition/Paladin")]
        private static void CreatePaladin()
        {
            CreatePreset("Paladin", "class.paladin", new CharacterPreset
            {
                MaxHealth = 450f, MinAttack = 21f, MaxAttack = 30f, Dodge = 8f,
                PhysicalDefense = 16f, MagicalDefense = 16f, Speed = 35f,
                GrowthMaxHealth = 20f, GrowthMinAttack = 0.6f, GrowthMaxAttack = 0.8f,
                GrowthDodge = 0.04f, GrowthPhysicalDefense = 0.6f,
                GrowthMagicalDefense = 0.6f
            });
        }

        private static void CreatePreset(
            string presetName,
            string classId,
            CharacterPreset preset)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            var serializedDefinition = new SerializedObject(definition);
            var classData = FindClass(classId);

            serializedDefinition.FindProperty("<Id>k__BackingField").stringValue =
                classId.Length == 0 ? string.Empty : $"character.{presetName.ToLowerInvariant()}";
            serializedDefinition.FindProperty("<DisplayName>k__BackingField").stringValue = presetName;
            serializedDefinition.FindProperty("<ClassData>k__BackingField").objectReferenceValue = classData;
            ApplyBaseStats(serializedDefinition.FindProperty("<BaseStats>k__BackingField"), preset);
            ApplyGrowth(serializedDefinition.FindProperty("<GrowthData>k__BackingField"), preset);
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();

            var path = AssetDatabase.GenerateUniqueAssetPath(
                $"Assets/_Project/ScriptableObjects/Characters/CD_{presetName}.asset");
            AssetDatabase.CreateAsset(definition, path);
            AssetDatabase.SaveAssets();
            ProjectWindowUtil.ShowCreatedAsset(definition);

            if (classId.Length > 0 && classData == null)
            {
                Debug.LogWarning($"Nie znaleziono CharacterClassData o Id '{classId}'. "
                    + "Ustaw Class Data recznie w Inspectorze.", definition);
            }
        }

        private static CharacterClassData FindClass(string classId)
        {
            if (string.IsNullOrWhiteSpace(classId)) return null;

            foreach (var guid in AssetDatabase.FindAssets("t:CharacterClassData"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<CharacterClassData>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && asset.Id == classId) return asset;
            }

            return null;
        }

        private static void ApplyBaseStats(SerializedProperty property, CharacterPreset preset)
        {
            property.FindPropertyRelative("<MaxHealth>k__BackingField").intValue =
                Mathf.RoundToInt(preset.MaxHealth);
            property.FindPropertyRelative("<MinAttack>k__BackingField").intValue =
                Mathf.RoundToInt(preset.MinAttack);
            property.FindPropertyRelative("<MaxAttack>k__BackingField").intValue =
                Mathf.RoundToInt(preset.MaxAttack);
            property.FindPropertyRelative("<PhysicalDefense>k__BackingField").floatValue = preset.PhysicalDefense;
            property.FindPropertyRelative("<MagicalDefense>k__BackingField").floatValue = preset.MagicalDefense;
            property.FindPropertyRelative("<Dodge>k__BackingField").floatValue = preset.Dodge;
            property.FindPropertyRelative("<Speed>k__BackingField").floatValue = preset.Speed;
            property.FindPropertyRelative("<MaxResource>k__BackingField").intValue =
                Mathf.RoundToInt(preset.MaxResource);
        }

        private static void ApplyGrowth(SerializedProperty property, CharacterPreset preset)
        {
            property.FindPropertyRelative("<MaxHealth>k__BackingField").floatValue = preset.GrowthMaxHealth;
            property.FindPropertyRelative("<MinAttack>k__BackingField").floatValue = preset.GrowthMinAttack;
            property.FindPropertyRelative("<MaxAttack>k__BackingField").floatValue = preset.GrowthMaxAttack;
            property.FindPropertyRelative("<PhysicalDefense>k__BackingField").floatValue = preset.GrowthPhysicalDefense;
            property.FindPropertyRelative("<MagicalDefense>k__BackingField").floatValue = preset.GrowthMagicalDefense;
            property.FindPropertyRelative("<Dodge>k__BackingField").floatValue = preset.GrowthDodge;
        }

        private sealed class CharacterPreset
        {
            public float MaxHealth = 100f;
            public float MinAttack = 8f;
            public float MaxAttack = 12f;
            public float PhysicalDefense = 10f;
            public float MagicalDefense = 10f;
            public float Dodge = 15f;
            public float Speed = 50f;
            public float MaxResource = 100f;
            public float GrowthMaxHealth;
            public float GrowthMinAttack;
            public float GrowthMaxAttack;
            public float GrowthPhysicalDefense;
            public float GrowthMagicalDefense;
            public float GrowthDodge;
        }
    }
}
