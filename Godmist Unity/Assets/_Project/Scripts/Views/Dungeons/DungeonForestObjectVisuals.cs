using System;
using System.Collections.Generic;
using Godmist.Data.Dungeons;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    /// <summary>Native pixel art for explicit Forest exploration definitions.</summary>
    internal static class DungeonForestObjectVisuals
    {
        const string Root="Godmist/ForestArt/Exploration/";
        static readonly Dictionary<string,Sprite> sprites=new(StringComparer.Ordinal);
        public static Sprite Load(string name)
        {
            if(!sprites.TryGetValue(name,out var sprite))
            {
                sprite=Resources.Load<Sprite>(Root+name);
                if(sprite==null)throw new InvalidOperationException("Missing imported Forest exploration art: "+Root+name);
                sprites.Add(name,sprite);
            }
            return sprite;
        }
        public static void TryApplyTrap(DungeonSpikeTrapView view,SpikeTrapDungeonObjectDefinition definition)
        {
            var name=definition?.Id switch {"object.forest.trap.snare"=>"snare","object.forest.trap.poison"=>"poison","object.forest.trap.roots"=>"roots",_=>null};
            if(name==null)return;
            view.ConfigureRuntimeSprites(Load(name),nativeScale:name=="snare"?.55f:.8f);
            view.ConfigureRuntimePresentationColors(Color.white,new Color(.42f,.42f,.38f,1));
        }
        public static DungeonLeverView CreateLeverView(Transform parent,string name,LeverDungeonObjectDefinition definition)
        {
            if(definition?.Id!="object.forest.lever")return null;
            var view=Create<DungeonLeverView>(parent,name,"lever");
            view.ConfigureRuntimePresentation(Color.white,Color.white,true);
            // Only the winch base is solid. Ground traps and cleansing marks
            // remain walkable; blocking their tile would prevent triggering.
            var collider=view.gameObject.AddComponent<BoxCollider2D>();
            collider.size=new Vector2(.45f,.3f);collider.offset=new Vector2(0,.15f);
            return view;
        }
        public static DungeonConditionView CreateConditionView(Transform parent,string name,ConditionDungeonObjectDefinition definition)
        {
            if(definition?.Id!="object.forest.condition")return null;
            var view=Create<DungeonConditionView>(parent,name,"condition");
            view.ConfigureRuntimePresentation(new Color(.72f,.62f,.55f,1),new Color(.45f,.82f,.62f,1));
            return view;
        }
        static T Create<T>(Transform parent,string name,string art) where T:DungeonObjectView
        {
            var root=new GameObject(name);root.transform.SetParent(parent,false);
            if(parent!=null)root.layer=parent.gameObject.layer;
            var renderer=root.AddComponent<SpriteRenderer>();renderer.sprite=Load(art);renderer.sortingOrder=6;
            return root.AddComponent<T>();
        }
    }
}
