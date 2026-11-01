using System;
using System.Linq;
using Godmist.Data.Skills;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    // Explicit additive authoring. Never run the historical whole-content setup for this pass.
    public static class ContentPass2WarriorT5Setup
    {
        const string Root = "Assets/_Project/ScriptableObjects/Skills/SkillTrees/ST_Warrior_";
        static string F(string field) => "<"+field+">k__BackingField";
        static readonly string[][] Commander = {
            new[]{"Egida","2","regroup","Rozkaz Gotowości zwiększa obrażenia celu o 15%/30% na 3 tury."},
            new[]{"Egzekucja","1","t5_3","Krytyk ma 10% szansy na egzekucję celu, który zginąłby od pięciokrotności obrażeń. Na bossie zamiast egzekucji podwaja obrażenia."},
            new[]{"Pojedynek","2","","+10%/20% szansy krytycznej przeciw sąsiadującemu celowi, jeśli jest jedynym sąsiadującym wrogiem."},
            new[]{"Szermierka","2","t5_3","Ataki w ten sam cel: +2%/4% obrażeń na ładunek, maks. 5. Jeden ładunek na użycie, także przy pudle. Zmiana celu lub atak wielocelowy resetuje serię."},
            new[]{"Oko Dowódcy","1","","+2 zasięgu rozkazów."}
        };
        static readonly string[][] Brawler = {
            new[]{"Trening Skupienia","1","","Każdy punkt aktualnej wściekłości zwiększa celność umiejętności o 0,5. Wartość liczona przed opłaceniem ataku."},
            new[]{"Szał Bitewny","2","t5_3","+30%/50% obrażeń poniżej 10%/20% maksymalnego zdrowia."},
            new[]{"Furia Bitewna","2","","+0,003x/0,005x obrażeń krytycznych za każdy brakujący procent zdrowia."},
            new[]{"Powrót do Żywych","2","death_wish","Po wyłączeniu Pragnienia Śmierci leczy 25%/50% zanegowanych przez jego ochronę obrażeń."},
            new[]{"Nieśmiertelna Furia","1","t5_4","Śmiertelne obrażenia zatrzymane przez Pragnienie Śmierci generują 10 wściekłości."}
        };

        [MenuItem("Tools/Godmist/Content Pass 2/Apply Warrior T5")]
        public static void Apply()
        {
            foreach (var name in new[]{"Commander","Brawler"})
            {
                var tree = AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>(Root+name+".asset");
                if (tree == null) throw new InvalidOperationException("Missing existing tree: "+name);
                ApplyToTree(tree);
                AssetDatabase.SaveAssetIfDirty(tree);
            }
        }

        public static void ApplyToTree(SkillTreeDefinition tree)
        {
            var commander = tree.Id == "tree.warrior.commander";
            if (!commander && tree.Id != "tree.warrior.brawler") return;
            var entries = commander ? Commander : Brawler;
            var prefix = "node.warrior."+(commander?"commander.":"brawler.");
            var slugs = commander
                ? new[]{"aegis","execution","duel","fencing","commanders_eye"}
                : new[]{"focused_rage","battle_frenzy","battle_fury","return_to_life","undying_fury"};
            var artRoot = "Assets/_Project/Art/Icons/Passives/VS22/Warrior"+(commander?"Commander/":"Brawler/");
            var icons = slugs.Select(slug=>ImportIcon(artRoot+"t5_"+slug+"-32.png")).ToArray();
            if (icons.Any(icon=>icon==null)) throw new InvalidOperationException("Missing source icon in "+tree.Id);
            var so = new SerializedObject(tree);
            var nodes = so.FindProperty(F("Nodes"));
            for (var column=1;column<=5;column++)
            {
                var id = prefix+"t5_"+column;
                var index = -1;
                for (var i=0;i<nodes.arraySize;i++)
                    if (nodes.GetArrayElementAtIndex(i).FindPropertyRelative(F("Id")).stringValue==id)
                    { if(index>=0)throw new InvalidOperationException("Duplicate "+id);index=i; }
                if(index<0){index=nodes.arraySize;nodes.InsertArrayElementAtIndex(index);}
                var p=nodes.GetArrayElementAtIndex(index);var e=entries[column-1];
                p.FindPropertyRelative(F("Id")).stringValue=id;
                p.FindPropertyRelative(F("DisplayName")).stringValue=e[0];
                p.FindPropertyRelative(F("Description")).stringValue=e[3];
                p.FindPropertyRelative(F("Tier")).intValue=5;
                p.FindPropertyRelative(F("Column")).intValue=column;
                p.FindPropertyRelative(F("MaxRanks")).intValue=int.Parse(e[1]);
                p.FindPropertyRelative(F("Icon")).objectReferenceValue=icons[column-1];
                p.FindPropertyRelative(F("GrantedSkill")).objectReferenceValue=null;
                p.FindPropertyRelative(F("PassiveEffects")).ClearArray();
                var req=p.FindPropertyRelative(F("Requirements"));req.ClearArray();
                if(e[2].Length>0)
                {
                    req.arraySize=1;
                    req.GetArrayElementAtIndex(0).FindPropertyRelative(F("NodeId")).stringValue=prefix+e[2];
                    req.GetArrayElementAtIndex(0).FindPropertyRelative(F("MinimumRanks")).intValue=1;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tree);
        }

        static Sprite ImportIcon(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter
                ?? throw new InvalidOperationException("Missing dedicated T5 icon: "+path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            foreach (var platform in new[]{"DefaultTexturePlatform","Standalone"})
            {
                var settings = platform == "DefaultTexturePlatform"
                    ? importer.GetDefaultPlatformTextureSettings() : importer.GetPlatformTextureSettings(platform);
                settings.textureCompression = TextureImporterCompression.Uncompressed;
                settings.crunchedCompression = false;
                importer.SetPlatformTextureSettings(settings);
            }
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path)
                ?? throw new InvalidOperationException("Dedicated T5 icon did not import: "+path);
        }
    }
}
