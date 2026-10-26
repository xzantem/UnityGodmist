using Godmist.Views.Exploration;
using UnityEngine;

// Isolated town-art prototype. Ground north edge is authored per building.
public sealed class ArungardBuildingRoofPreview : MonoBehaviour
{
    public SpriteRenderer upper;
    public ExplorationPartyController party;
    public float groundNorth;
    // Optional world-space (west, east, north) edges for joined, nonrectangular buildings.
    // Empty retains the existing rectangular-building behavior.
    public Vector3[] groundEdges;
    public bool IsOccluding { get; private set; }
    void LateUpdate()
    {
        if(upper==null||party==null)return;
        IsOccluding=false;
        var b=upper.bounds;
        foreach(var member in party.ActiveMemberViews)
        {
            var p=member.Position;
            var behind=false;
            if(groundEdges!=null&&groundEdges.Length>0)
            {
                foreach(var edge in groundEdges)
                    if(p.x>edge.x&&p.x<edge.y&&p.y>edge.z){behind=true;break;}
            }
            else behind=p.y>groundNorth;
            if(p.x>b.min.x&&p.x<b.max.x&&behind&&p.y<b.max.y)
            {IsOccluding=true;break;}
        }
        var c=upper.color;c.a=Mathf.MoveTowards(c.a,IsOccluding?.28f:1f,Time.deltaTime*4);upper.color=c;
    }
}
