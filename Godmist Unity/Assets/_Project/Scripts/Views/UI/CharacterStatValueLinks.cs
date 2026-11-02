using System;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Utilities;
using Godmist.Runtime.Characters;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Godmist.Views.UI
{
    public static class CharacterStatValueLinks
    {
        public const string StatPrefix="character-stat.";
        public const string ResistancePrefix="character-resistance.";
        public static string Stat(string display,CharacterStatType type)=>Link(display,StatPrefix+type);
        public static string Resistance(string display,ResistanceType type)=>Link(display,ResistancePrefix+type);
        static string Link(string display,string id)=>"<link=\""+id+"\"><color=#7EDBFF>"+display+"</color></link>";
        public static string Decorate(string text,CharacterInstance character,bool resistances=false)
        {
            var names=new Dictionary<string,CharacterStatType>{{UiTerms.Attack,CharacterStatType.MinAttack}};
            foreach(CharacterStatType type in Enum.GetValues(typeof(CharacterStatType)))names[UiTerms.Stat(type,character.ResourcePool?.ResourceType??ResourceType.None)]=type;
            return string.Join("\n",text.Split('\n').Select(line=>
            {
                var separator=line.IndexOf("<pos=",StringComparison.Ordinal);var end=separator<0?-1:line.IndexOf('>',separator)+1;
                if(separator<0){separator=line.IndexOf(": ",StringComparison.Ordinal);end=separator+2;}
                if(separator<0 || end<=0)return line;
                var label=line.Substring(0,separator);var value=line.Substring(end);
                if(resistances)
                {
                    foreach(ResistanceType type in Enum.GetValues(typeof(ResistanceType)))if(type!=ResistanceType.All && UiTerms.ResistanceRow(type)==label)return line.Substring(0,end)+Resistance(value,type);
                }
                else if(names.TryGetValue(label,out var stat))return line.Substring(0,end)+Stat(value,stat);
                return line;
            }));
        }
        public static string Health(CharacterInstance c)
        {
            var maximum=c.MaximumHealth;var shield=c.ShieldPool?.TotalShield??0;
            return c.CurrentHealth.ToString("0",UiTerms.PolishCulture)+"/"+Stat(maximum.ToString("0",UiTerms.PolishCulture),CharacterStatType.MaxHealth)+(shield>0?" (+"+shield+")":"");
        }
        public static string Resource(CharacterInstance c)=>c.ResourcePool==null?"":c.ResourcePool.CurrentValue+"/"+Stat(c.ResourcePool.MaxValue.ToString(),CharacterStatType.MaxResource);
    }

    // Owned by the two explicitly bound views; no scene discovery or serialized wiring.
    public sealed class CharacterStatHoverController
    {
        CharacterInstance character;Transform owner;TMP_Text[] fields=Array.Empty<TMP_Text>();UIWorkspaceTooltipView tooltip;
        string currentId,currentBody;
        readonly List<RaycastResult> raycasts=new();
        public void Bind(CharacterInstance c,Transform ownerView,UIWorkspaceTooltipView view,params TMP_Text[] textFields)
        {
            if(character!=c){Clear();character=c;}
            owner=ownerView;tooltip=view??tooltip;fields=textFields;
        }
        public void Clear(){tooltip?.InvalidateSource(this);currentId=null;currentBody=null;character=null;}
        public void Tick(Vector2 position)
        {
            tooltip ??= UIWorkspaceTooltipView.ActiveInstance;
            if(character==null || tooltip==null)return;
            string id=null,display=null;
            var allowed=true;
            if(EventSystem.current!=null)
            {
                raycasts.Clear();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=position},raycasts);
                allowed=raycasts.Count==0 || owner!=null && raycasts[0].gameObject.transform.IsChildOf(owner);
            }
            if(allowed)foreach(var field in fields)
            {
                if(field==null || !field.gameObject.activeInHierarchy || field.canvasRenderer.cull)continue;
                var canvas=field.canvas;var camera=canvas!=null&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
                var index=TMP_TextUtilities.FindIntersectingLink(field,position,camera);if(index<0)continue;
                var link=field.textInfo.linkInfo[index];var candidate=link.GetLinkID();
                if(!candidate.StartsWith(CharacterStatValueLinks.StatPrefix,StringComparison.Ordinal)&&!candidate.StartsWith(CharacterStatValueLinks.ResistancePrefix,StringComparison.Ordinal))continue;
                id=candidate;display=link.GetLinkText();break;
            }
            if(id==null){if(currentId!=null)tooltip.NotifySourceExit(this);currentId=null;currentBody=null;return;}
            var entry=tooltip.BuildCharacterValueDetails(character,id,display);if(entry==null)return;
            if(id==currentId && entry.Description==currentBody)return;
            if(currentId!=null)tooltip.InvalidateSource(this);
            currentId=id;currentBody=entry.Description;tooltip.ShowText(entry.Title,entry.Description,this);
        }
    }
}
