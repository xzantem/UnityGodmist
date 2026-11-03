using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Constants;
using Godmist.Core.Types;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using Godmist.Views.Combat;
using Godmist.Views.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class CombatFailureFeedbackPlayTests
    {
        [Serializable] private sealed class Scenes { public string[] paths; public bool[] loaded,active; }
        private static string Output=>Path.GetFullPath("../LocalHandoffs/combat-failure-feedback-20261103");
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        private static T View<T>() where T:Component=>Object.FindObjectsByType<T>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(v=>v.gameObject.scene.IsValid());
        private static IEnumerator Frames(int n=8){for(var i=0;i<n;i++)yield return null;}
        private static void Set(object owner,string name,object value)=>owner.GetType().GetField(name,Flags).SetValue(owner,value);

        [UnityTest]
        public IEnumerator AuthoredFeedbackRendersRealKickHookAndMissOutcomes()
        {
            Assert.That(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty),Is.False);
            Directory.CreateDirectory(Output);
            var scenes=EditorSceneManager.GetSceneManagerSetup();
            File.WriteAllText(Path.Combine(Output,"previous-scenes.json"),JsonUtility.ToJson(new Scenes {paths=scenes.Select(s=>s.path).ToArray(),loaded=scenes.Select(s=>s.isLoaded).ToArray(),active=scenes.Select(s=>s.isActive).ToArray()}));
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox/CombatSandbox.unity");
            yield return new EnterPlayMode();
            Application.runInBackground=true;
            yield return Frames(30);
            var host=View<CombatSandboxBootstrap>();var session=host.Session;
            Assert.That(session,Is.Not.Null);
            // Freeze only the test driver, leaving native unit views, cameras and feedback running.
            host.enabled=false;
            var source=session.Units.First(u=>u.IsPlayerSide);var target=session.Units.First(u=>!u.IsPlayerSide);
            var bus=host.GetDriver(session).FeedbackBus;
            var controller=View<CombatFloatingFeedbackController>();
            Set(controller,"lingerDuration",20f);Set(controller,"fadeDuration",0f);
            var messages=new List<CombatFeedbackEvent>();bus.Published+=messages.Add;
            var execution=new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem(feedbackBus:bus),feedbackBus:bus);
            var textOutput=new List<string>();
            for(var scenario=0;scenario<7;scenario++)
            {
                controller.Bind(null);controller.Bind(bus);messages.Clear();
                foreach(var unit in session.Units)unit.Position=new GridPosition(6,6);
                source.Position=new GridPosition(1,1);target.Position=new GridPosition(scenario is 1 or 4?4:2,1);
                source.Character.PassiveEffects.Clear();target.Character.PassiveEffects.Clear();
                source.Character.WeaponAccuracy=scenario==2?0:100;
                target.Character.Stats.Dodge=new Stat(scenario is 2 or 3?1e20f:0);
                target.Character.BaseResistances.MoveResistance=scenario==4?1f:0f;
                target.Character.BaseResistances.PoisonResistance=scenario==5?1f:0f;
                target.Character.BaseResistances.FireResistance=scenario==6?1f:0f;
                source.CurrentActionPoints=10000;source.MaxActionPoints=10000;
                session.ActiveUnit=source;
                session.Grid.SetBlocked(new GridPosition(3,1));
                var path="Assets/_Project/ScriptableObjects/Skills/"+(scenario is 1 or 4?"SD_ScoutHook.asset":"SD_WarriorKick.asset");
                var skill=Object.Instantiate(AssetDatabase.LoadAssetAtPath<SkillData>(path));
                if(scenario==2)Set(skill,"<SkillAccuracy>k__BackingField",0f);
                host.CameraFramer?.FocusUnit(target);
                yield return Frames(35);
                var randomState=UnityEngine.Random.state;UnityEngine.Random.InitState(8123);
                if(scenario>=5)
                    Assert.That(new StatusEffectSystem(feedbackBus:bus).ApplyStatus(source,target,
                        scenario==5?StatusEffectIds.Poison:StatusEffectIds.Burn,1f,2,1,.1f,0f,"test.resisted.dot"),Is.False);
                else
                    Assert.That(execution.TryExecute(session,source,skill,
                        new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,new[]{target}}},null,null,null,skipCosts:true),Is.True);
                UnityEngine.Random.state=randomState;
                Object.Destroy(skill);
                yield return new WaitForSecondsRealtime(1.1f);
                var expected=scenario==0?CombatFeedbackType.StatusApplied:scenario is 2 or 3?CombatFeedbackType.Miss:CombatFeedbackType.EffectFailed;
                Assert.That(messages.Any(e=>e.Type==expected),Is.True);
                var labels=Object.FindObjectsByType<CombatFloatingFeedbackView>(FindObjectsInactive.Exclude,FindObjectsSortMode.None)
                    .SelectMany(v=>v.GetComponentsInChildren<TMPro.TMP_Text>()).Select(t=>t.text).ToArray();
                Assert.That(labels.Any(t=>t.Contains(scenario==0?"Ogłuszenie":scenario==2?"Pudło":scenario==3?"Unik":scenario>=4?"Odporność":"Zablokowano")),Is.True,string.Join(" | ",labels));
                if(scenario==0)Assert.That(labels,Has.None.Contains("Zablokowano"));
                if(scenario>=4)Assert.That(labels,Is.EqualTo(new[]{"Odporność"}),"No effect name or duplicate resistance.");
                textOutput.Add(scenario+": "+string.Join(" | ",labels));
                yield return BackgroundGameCapture.Capture(Path.Combine(Output,$"0{scenario+1}-"+(scenario==0?"kick-blocked":scenario==1?"hook-blocked":scenario==2?"miss":scenario==3?"dodge":scenario==4?"hook-resisted":scenario==5?"poison-resisted":"burn-resisted")+".png"));
            }
            File.WriteAllLines(Path.Combine(Output,"native-feedback.txt"),textOutput);
        }

        [UnityTearDown]
        public IEnumerator RestoreScenes()
        {
            if(Application.isPlaying)yield return new ExitPlayMode();
            var path=Path.Combine(Output,"previous-scenes.json");if(!File.Exists(path))yield break;
            var previous=JsonUtility.FromJson<Scenes>(File.ReadAllText(path));
            if(previous.paths.Length==0||previous.paths.Any(string.IsNullOrEmpty))EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            else EditorSceneManager.RestoreSceneManagerSetup(previous.paths.Select((p,i)=>new SceneSetup{path=p,isLoaded=previous.loaded[i],isActive=previous.active[i]}).ToArray());
        }
    }
}
