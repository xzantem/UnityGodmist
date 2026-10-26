using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Views.Dungeons;
using Godmist.Views.Lighting;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed partial class CombatBalanceOctober24Tests
    {
        [Test, Explicit("Render the actual stair prefab on production catacomb terrain with its lighting.")]
        public void RenderUpAndDownStairsOnProductionFloor()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, flags).GetValue(owner);
            var output = Path.GetFullPath("../LocalHandoffs/combat-balance-20261024");
            var fixture = new DungeonEntryPerformanceTests { OutputOverride = output, StageOverride = "stairs", SeedOverride = 1 };
            fixture.FrameProbe = (bootstrap, game) =>
            {
                var lighting = Field<DungeonSilhouetteLighting>(bootstrap, "silhouetteLighting");
                var camera = Field<Camera>(lighting, "worldCamera");
                var party = Field<DungeonPartySpawner>(bootstrap, "partySpawner").PartyController;
                var stair = Object.Instantiate(AssetDatabase.LoadAssetAtPath<DungeonExitView>("Assets/_Project/Prefabs/Dungeon/PF_DungeonExitView.prefab"), bootstrap.transform);
                stair.InitializeTransition(party, party.ActiveMemberViews.First().Position + Vector2.right, () => { });
                typeof(DungeonSandboxBootstrap).GetMethod("RegisterTransitionLighting", flags).Invoke(bootstrap, new object[] { stair });
                foreach (var root in bootstrap.gameObject.scene.GetRootGameObjects())
                    foreach (var node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 30;
                var target = new RenderTexture(640, 480, 24);
                var image = new Texture2D(640, 480, TextureFormat.RGBA32, false);
                var active = RenderTexture.active;
                var previousTarget = camera.targetTexture;
                var previousPosition = camera.transform.position;
                var previousSize = camera.orthographicSize;
                var previousMask = camera.cullingMask;
                try
                {
                    camera.targetTexture = target; camera.cullingMask = 1 << 30; camera.orthographicSize = 1.5f;
                    camera.transform.position = new Vector3(stair.transform.position.x, stair.transform.position.y, -10);
                    typeof(DungeonSilhouetteLighting).GetMethod("RefreshLighting", flags).Invoke(lighting, new object[] { Time.time + 1f });
                    Color32[] Render(string direction)
                    {
                        camera.Render(); RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, 640, 480), 0, 0); image.Apply();
                        File.WriteAllBytes(Path.Combine(output, "stairs-" + direction + "-native.png"), image.EncodeToPNG());
                        return image.GetPixels32();
                    }
                    var down = Render("down"); stair.ConfigureFloorDirection(true); var up = Render("up");
                    Assert.That(Enumerable.Range(0, up.Length).Count(i => !up[i].Equals(down[i])), Is.GreaterThan(100), "The different stair direction must be visible in the native render.");
                }
                finally
                {
                    camera.targetTexture = previousTarget; camera.transform.position = previousPosition;
                    camera.orthographicSize = previousSize; camera.cullingMask = previousMask;
                    RenderTexture.active = active; Object.DestroyImmediate(image); Object.DestroyImmediate(target); Object.DestroyImmediate(stair.gameObject);
                }
            };
            fixture.MeasureProductionFloorEntry("dungeon.catacombs.expedition", DungeonLength.Short);
        }
    }
}
