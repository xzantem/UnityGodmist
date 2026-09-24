using System;
using Godmist.Core.Enums;
using Godmist.Data.Quests;
using Godmist.Data.World;
using Godmist.Systems.Quests;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    /// <summary>Idempotent authoring for the three repeatable Catacombs contracts.</summary>
    public static class VS17CatacombsMissionSetup
    {
        private const string Root = "Assets/_Project/ScriptableObjects/";
        private const string QuestPath = Root + "Quests/";
        private static string F(string name) => "<" + name + ">k__BackingField";

        [MenuItem("Godmist/Content/VS-17/Configure Catacombs Missions")]
        public static void Configure()
        {
            var kill = Create("QD_Catacombs_Radiant_Kill", CatacombsMissionBoard.KillQuestId,
                "Oczyść Katakumby",
                "Pokonaj 12 zwykłych nieumarłych w Katakumbach. Posiłki i przywołania nie liczą się do zlecenia.",
                "Pokonaj 12 zwykłych przeciwników Katakumb.",
                QuestObjectiveType.KillEnemy, "dungeon.catacombs", 12);
            var expedition = CreateExpedition();
            var boss = Create("QD_Catacombs_Radiant_Boss",
                CatacombsMissionBoard.BossQuestId,
                "Pokonaj Ducha Umarłego Króla",
                "Zabij Ducha Umarłego Króla i opuść wyprawę. Zlecenie dotyczy poziomu wyprawy dostępnego w chwili przyjęcia.",
                "Pokonaj bossa i ukończ wyprawę.",
                QuestObjectiveType.ExitDungeon, CatacombsMissionBoard.BossDungeonId, 1);

            var catalogPath = Root + "World/GC_MainContentCatalog.asset";
            AssetDatabase.ImportAsset(catalogPath, ImportAssetOptions.ForceSynchronousImport);
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(catalogPath)
                ?? throw new InvalidOperationException("VS-17: brak katalogu zawartości pod "
                    + catalogPath + "; GUID: " + AssetDatabase.AssetPathToGUID(catalogPath)
                    + "; typ assetu: " + AssetDatabase.GetMainAssetTypeAtPath(catalogPath));
            var catalogSo = new SerializedObject(catalog);
            var questDefinitions = catalogSo.FindProperty(F("QuestDefinitions"));
            AddDistinct(questDefinitions, kill);
            AddDistinct(questDefinitions, expedition);
            AddDistinct(questDefinitions, boss);
            catalogSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);

            var intro = AssetDatabase.LoadAssetAtPath<QuestDefinition>(
                QuestPath + "QD_InvestigateCatacombs.asset");
            if (intro != null)
            {
                var introSo = new SerializedObject(intro);
                var objective = introSo.FindProperty(F("Stages"))
                    .GetArrayElementAtIndex(0).FindPropertyRelative(F("Objectives"))
                    .GetArrayElementAtIndex(0);
                objective.FindPropertyRelative(F("TargetId")).stringValue =
                    "dungeon.catacombs.expedition";
                introSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(intro);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("VS-17: skonfigurowano trzy zlecenia Katakumb i poprawiono pierwszą misję.");
        }

        public static void ConfigureAll()
        {
            Configure();
            VS17CatacombsMissionUiSetup.Configure();
        }

        [MenuItem("Godmist/Content/VS-17/Refresh Expedition Mission Text")]
        public static void RefreshExpeditionCopy()
        {
            CreateExpedition();
            AssetDatabase.SaveAssets();
            Debug.Log("VS-17: skrócono nazwę zlecenia ukończenia Katakumb.");
        }

        public static void RefreshMissionPresentation()
        {
            RefreshExpeditionCopy();
            VS17CatacombsMissionUiSetup.RepairLayout();
        }

        private static QuestDefinition CreateExpedition() => Create(
            "QD_Catacombs_Radiant_Expedition", CatacombsMissionBoard.ExpeditionQuestId,
            "Ukończ Katakumby",
            "Dotrzyj do końca wyprawy i opuść Katakumby oficjalnym wyjściem.",
            "Ukończ wyprawę w Katakumbach.",
            QuestObjectiveType.ExitDungeon, "dungeon.catacombs.expedition", 1);

        private static QuestDefinition Create(string file, string id, string title,
            string description, string objectiveText, QuestObjectiveType type,
            string target, int amount)
        {
            var path = QuestPath + file + ".asset";
            var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
            if (quest == null)
            {
                quest = ScriptableObject.CreateInstance<QuestDefinition>();
                quest.name = file;
                AssetDatabase.CreateAsset(quest, path);
            }
            var so = new SerializedObject(quest);
            so.FindProperty(F("Id")).stringValue = id;
            so.FindProperty(F("DisplayName")).stringValue = title;
            so.FindProperty(F("Description")).stringValue = description;
            so.FindProperty(F("QuestType")).enumValueIndex = (int)QuestType.Radiant;
            so.FindProperty(F("StartStageId")).stringValue = "stage.contract";
            var stages = so.FindProperty(F("Stages"));
            stages.arraySize = 1;
            var stage = stages.GetArrayElementAtIndex(0);
            stage.FindPropertyRelative(F("StageId")).stringValue = "stage.contract";
            stage.FindPropertyRelative(F("Description")).stringValue = objectiveText;
            stage.FindPropertyRelative(F("CompletionRewards")).ClearArray();
            stage.FindPropertyRelative(F("Transitions")).ClearArray();
            var objectives = stage.FindPropertyRelative(F("Objectives"));
            objectives.arraySize = 1;
            var objective = objectives.GetArrayElementAtIndex(0);
            objective.FindPropertyRelative(F("ObjectiveId")).stringValue = "objective.contract";
            objective.FindPropertyRelative(F("Description")).stringValue = objectiveText;
            objective.FindPropertyRelative(F("ObjectiveType")).enumValueIndex = (int)type;
            objective.FindPropertyRelative(F("TargetId")).stringValue = target;
            objective.FindPropertyRelative(F("RequiredAmount")).intValue = amount;
            objective.FindPropertyRelative(F("IsOptional")).boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(quest);
            return quest;
        }

        private static void AddDistinct(SerializedProperty array, UnityEngine.Object asset)
        {
            for (var i = 0; i < array.arraySize; i++)
                if (array.GetArrayElementAtIndex(i).objectReferenceValue == asset) return;
            array.InsertArrayElementAtIndex(array.arraySize);
            array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = asset;
        }
    }
}
