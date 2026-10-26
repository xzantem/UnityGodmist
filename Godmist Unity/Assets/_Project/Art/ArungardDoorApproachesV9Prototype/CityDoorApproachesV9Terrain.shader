Shader "Godmist/Prototype/City Door Approaches V9 Terrain" {
Properties { _RouteMask("Native street coverage",2D)="black"{}  _MainTex("Dark earth",2D)="white"{} _PathTex("Packed earth",2D)="white"{} _GrassTex("Grass",2D)="white"{} _StoneTex("Cobble",2D)="white"{} _SlabTex("Slabs",2D)="white"{} _ForgeLight("Light",Vector)=(5,13,1.8,0) _ForgeStrength("Energy",Float)=.32 _BaseY("Ground",Float)=-1000 }
SubShader { Tags {"Queue"="Transparent"} Cull Off ZWrite Off
Pass {CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
#include "../ArungardWorkstationPrototype/ArungardFireResponse.cginc"
sampler2D _RouteMask;
sampler2D _MainTex,_PathTex,_GrassTex,_StoneTex,_SlabTex;float4 _Footprints[64];int _FootprintCount;
struct v2f {float4 pos:SV_POSITION;float2 world:TEXCOORD0;};
v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xy;return o;}
float hash(float2 p){return frac(sin(dot(p,float2(12.9898,78.233)))*43758.5453);}
float seg(float2 p,float2 a,float2 b){float2 d=b-a;return length(p-a-d*saturate(dot(p-a,d)/dot(d,d)));}
float patch(float2 p,float2 at,float2 radius){return 1-length((p-at)/radius);}
float edge(float d,float jitter){return saturate((d+jitter+.07)/.14);}
float rectInside(float2 p,float4 b){float2 d=min(p-b.xy,b.zw-p);return min(d.x,d.y);}
// Native chips follow short stone courses; no soft brush fringe or extra geometry.
float stoneEdge(float2 w,float d,float depth,float seed){
 float row=floor(w.y*8);float2 cell=float2(floor(w.x*8+fmod(abs(row),2)*.5),row);
 float chip=step(.64,hash(cell+seed))*depth;
 float grain=(hash(floor(w*32)+seed)-.5)*.03125;
 return step(chip+grain,d);
}
fixed4 frag(v2f i):SV_Target {
float2 w=(floor(i.world*32)+.5)/32,uv=w/4;
float jitter=(hash(floor(w*8))-.5)*.16+(hash(floor(w*32))-.5)*.05;
// United native32PPU street coverage, without analytic capsule end caps.
float2 maskUV=(w-float2(-44,-40))/96;
float3 route=tex2D(_RouteMask,maskUV).rgb;
float plaza=rectInside(w,float4(9.1,12.6,14.7,17.1));
float clippedCorner=(abs(w.x-11.9)+abs(w.y-14.85))-4.65;
plaza=min(plaza,-clippedCorner);
float eastHousehold=seg(w,float2(30.7,12.5),float2(30.7,17.9));
eastHousehold=min(eastHousehold,seg(w,float2(30.7,17.9),float2(29.7,18.1)));
eastHousehold=min(eastHousehold,seg(w,float2(29.7,18.1),float2(29.7,20.85)));
eastHousehold=min(eastHousehold,seg(w,float2(29.7,18.1),float2(27,18.35)));
eastHousehold=min(eastHousehold,seg(w,float2(29.7,20.85),float2(23.2,20.85)));
eastHousehold=min(eastHousehold,seg(w,float2(23.2,20.85),float2(23.2,23.7)));
eastHousehold=min(eastHousehold,seg(w,float2(23.2,23.7),float2(30.7,23.7)));
// Enchanter court: skirt the instrument rack from its south side.
eastHousehold=min(eastHousehold,seg(w,float2(27,3.5),float2(28.3,2)));
eastHousehold=min(eastHousehold,seg(w,float2(28.3,2),float2(31.4,2)));
eastHousehold=min(eastHousehold,seg(w,float2(31.4,2),float2(31.4,5.2)));
eastHousehold=min(eastHousehold,seg(w,float2(31.4,5.2),float2(32.1,6.1)));
float green=patch(w,float2(19,19),float2(3.7,4.2));
green=max(green,patch(w,float2(12,5),float2(4.8,3.2)));
green=max(green,patch(w,float2(28,20),float2(3.2,2.5)));
green=max(green,patch(w,float2(3,21),float2(2.6,2.2)));
green=max(green,patch(w,float2(20,3),float2(3,1.5)));
green=max(green,patch(w,float2(9.2,7.3),float2(1.3,1.1)));
green=max(green,patch(w,float2(22.3,18.8),float2(1.4,1.2)));
green=max(green,patch(w,float2(-6.7,8),float2(1.1,2)));green=max(green,patch(w,float2(-5,23.5),float2(2,.8)));
// Household margins: turf around rear plots and small working yards.
// Path/plaza composition below is applied afterwards, keeping traffic surfaces clear.
green=max(green,patch(w,float2(-7.8,17.8),float2(1.2,2)));
green=max(green,patch(w,float2(-7.3,21.8),float2(1.1,1.7)));
green=max(green,patch(w,float2(-8,6.5),float2(1.1,1.5)));
green=max(green,patch(w,float2(7,-5),float2(4.7,1.1)));
green=max(green,patch(w,float2(26,-5.2),float2(3,1)));
green=max(green,patch(w,float2(32.1,15.5),float2(1.6,1)));
green=max(green,patch(w,float2(-8.65,13.35),float2(.35,.8)));
green=max(green,patch(w,float2(-6.35,2.65),float2(.3,.25)));
green=max(green,patch(w,float2(26.6,19.9),float2(2.8,1.2)));
green=max(green,patch(w,float2(33.5,5.7),float2(.8,2.5)));
green=max(green,patch(w,float2(48,20.4),float2(1.5,.8)));
green=max(green,patch(w,float2(46.4,1.1),float2(1.2,.8)));
green=max(green,patch(w,float2(46.4,-1.4),float2(1.1,.6)));
green=max(green,patch(w,float2(8.8,-22),float2(2,1.1)));
green=max(green,patch(w,float2(25.3,-22.4),float2(2.5,1.1)));
green=max(green,patch(w,float2(8.7,-28.5),float2(1.8,.7)));
green=max(green,patch(w,float2(30,-29.4),float2(1.6,1)));
green=max(green,patch(w,float2(-22,-17),float2(1.2,1.5)));
green=max(green,patch(w,float2(-29,-28),float2(.8,1.5)));
green=max(green,patch(w,float2(-18,-29),float2(1.1,1.6)));
green=max(green,patch(w,float2(-28,-36),float2(2.5,1)));
green=max(green,patch(w,float2(-18,-36),float2(2,1)));
green=max(green,patch(w,float2(-36,-34),float2(1.6,1)));
green=max(green,patch(w,float2(48.3,-19.4),float2(1.2,.6)));
green=max(green,patch(w,float2(42.6,-28.2),float2(1.4,.8)));
green=max(green,patch(w,float2(49,-33.2),float2(1.2,.7)));
// Retained earth dominates the inner plots; turf marks quiet yard margins.
green=max(green,patch(w,float2(-35,20.5),float2(2.3,.8)));
green=max(green,patch(w,float2(-23,20.5),float2(1.5,.8)));
green=max(green,patch(w,float2(-35,4.2),float2(1.7,1.5)));
green=max(green,patch(w,float2(-32,-4.6),float2(1.7,1.3)));
// The quiet rear plot stays earthy; low planting groups the bench and household props.
green=max(green,patch(w,float2(12.4,-2.4),float2(1.7,1.6)));
green=max(green,patch(w,float2(4.7,-3.8),float2(1.6,.7)));
green=max(green,patch(w,float2(2.3,2),float2(1.4,1.8)));
// Retained churchyard soil beneath the grave rows.
green=min(green,-rectInside(w,float4(24.9,18.9,29.1,19.8)));
float northCourt=edge(rectInside(w,float4(-9,10,-6.55,14.3)),jitter);
float southCourt=edge(rectInside(w,float4(-6.75,.7,-2.65,3)),jitter);
float3 c=tex2D(_MainTex,uv).rgb;
// Trampled but predominantly earthy courts, distinct from pale traffic strips.
c=lerp(c,tex2D(_PathTex,uv).rgb,max(northCourt,southCourt)*.22);
// Unbuilt margins of western household plots: finer soil with low self-seeded cover.
// Composite before every street, doorstep, worked bed and foundation layer.
float quietPlot=max(rectInside(w,float4(-39,13,-21,14.95)),rectInside(w,float4(-37,5.8,-30.7,9.7)));
quietPlot=max(quietPlot,rectInside(w,float4(-22.7,-17.8,-17.1,-14.1)));
quietPlot=max(quietPlot,rectInside(w,float4(-29.3,-28.9,-25.8,-25)));
float2 yardCell=floor(w*.8)+float2(71,19),yardBlend=frac(w*.8);
yardBlend=yardBlend*yardBlend*(3-2*yardBlend);
float growth=lerp(lerp(hash(yardCell),hash(yardCell+float2(1,0)),yardBlend.x),lerp(hash(yardCell+float2(0,1)),hash(yardCell+1),yardBlend.x),yardBlend.y);
float quiet=edge(quietPlot-(.12+(1-growth)*.75),jitter*.8)*smoothstep(.18,.70,growth)*(1-step(.001,route.r));
float3 fineSoil=lerp(tex2D(_MainTex,uv*1.75+float2(.37,.61)).rgb,tex2D(_PathTex,uv).rgb,.22);
c=lerp(c,fineSoil,quiet*.62);
// Broad uneven clusters, not scattered props or planted beds on the streets.
float yardGrowthMask=quiet*saturate((growth-.32)*1.7);
c=lerp(c,tex2D(_GrassTex,uv).rgb,yardGrowthMask*.68);
c=lerp(c,tex2D(_GrassTex,uv).rgb,edge(green,jitter)*.88);
c=lerp(c,tex2D(_PathTex,uv).rgb,route.r*route.b);
c=lerp(c,tex2D(_StoneTex,uv).rgb,route.g);
c=lerp(c,tex2D(_SlabTex,uv).rgb,stoneEdge(w,plaza,.15625,7));
// Worked soil unifies the new garden beds with their native ground.
float yardSoil=max(rectInside(w,float4(-35.6,4,-33.4,6.35)),rectInside(w,float4(43.1,.5,47.3,1.45)));
yardSoil=max(yardSoil,rectInside(w,float4(-23.9,-19.2,-21.7,-16.85)));
c=lerp(c,tex2D(_MainTex,uv).rgb*.9,edge(yardSoil,jitter)*.85);
float yardWear=max(patch(w,float2(-30.3,-3.2),float2(1.15,.4)),patch(w,float2(-32.5,-3.3),float2(1.1,.4)));
yardWear=max(yardWear,patch(w,float2(44.8,1.85),float2(1,.4)));
c=lerp(c,tex2D(_PathTex,uv).rgb,edge(yardWear,jitter)*.25);
float supplyPad=max(patch(w,float2(4.6,10.15),float2(1.1,.65)),patch(w,float2(17.5,18.35),float2(1.1,.65)));
c=lerp(c,tex2D(_PathTex,uv).rgb,edge(supplyPad,jitter)*.35);
// Alchemist front threshold; medicinal beds stand on worked soil, not street paving.
float alchemistStep=edge(rectInside(w,float4(4.45,3.55,5.55,4.04)),jitter*.2);
c=lerp(c,tex2D(_SlabTex,uv).rgb,alchemistStep);
float benchSoil=edge(patch(w,float2(12,-2),float2(1.1,.6)),jitter);
c=lerp(c,tex2D(_MainTex,uv).rgb,benchSoil*.5);
// Short functional thresholds, kept beneath props and before the footprint skirts.
float forgeThreshold=stoneEdge(w,rectInside(w,float4(4.45,12.55,5.55,13.03)),.0625,19);
c=lerp(c,tex2D(_SlabTex,uv).rgb,forgeThreshold);
float workPad=edge(patch(w,float2(5.25,11.65),float2(1.05,.72)),jitter);
c=lerp(c,tex2D(_MainTex,uv).rgb*.77,workPad*.72);
// A damp, trampled strip at the trough, not a second paved road.
float waterWear=edge(patch(w,float2(10,13.08),float2(1.05,.35)),jitter);
c=lerp(c,tex2D(_MainTex,uv).rgb*.83,waterWear*.32);
// Small bench landing, surrounded by earth rather than an extended paved square.
float benchLanding=edge(rectInside(w,float4(31.2,5.9,33,6.7)),jitter*.3);
c=lerp(c,tex2D(_SlabTex,uv).rgb,benchLanding);
// Small flat stone landings meet the actual front doors, on the 32PPU ground grid.
// They stop at the southern physical footprint edge and do not create new obstacles.
float chapelLanding=rectInside(w,float4(25.84375,12.9375,28.15625,14.03125));
float towerLanding=rectInside(w,float4(25.40625,4.1875,27.21875,5.03125));
float entryStone=max(chapelLanding,towerLanding);
float stoneCoverage=stoneEdge(w,entryStone,.0625,31);
c=lerp(c,tex2D(_SlabTex,uv).rgb,stoneCoverage);
// One native-pixel dark course connects the lower stone lip to the surrounding soil.
float chapelLip=rectInside(w,float4(25.875,12.9375,28.125,12.96875));
float towerLip=rectInside(w,float4(25.4375,4.1875,27.1875,4.21875));
float lip=step(0,max(chapelLip,towerLip));
c*=1-lip*.12;
// Small bench landings stay at the northern plaza edge, leaving the well aisle open.
float benchPads=max(rectInside(w,float4(8.84375,16.46875,10.65625,17.21875)),rectInside(w,float4(12.84375,16.46875,14.65625,17.21875)));
c=lerp(c,tex2D(_SlabTex,uv).rgb,edge(benchPads,jitter*.25));
// Six discrete burial plots in the former medicinal garden, behind the chapel.
float churchyard=edge(rectInside(w,float4(24.5,18.4,29.2,20.65)),jitter);
c=lerp(c,lerp(tex2D(_MainTex,uv).rgb,tex2D(_GrassTex,uv).rgb,.24),churchyard*.7);
// Castle retains the approved34x24 footprint. Courts use the same32PPU terrain library.
float terrace=step(0,rectInside(w,float4(-44,26,-10,50)));
float lower=edge(rectInside(w,float4(-39,28,-16,36)),jitter);
float upper=edge(rectInside(w,float4(-38,39,-17,47)),jitter*.3);
float avenue=min(seg(w,float2(-27,26),float2(-27,37)),seg(w,float2(-39,29),float2(-17,29)));
float3 castle=lerp(tex2D(_MainTex,uv).rgb,tex2D(_GrassTex,uv).rgb,.16);
castle=lerp(castle,tex2D(_PathTex,uv).rgb,lower);
castle=lerp(castle,tex2D(_StoneTex,uv).rgb,edge(.85-avenue,jitter));
castle=lerp(castle,tex2D(_SlabTex,uv).rgb,upper);
c=lerp(c,castle,terrace);
// Private household approach joins the existing public street east of the two doors.
// A narrow shared forecourt, not another road crossing the block.
float artisanAccess=min(seg(w,float2(3.5,19.6),float2(3.5,19)),seg(w,float2(3.5,19),float2(7.25,19.25)));
artisanAccess=min(artisanAccess,seg(w,float2(6.875,19.6),float2(6.875,19.225)));
artisanAccess=min(artisanAccess,seg(w,float2(7.25,19.25),float2(10.8,19.4)));
artisanAccess=min(artisanAccess,seg(w,float2(10.8,19.4),float2(12,20)));
float artisanWidth=lerp(.21,.27,saturate((w.x-7.25)/3.55));
float accessCover=edge(artisanWidth-artisanAccess,jitter*.7);
c=lerp(c,tex2D(_PathTex,uv).rgb,accessCover*.86);
// Small landing below the existing sprite steps; no duplicate tall doorstep.
float artisanSteps=max(rectInside(w,float4(3.15625,19.8125,3.84375,20.03125)),rectInside(w,float4(6.53125,19.8125,7.21875,20.03125)));
c=lerp(c,tex2D(_SlabTex,uv).rgb*.92,edge(artisanSteps,jitter*.18)*.9);
// A working approach wraps south/east around the actual supply stack.
float storeAccess=min(seg(w,float2(2.75,9.1),float2(3.2,8.7)),seg(w,float2(3.2,8.7),float2(5.85,8.9)));
storeAccess=min(storeAccess,seg(w,float2(5.85,8.9),float2(5.85,10.65)));
storeAccess=min(storeAccess,seg(w,float2(5.85,10.65),float2(6,11.5)));
float storeApron=patch(w,float2(3.2,8.85),float2(.72,.36));
float storeCover=max(edge(.24-storeAccess,jitter*.6),edge(storeApron,jitter*.6)*.7);
c=lerp(c,tex2D(_PathTex,uv).rgb,storeCover*.78);
float storeStep=rectInside(w,float4(2.40625,9.3125,3.09375,9.53125));
c=lerp(c,tex2D(_SlabTex,uv).rgb*.92,edge(storeStep,jitter*.18)*.9);
// Short dirty skirts meet the existing ground contacts at foundations.
float soil=0;for(int j=0;j<_FootprintCount;j++){float4 b=_Footprints[j];float2 d=abs(w-b.xy)-b.zw;float dist=length(max(d,0));soil=max(soil,saturate((.20-dist+jitter)/.17));}
c=lerp(c,tex2D(_MainTex,uv).rgb,soil*.6);
// Damp stone follows actual water use: well lip, bucket and the trough, not a circular glow.
float waterDamp=max(patch(w,float2(12,14.35),float2(1.04,.36)),patch(w,float2(13.3,14.37),float2(.36,.26)));
waterDamp=max(waterDamp,patch(w,float2(10,13.17),float2(.91,.30)));
waterDamp=max(waterDamp,.10-seg(w,float2(11.3,14.35),float2(10.35,13.45)));
float damp=edge(waterDamp,jitter*.45);
c*=1-damp*.22;
c=lerp(c,c*float3(.94,.97,1),damp*.45);
// Trading fronts are swept and repeatedly trampled; the quiet rear soil remains intact.
// These masks follow the two physical stall fronts, not their tall canopy bounds.
float westTrade=patch(w,float2(19.2,14.7),float2(1.75,.9));
float eastTrade=patch(w,float2(22.5,13.1),float2(1.78,.83));
float tradeJoin=.50-seg(w,float2(20.05,14.35),float2(21.4,13.35));
float trading=edge(max(max(westTrade,eastTrade),tradeJoin),jitter*.8);
float3 swept=lerp(tex2D(_MainTex,uv).rgb,tex2D(_PathTex,uv).rgb,.78);
c=lerp(c,swept,trading*.88);
// The delivery stacks sit beside the gate street. Short paired wheel impressions
// finish at the stacks, leaving the street and chapel threshold untouched.
float wheelA=seg(w,float2(16.8,16.75),float2(17.45,17.85));
float wheelB=seg(w,float2(17.45,16.75),float2(18.1,17.85));
float wheelDist=min(wheelA,wheelB);
float wheelCut=step(wheelDist,.045)*step(.24,hash(floor(w*16)));
float deliveryWear=edge(.62-seg(w,float2(17.1,16.75),float2(17.75,17.85)),jitter);
c=lerp(c,tex2D(_PathTex,uv).rgb,deliveryWear*.28);
c*=1-wheelCut*.20;
// Forge working surface follows the anvil, stock and doorway, rather than the roof.
float smithStone=max(rectInside(w,float4(4.25,10.875,6.125,12.6875)),rectInside(w,float4(6.125,11.75,7.75,12.6875)));
float smithCoverage=edge(smithStone,jitter*.2);
c=lerp(c,tex2D(_SlabTex,uv).rgb*float3(.87,.88,.88),smithCoverage);
float smithSoot=max(patch(w,float2(5.15,11.5),float2(.65,.4)),patch(w,float2(5,12.55),float2(.5,.35)));
c=lerp(c,tex2D(_MainTex,uv*2).rgb*float3(.42,.45,.48),edge(smithSoot,jitter)*.42);
float smithLip=step(0,rectInside(w,float4(4.28125,10.875,6.09375,10.90625)));
c*=1-smithLip*.14;
// Stone threshold fits the actual gate opening, with the packed road beyond it.
float gateStone=rectInside(w,float4(14.875,21.9375,17.125,23.1875));
c=lerp(c,tex2D(_SlabTex,uv).rgb*.92,edge(gateStone,jitter*.2));
float gateLip=step(0,rectInside(w,float4(14.90625,21.9375,17.09375,21.96875)));
c*=1-gateLip*.13;
// A quiet green pocket around the southern bench; seating and access stay clear.
float quietGreen=max(patch(w,float2(10.8,-2.65),float2(.75,.65)),patch(w,float2(12.9,-3.2),float2(.85,.6)));
float quietWalk=min(seg(w,float2(10,-3.8),float2(11.7,-2.5)),seg(w,float2(11.7,-2.5),float2(12.55,-2.5)));
float quietGrass=edge(quietGreen,jitter)*(1-edge(.27-quietWalk,jitter*.5));
c=lerp(c,tex2D(_GrassTex,uv).rgb,quietGrass*.8);
c=lerp(c,tex2D(_PathTex,uv).rgb,edge(.23-quietWalk,jitter*.5)*.35);
// Laundry is reached from the existing household path, with a damp patch at the bucket.
float washWalk=min(seg(w,float2(8.2,-3.6),float2(8.45,-4.95)),seg(w,float2(8.45,-4.95),float2(10.65,-4.95)));
c=lerp(c,tex2D(_PathTex,uv).rgb,edge(.22-washWalk,jitter*.6)*.66);
float washDamp=edge(patch(w,float2(8.8,-4.76),float2(.44,.23)),jitter*.4);
c*=1-washDamp*.23;
// Delivery cart joins the retained wheel tracks and supplies beside the gate street.
float cartPad=edge(patch(w,float2(17.7,16.85),float2(.95,.66)),jitter);
c=lerp(c,tex2D(_PathTex,uv).rgb,cartPad*.4);
// Fuel is stored on a darker working strip; water belongs beside the work and herbs.
float fuelPad=edge(patch(w,float2(3,12.7),float2(.92,.32)),jitter);
c=lerp(c,tex2D(_MainTex,uv).rgb*.72,fuelPad*.6);
float barrelDamp=max(patch(w,float2(1.25,13.12),float2(.43,.25)),patch(w,float2(3.875,.145),float2(.43,.25)));
c*=1-edge(barrelDamp,jitter*.4)*.22;
// The herb plot lies south of the public street. Its access serves the beds and water.
float herbPlot=max(rectInside(w,float4(1.1,-.0625,3.5,.8125)),rectInside(w,float4(1.1,1.1875,3.5,2.0625)));
c=lerp(c,tex2D(_MainTex,uv*1.5).rgb*float3(.66,.63,.53),edge(herbPlot,jitter*.6)*.98);
float herbAccess=min(seg(w,float2(4.2,2.5),float2(3.875,2.2)),seg(w,float2(3.875,2.2),float2(3.875,.8)));
herbAccess=min(herbAccess,seg(w,float2(3.875,.95),float2(1.2,.95)));
c=lerp(c,tex2D(_PathTex,uv).rgb,edge(.13-herbAccess,jitter*.4)*.65);
// Kitchen plot is behind the household walk, with a short access along its east edge.
float kitchenPlot=rectInside(w,float4(5.9,-5.0625,8.1,-4.125));
c=lerp(c,tex2D(_MainTex,uv*1.5).rgb*.73,edge(kitchenPlot,jitter*.5)*.9);
float kitchenAccess=seg(w,float2(8.35,-3.675),float2(8.35,-4.5));
c=lerp(c,tex2D(_PathTex,uv).rgb,edge(.15-kitchenAccess,jitter*.4)*.55);
// Quiet churchyard vegetation is continuous around the burial ground, not isolated stamps.
// Keep the existing public/household walks bare and leave grave soil to the final layer.
float churchGreen=max(patch(w,float2(26.8,19.55),float2(3.45,2.05)),patch(w,float2(30.3,20.2),float2(1.15,.95)));
float greenCover=edge(churchGreen,jitter*1.3)*(1-edge(.40-eastHousehold,jitter*.7));
float3 churchGrass=lerp(tex2D(_MainTex,uv).rgb,tex2D(_GrassTex,uv).rgb,.63);
c=lerp(c,churchGrass,greenCover*.8);
// The clergy house front door connects to the existing northern cemetery walk.
float clergyAccess=seg(w,float2(26.625,20.85),float2(26.625,21.5));
c=lerp(c,tex2D(_PathTex,uv).rgb,edge(.22-clergyAccess,jitter*.55)*.85);
float clergyStep=rectInside(w,float4(26.28125,21.3125,26.96875,21.53125));
c=lerp(c,tex2D(_SlabTex,uv).rgb*.92,edge(clergyStep,jitter*.18)*.9);
// A small bench landing and its approach serve sitting, without paving the entire cemetery.
float benchAccess=seg(w,float2(29.7,20.2),float2(30.3,20.4));
c=lerp(c,tex2D(_PathTex,uv).rgb,edge(.22-benchAccess,jitter*.55)*.75);
float churchBench=rectInside(w,float4(29.4375,20.3125,31.15625,21.09375));
c=lerp(c,tex2D(_SlabTex,uv).rgb*.91,edge(churchBench,jitter*.35)*.92);
if(w.x>28.5 && w.x<35 && w.y>0.5 && w.y<8.2){
// Instrument work needs a small level stone surface, not a paved whole courtyard.
float instrumentPad=max(rectInside(w,float4(29.03125,2.21875,30.78125,3.1875)),rectInside(w,float4(29.25,2.125,30.5625,3.3125)));
c=lerp(c,tex2D(_SlabTex,uv).rgb*.92,edge(instrumentPad,jitter*.28)*.94);
float instrumentAccess=seg(w,float2(29.9,2),float2(29.9,2.35));
c=lerp(c,tex2D(_PathTex,uv).rgb,edge(.19-instrumentAccess,jitter*.5)*.68*(1-edge(instrumentPad,jitter*.28)));
// Existing broken statue rests on a low remnant of its old foundation.
float statueBase=rectInside(w,float4(29.4375,.84375,30.15625,1.375));
c=lerp(c,tex2D(_SlabTex,uv).rgb*.83,edge(statueBase,jitter*.22)*.95);
// Quiet planted margin follows the bench and shrubs; heavily used access stays bare.
float courtGreen=max(patch(w,float2(33.35,5.65),float2(.95,1.9)),patch(w,float2(32.25,7.35),float2(1.15,.65)));
float courtGrass=edge(courtGreen,jitter*1.2)*(1-edge(.43-eastHousehold,jitter*.6));
float benchExclusion=edge(rectInside(w,float4(31.15,5.85,33.05,7.05)),jitter*.3);
c=lerp(c,lerp(tex2D(_MainTex,uv).rgb,tex2D(_GrassTex,uv).rgb,.65),courtGrass*.8*(1-benchExclusion));
}
// Functional ground at retained western work areas. Keep each mask local.
if(w.x>-9.5 && w.x<-5.5 && w.y>7.5 && w.y<13){
 // Dry fuel rests on a small stone pad; only the handling side is trampled.
 float fuelBase=rectInside(w,float4(-9.03125,7.9375,-6.96875,8.5));
 c=lerp(c,tex2D(_SlabTex,uv).rgb*.83,edge(fuelBase,jitter*.3)*.9);
 float fuelHandling=min(seg(w,float2(-6.5,8.9),float2(-8,8.8)),seg(w,float2(-8,8.8),float2(-8.55,8.75)));
 c=lerp(c,tex2D(_PathTex,uv).rgb,edge(.19-fuelHandling,jitter*.65)*.72);
 // The ground directly beneath and in front of the drying rack is repeatedly used.
 float laundryWear=patch(w,float2(-7.6,12.15),float2(1.05,.53));
 c=lerp(c,tex2D(_PathTex,uv).rgb*.94,edge(laundryWear,jitter*.85)*.62);
}
if(w.x>-36 && w.x<-32 && w.y>3 && w.y<6.75){
 // Two background household beds have worked soil, with a narrow maintenance aisle.
 float westBeds=max(rectInside(w,float4(-35.65625,4,-33.34375,4.90625)),rectInside(w,float4(-35.65625,5.40625,-33.34375,6.3125)));
 c=lerp(c,tex2D(_MainTex,uv*1.5).rgb*float3(.68,.64,.53),edge(westBeds,jitter*.6)*.96);
 float bedAccess=min(seg(w,float2(-33.3,3.1),float2(-33,5.15)),seg(w,float2(-33,5.15),float2(-34.5,5.15)));
 c=lerp(c,tex2D(_PathTex,uv).rgb,edge(.13-bedAccess,jitter*.45)*.65);
}
// Burial soil is the final ground layer: foundation skirts must not wash it out.
// Each plot is wider than its headstone plinth and extends26native pixels south.
float graveCut=0,graveRim=0,graveCrown=0;
for(int row=0;row<2;row++)for(int col=0;col<3;col++){
 float2 at=float2(25.25+1.6*col,19+1.2*row);
 float2 q=w-at;
 float rimNoise=step(.65,hash(float2(floor(q.y*12),col+row*3)))*.03125;
 float plot=rectInside(w,float4(at.x-.375+rimNoise,at.y-.8125,at.x+.375-rimNoise,at.y+.0625));
 // Clip the four corners by one pixel for a dug-earth silhouette.
 float corner=min(abs(q.x)-.34375,abs(q.y+.375)-.40625);
 float cut=step(0,plot)*(1-step(0,corner));
 graveCut=max(graveCut,cut);
 graveRim=max(graveRim,cut*(1-step(.03125,plot)));
 graveCrown=max(graveCrown,cut*saturate(1-abs(q.x)/.375)*saturate(1-abs(q.y+.375)/.4375));
}
// Retain real earth clumps; tint/shade them rather than replacing them with flat colour.
float3 burialSoil=tex2D(_MainTex,uv*2).rgb*float3(.68,.60,.48);
burialSoil*=.9+graveCrown*.22;
burialSoil*=1-graveRim*.27;
c=lerp(c,burialSoil,graveCut);
// Soil caught in exposed paving joints; retain the original stone colour and water marks.
float stoneRim=max(step(0,plaza)*(1-step(.21875,plaza)),step(0,entryStone)*(1-step(.09375,entryStone)));
float grit=stoneRim*step(.58,hash(floor(w*16)+43))*step(.33,hash(floor(w*32)+11));
c=lerp(c,tex2D(_MainTex,uv).rgb,grit*.42);
return fixed4(ForgeResponse(c,w),1);
}ENDCG}}}

