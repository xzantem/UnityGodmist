using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Lighting;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class ForestStationaryShadowTests
    {
        const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        static T Read<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Private).GetValue(owner);
        static void Call(object owner,string name,params object[] args)=>owner.GetType().GetMethod(name,Private).Invoke(owner,args);

        [Test]
        public void StationaryProductionForestRetainsShadowMasksAcrossRepeatedFogAndLightingTicks()
        {
            var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../LocalHandoffs/forest-thin-dividers-shadow-stability-20261022"));
            Directory.CreateDirectory(output);
            var fixture=new DungeonEntryPerformanceTests{OutputOverride=output,StageOverride="stationary",SeedOverride=171026};
            fixture.FrameProbe=(bootstrap,session)=>
            {
                var map=Read<DungeonFloorMapView>(bootstrap,"floorMapView");
                var lighting=Read<DungeonSilhouetteLighting>(bootstrap,"silhouetteLighting");
                var party=Read<DungeonPartySpawner>(bootstrap,"partySpawner").PartyController;
                var floor=session.ActiveDungeon.Floors[0];
                // Production entry rooms need not contain a torch. Test an
                // existing authored fixture rather than accepting empty masks.
                map.ApplyFog(floor);
                typeof(DungeonSilhouetteLighting).GetField("lastLightingFrame",Private).SetValue(lighting,int.MinValue);
                Call(lighting,"RefreshLighting",0f);
                Assert.That(lighting.TorchCount,Is.GreaterThan(0));
                var fixturePosition=lighting.FirstTorchPosition;
                var camera=Read<Camera>(lighting,"worldCamera");
                camera.transform.position=new Vector3(fixturePosition.x,fixturePosition.y,-10);
                int actorIndex=0;
                foreach(var actor in party.ActiveMemberViews)
                    actor.SetPosition(fixturePosition+new Vector2(1+actorIndex++*.35f,0));
                new DungeonFogSystem().Update(floor,party.ActiveMemberViews.Select(a=>a.DungeonPosition).ToArray());
                var visible=floor.Fog.Visible.ToArray();map.ApplyFog(floor);
                Call(map,"TickFogAnimation",1f);
                void Tick(float t)
                {
                    floor.Fog.SetVisible(visible);map.ApplyFog(floor);Call(map,"TickFogAnimation",.2f);
                    typeof(DungeonSilhouetteLighting).GetField("lastLightingFrame",Private).SetValue(lighting,int.MinValue);
                    Call(lighting,"RefreshLighting",t);Call(lighting,"RefreshForestPartyReveal");Call(lighting,"AdvanceForestCanopyFade",.2f);
                }
                Tick(0);Tick(.2f);Tick(.4f);
                var reader=typeof(DungeonLightingCadenceTests).GetMethod("ReadMask",BindingFlags.NonPublic|BindingFlags.Static);
                var mask=Read<RenderTexture>(lighting,"mask");
                Assert.That(lighting.ActiveTorchCount,Is.GreaterThan(0));
                var baseline=Enumerable.Range(0,lighting.ActiveTorchCount)
                    .Select(i=>(Color32[])reader.Invoke(null,new object[]{mask,i})).ToArray();
                Assert.That(baseline.Any(p=>p.Any(c=>c.r>0)),Is.True,"Blank masks cannot establish shadow stability.");
                int rebuilds=lighting.ShadowMaskRebuildCount;
                for(int frame=0;frame<30;frame++)
                {
                    Tick(.6f+frame*.2f);
                    Assert.That(lighting.ShadowMaskRebuildCount,Is.EqualTo(rebuilds),"Stationary silhouettes changed at tick "+frame);
                }
                foreach(int i in Enumerable.Range(0,baseline.Length))
                    CollectionAssert.AreEqual(baseline[i],(Color32[])reader.Invoke(null,new object[]{mask,i}),"Stationary shadow slice "+i);
                File.WriteAllText(Path.Combine(output,"stationary-masks.txt"),$"30 stationary ticks; {baseline.Length} non-empty-source slices; combined rebuilds held at {rebuilds}; unchanged mask pixels.");
            };
            fixture.MeasureProductionFloorEntry("dungeon.forest.expedition",DungeonLength.Long);
        }
    }
}
