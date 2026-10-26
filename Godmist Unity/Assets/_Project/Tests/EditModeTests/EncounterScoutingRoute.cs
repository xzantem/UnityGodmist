using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;

namespace Godmist.Tests.EditMode
{
    // Simulation-only route selection on the generated room graph. Gates require
    // visiting their authored branch; never invent a percentage of skippable fights.
    internal static class EncounterScoutingRoute
    {
        public static List<string> SelectRooms(DungeonFloorInstance floor)
        {
            var start=floor.Rooms.Single(r=>r.RoomType==DungeonRoomType.Start).RoomId;
            var final=floor.Rooms.Single(r=>r.RoomType==DungeonRoomType.Final).RoomId;
            var branches=floor.Connections.Where(c=>c.HasProgressionGate).Select(c=>c.RequiredBranchRoomId)
                .Distinct().ToArray();
            if(branches.Any(string.IsNullOrEmpty) || branches.Length>8)
                throw new InvalidOperationException("Unsupported gate graph in scouting simulation.");
            var branchBits=branches.Select((id,i)=>(id,bit:1<<i)).ToDictionary(x=>x.id,x=>x.bit);
            var threat=floor.Encounters.GroupBy(e=>e.RoomId).ToDictionary(g=>g.Key,g=>g.Sum(e=>e.Enemies.Count));
            var adjacency=floor.Rooms.ToDictionary(r=>r.RoomId,r=>floor.Connections
                .Where(c=>c.FirstRoomId==r.RoomId || c.SecondRoomId==r.RoomId).ToArray());
            int Collect(string room,int keys)=>keys | (branchBits.TryGetValue(room,out var bit)?bit:0);
            (List<string> rooms,int keys) Path(string from,string to,int initialKeys)
            {
                var initial=(room:from,keys:Collect(from,initialKeys));
                var open=new List<(string room,int keys)>{initial};
                var distance=new Dictionary<(string room,int keys),int>{{initial,0}};
                var previous=new Dictionary<(string room,int keys),(string room,int keys)>();
                var settled=new HashSet<(string room,int keys)>();
                while(open.Count>0)
                {
                    open.Sort((a,b)=>{var d=distance[a].CompareTo(distance[b]);return d!=0?d:string.CompareOrdinal(a.room,b.room);});
                    var current=open[0];open.RemoveAt(0);if(!settled.Add(current))continue;
                    if(current.room==to)
                    {
                        var result=new List<string>{current.room};var cursor=current;
                        while(previous.TryGetValue(cursor,out var parent)){result.Add(parent.room);cursor=parent;}
                        result.Reverse();return(result,current.keys);
                    }
                    foreach(var edge in adjacency[current.room])
                    {
                        if(edge.HasProgressionGate && (current.keys & branchBits[edge.RequiredBranchRoomId])==0)continue;
                        var nextRoom=edge.FirstRoomId==current.room?edge.SecondRoomId:edge.FirstRoomId;
                        var next=(room:nextRoom,keys:Collect(nextRoom,current.keys));
                        var cost=distance[current]+1+(threat.TryGetValue(nextRoom,out var enemies)?enemies*100:0);
                        if(distance.TryGetValue(next,out var old) && old<=cost)continue;
                        distance[next]=cost;previous[next]=current;open.Add(next);
                    }
                }
                throw new InvalidOperationException("No legal scouting route between rooms.");
            }
            var path=Path(start,final,0);var visited=path.rooms.Distinct().ToList();
            var keys=path.keys;
            var target=(int)Math.Ceiling(floor.Encounters.Count*.75);
            int Fights(IEnumerable<string> rooms)=>floor.Encounters.Count(e=>rooms.Contains(e.RoomId));
            while(Fights(visited)<target)
            {
                // Add legal return trips if the shortest route skips much more than
                // 25%. If the graph forces more fights, retain them rather than cheat.
                var options=floor.Encounters.Where(e=>!visited.Contains(e.RoomId)).Select(e=>
                {
                    var outward=Path(final,e.RoomId,keys);var inward=Path(e.RoomId,final,outward.keys);
                    var added=outward.rooms.Concat(inward.rooms).Where(r=>!visited.Contains(r)).Distinct().ToList();
                    return(added,keys:inward.keys,fights:Fights(added));
                }).OrderBy(o=>o.fights).ThenBy(o=>o.added.Count).ThenBy(o=>string.Join("/",o.added),StringComparer.Ordinal).ToArray();
                if(options.Length==0)break;
                visited.AddRange(options[0].added);keys=options[0].keys;
            }
            return visited;
        }
    }
}
