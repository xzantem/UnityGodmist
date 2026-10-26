using System;
using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Data.Items
{
    // Native pixel glyphs: effect owns the mark, powder owns the pigment.
    // Rows run top-to-bottom. Keep each5x7mark distinct at inventory scale.
    internal static class GalduryteRuneGlyphs
    {
        internal const int Width=5;
        internal const int Height=7;
        private static readonly string[] Patterns =
        {
            "10001/01010/00100/01010/10101/01010/10001", // GeneralDamagePercent: crossed blades
            "00100/01010/10001/10101/10001/01010/00100", // CriticalChancePercent: eye diamond
            "01110/10001/10101/11111/10101/10001/01110", // AccuracyPercent: sight
            "01010/11111/11111/11111/01110/00100/00100", // MaxHealthPercent: heart
            "11111/10001/10101/10101/10101/01010/00100", // PhysicalDefensePercent: shield
            "10010/01001/00100/01001/10010/01001/00100", // SpeedPercent: running chevrons
            "00100/01110/01110/01110/11111/00100/01110", // PhysicalDamagePercent: sword
            "10101/01110/00100/01110/10101/00100/00100", // MagicDamagePercent: spell staff
            "00100/01110/11011/10101/11011/01110/10101", // CriticalDamageFlat: split diamond
            "11110/11110/00100/00100/00100/01110/10101", // SuppressionChance: striking hammer
            "10100/01101/00110/11000/00110/01001/10010", // DebuffChance: broken binding
            "10101/10101/01010/00000/10101/01010/00100", // DotChance: falling marks
            "00100/00100/01010/01010/10011/10101/01110", // BleedOnAttack: cut drop
            "01110/00100/00100/01010/10001/10101/01110", // PoisonOnAttack: flask
            "00100/01100/01110/11010/10001/10101/01110", // BurnOnAttack: flame
            "01010/11111/01110/00100/11111/00100/00100", // HealOnAttack: heart and blade
            "00100/01010/10001/01010/11111/00100/00100", // ResourceOnAttack: crystal and blade
            "00010/00110/01100/11111/00110/01100/01000", // ActionValueOnAttack: forward bolt
            "01010/01110/00100/01110/10001/10001/01110", // ItemDropChance: tied bag
            "01110/10001/10100/01010/00101/10001/01110", // ResourceGenerationPercent: returning flow
            "01110/10101/11111/01010/01110/00100/10101", // UndeadDamagePercent: skull
            "00100/01010/00100/01110/10101/01010/10001", // HumanDamagePercent: figure
            "10101/10101/00000/01110/11111/11111/01010", // BeastDamagePercent: paw
            "10001/11011/10101/10001/01010/01110/10101", // DemonDamagePercent: horned face
            "00100/10101/01110/11111/01110/10101/00100", // StunOnAttack: impact star
            "10101/01110/10101/00100/10101/01110/10101", // FreezeOnAttack: frost
            "11111/01010/00100/00100/00100/01010/11111", // SlowOnAttack: hourglass
            "00000/11011/10101/10101/10101/11011/00100", // ExperiencePercent: open book
            "01110/10001/10110/10100/10110/10001/01110", // GoldPercent: stamped coin
            "00100/01110/11111/00100/11111/10001/01110", // PhysicalPenetration: arrow through plate
            "00010/00110/01100/11111/00110/10001/01110", // MagicPenetration: bolt through plate
            "00110/01001/10000/10110/01001/00010/01100", // DodgePercent: sidestep
            "11111/10001/10101/11011/10101/01010/00100", // MagicDefensePercent: crystal shield
            "11111/11011/10101/10001/01110/01010/00100", // AllDefensePercent: layered shield
            "00100/01110/00100/11111/01010/00100/01110", // ActionPointsPercent: time cross
            "11111/10101/10101/10001/11011/01010/00100", // DotResistance: sheltered drops
            "00100/01110/00100/10101/10101/01110/00100", // SuppressionResistance: anchor
            "11111/10001/11001/10101/10011/01010/00100", // DebuffResistance: binding shield
            "11111/10101/11111/10101/10001/01010/00100", // AllResistance: ward cross
            "11111/10001/10001/11111/10001/01010/00100", // PhysicalDamageReduction: barred shield
            "00100/01010/10001/11111/10001/01010/00100", // MagicDamageReduction: barred crystal
            "11111/10001/11111/10001/11111/01010/00100", // AllDamageReduction: double ward
            "01110/01010/01010/10001/10101/10001/01110", // MaxResourcePercent: full vessel
            "00100/00100/01010/11111/10001/10001/01110", // BleedDamageReduction: barred drop
            "01110/00100/00100/11111/10001/10001/01110", // PoisonDamageReduction: sealed flask
            "00100/01100/01110/11111/10001/10001/01110", // BurnDamageReduction: banked flame
            "00100/01010/00100/10101/01110/00100/00100", // ResourceCostReduction: descending crystal
            "00101/01010/10101/01010/10101/01010/10100", // CriticalAvoidance: deflected diamond
            "01110/10001/00100/01110/00100/10001/01110", // HealthPerTurn: returning cross
            "01110/10001/00100/01010/00100/10001/01110"  // ResourcePerTurn: returning crystal
        };
        private static readonly Color32[] Pigments =
        {
            new(146,43,57,255), new(41,73,142,255), new(107,53,137,255),
            new(142,112,29,255), new(38,102,69,255), new(55,49,63,255),
            new(159,58,112,255), new(94,105,123,255), new(151,107,29,255),
            new(158,77,26,255)
        };
        internal static bool IsColorSupported(int color) => color>=0 && color<Pigments.Length;
        internal static bool TryGetPattern(int effect,out string pattern)
        {
            pattern=effect>=0 && effect<Patterns.Length ? Patterns[effect].Replace("/",string.Empty) : null;
            return pattern!=null;
        }
        internal static bool TryParse(string token,out int color,out string pattern)
        {
            color=-1;pattern=null;
            var parts=token.Split(':');
            if(parts.Length<1 || parts.Length>2 || !int.TryParse(parts[0],out color) || !IsColorSupported(color)) return false;
            // Color-only keys remain supported for old direct library callers.
            // All item instances now include the actual effect in their key.
            var effect=color switch { 0=>0,1=>1,2=>2,3=>12,4=>15,5=>18,6=>20,7=>25,8=>28,_=>29 };
            if(parts.Length==2 && !int.TryParse(parts[1],out effect)) return false;
            return TryGetPattern(effect,out pattern);
        }
        internal static void Draw(Color32[] pixels,int slot,int count,int color,string pattern)
        {
            var scale=count==1 ? 2 : 1;
            var rows=(count+1)/2;
            var totalHeight=count==1 ? Height*scale : rows*8-1;
            var left=count==1 ? 11 : (slot%2==0 ? 9 : 17);
            var bottom=(32-totalHeight)/2+(count==1 ? 0 : (rows-1-slot/2)*8);
            var pigment=Pigments[color];
            // A restrained lower-right stone lip suggests an incision. It never
            // changes surface alpha or becomes a bright opaque outline/glow.
            for(var pass=0;pass<2;pass++)
            for(var y=0;y<Height;y++)
            for(var x=0;x<Width;x++)
            {
                if(pattern[y*Width+x]!='1') continue;
                for(var dy=0;dy<scale;dy++)
                for(var dx=0;dx<scale;dx++)
                {
                    var px=left+x*scale+dx+(pass==0 ? 1 : 0);
                    var py=bottom+(Height-1-y)*scale+dy+(pass==0 ? -1 : 0);
                    if(px<0 || px>=32 || py<0 || py>=32) continue;
                    var index=py*32+px;var surface=pixels[index];
                    if(surface.a==0) continue;
                    var target=pass==0 ? new Color32(255,239,208,255) : pigment;
                    var amount=pass==0 ? .30f : .88f;
                    var blended=(Color32)Color.Lerp(surface,target,amount);
                    blended.a=surface.a;pixels[index]=blended;
                }
            }
        }
    }
}
