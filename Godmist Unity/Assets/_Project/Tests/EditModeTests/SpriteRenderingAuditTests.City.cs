using System;
using System.Collections.Generic;
using System.IO;
using Godmist.Data.NPC;
using Godmist.Views.Characters;
using Godmist.Views.Exploration;
using Godmist.Views.NPC;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Godmist.Tests.EditMode
{
    public sealed partial class SpriteRenderingAuditTests
    {
        private ArungardCityDepthPreview CityDepth(ExplorationCharacterView hero,NpcView npc)
        {
            var root=Keep(new GameObject("Explicit city presentation"));
            var depth=root.AddComponent<ArungardCityDepthPreview>();
            var party=new[]{new ExplorationCharacterBinding { CharacterView=hero }};
            var npcs=new[]{new NpcSceneBinding { NpcView=npc }};
            ArungardPresentationRuntimeSetup.ConfigureActorDepth(root,party,npcs);
            ArungardPresentationRuntimeSetup.ConfigureActorDepth(root,party,npcs);
            Assert.That(depth.actors.Length,Is.EqualTo(2),"Repeated setup must replace entries rather than duplicate them.");
            Assert.That(hero.GetComponents<SortingGroup>().Length,Is.EqualTo(1));
            Assert.That(npc.GetComponents<SortingGroup>().Length,Is.EqualTo(1));
            return depth;
        }

        [TestCase(-.125f)] [TestCase(.125f)]
        public void CityNpcAndPartyDrawByFeetRatherThanFixedNpcOrder(float offset)
        {
            var npcSprite=Rendered("NPC",Solid(32,64,Color.green),new Vector2(20,10));
            npcSprite.sortingOrder=900;
            var npc=npcSprite.gameObject.AddComponent<NpcView>(); Set(npc,"bodyRenderer",npcSprite);
            var heroSprite=Rendered("Hero",Solid(32,64,Color.red),new Vector2(20,10+offset));
            var hero=heroSprite.gameObject.AddComponent<ExplorationCharacterView>(); Set(hero,"bodyRenderer",heroSprite);
            var depth=CityDepth(hero,npc);
            Call(hero,"LateUpdate"); Call(npc,"LateUpdate"); depth.RefreshDepth();
            var image=Capture(new Vector2(20,11),1.25f,$"city-depth-solid-{offset}");
            var pixel=image.GetPixel(128,128);
            Assert.That(offset<0?pixel.r:pixel.g,Is.GreaterThan(.9f));
            Assert.That(npcSprite.sortingOrder,Is.EqualTo(900),"NPC composite internal layer order must remain intact.");
        }

        [TestCase("Priest")] [TestCase("Alchemist")] [TestCase("Blacksmith")]
        [TestCase("Enchanter")] [TestCase("QuestGiver")]
        public void CityProductionNpcSpritesInterleaveWithJerzyOnBothSides(string name)
        {
            var definition=AssetDatabase.LoadAssetAtPath<NpcDefinition>("Assets/_Project/ScriptableObjects/NPC/ND_Arungard_"+name+".asset");
            Assert.That(CharacterAnimationResolver.TryResolveNpcIdle(definition.Id,out var npcClip),Is.True);
            Assert.That(CharacterAnimationResolver.TryResolveCharacter("jerzy",Vector2.right,CharacterAnimationState.Idle,out var heroClip),Is.True);
            var npcSprite=Rendered(name,npcClip.Frames[0],new Vector2(20,10));
            var npc=npcSprite.gameObject.AddComponent<NpcView>(); Set(npc,"bodyRenderer",npcSprite);
            var heroSprite=Rendered("Jerzy",heroClip.Frames[0],new Vector2(20.2f,10));
            var hero=heroSprite.gameObject.AddComponent<ExplorationCharacterView>(); Set(hero,"bodyRenderer",heroSprite);
            var depth=CityDepth(hero,npc);
            var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../LocalHandoffs/city-npc-depth-20261021"));
            Directory.CreateDirectory(output);
            foreach(var offset in new[]{-.25f,.25f})
            {
                heroSprite.transform.position=new Vector2(20.2f,10+offset);
                Call(hero,"LateUpdate"); Call(npc,"LateUpdate"); depth.RefreshDepth();
                var actorOrder=hero.GetComponent<SortingGroup>().sortingOrder;
                var npcOrder=npc.GetComponent<SortingGroup>().sortingOrder;
                Assert.That(offset<0?actorOrder>npcOrder:actorOrder<npcOrder,Is.True);
                var image=Capture(new Vector2(20,11.25f),1.65f,$"city-{name}-{offset}");
                File.WriteAllBytes(Path.Combine(output,$"{name}-{(offset<0?"front":"behind")}.png"),image.EncodeToPNG());
            }
        }

        [Test] public void CityDepthRegistersInactivePartyAndKeepsNpcCompositeTogether()
        {
            var heroSprite=Rendered("Recovering hero",Solid(32,64,Color.red),new Vector2(20,10));
            var hero=heroSprite.gameObject.AddComponent<ExplorationCharacterView>(); Set(hero,"bodyRenderer",heroSprite);
            hero.gameObject.SetActive(false);
            var npcSprite=Rendered("NPC",Solid(32,64,Color.green),new Vector2(20,10));
            var npc=npcSprite.gameObject.AddComponent<NpcView>(); Set(npc,"bodyRenderer",npcSprite);
            var foreground=Keep(new GameObject("NPC workstation foreground",typeof(SpriteRenderer)));
            foreground.transform.SetParent(npc.transform,false);
            foreground.GetComponent<SpriteRenderer>().sortingOrder=2;
            var depth=CityDepth(hero,npc);
            hero.gameObject.SetActive(true); hero.transform.position=new Vector2(20,9);
            Call(hero,"LateUpdate"); depth.RefreshDepth();
            Assert.That(hero.GetComponent<SortingGroup>().sortingOrder,Is.GreaterThan(npc.GetComponent<SortingGroup>().sortingOrder));
            Assert.That(foreground.GetComponentInParent<SortingGroup>(),Is.SameAs(npc.GetComponent<SortingGroup>()));
            Assert.That(foreground.GetComponent<SpriteRenderer>().sortingOrder,Is.EqualTo(2));
        }
    }
}
