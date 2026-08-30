using UnityEditor;
using Godmist.Data.Dungeons;

namespace Godmist.Editor
{
    [InitializeOnLoad]
    internal static class DungeonRoomTemplateBoundaryMigration
    {
        static DungeonRoomTemplateBoundaryMigration()
        {
            EditorApplication.delayCall += MigrateExistingTemplates;
        }

        private static void MigrateExistingTemplates()
        {
            var changed = false;

            foreach (var guid in AssetDatabase.FindAssets(
                         "t:DungeonRoomTemplateDefinition"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var template = AssetDatabase.LoadAssetAtPath<
                    DungeonRoomTemplateDefinition>(path);

                if (template == null || template.BoundaryEdgesConfigured)
                {
                    continue;
                }

                Undo.RecordObject(
                    template,
                    "Migrate dungeon room boundary edges");
                template.MigrateLegacyBoundaryEdges();
                EditorUtility.SetDirty(template);
                changed = true;
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
            }
        }
    }
}
