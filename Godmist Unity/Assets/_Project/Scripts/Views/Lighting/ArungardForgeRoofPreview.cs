using Godmist.Views.Exploration;
using UnityEngine;

// Isolated art prototype only. Explicit scene references, no runtime discovery.
public sealed class ArungardForgeRoofPreview : MonoBehaviour
{
    public SpriteRenderer upper;
    public ExplorationPartyController party;
    public bool IsOccluding { get; private set; }
    void LateUpdate()
    {
        if(upper==null||party==null)return;
        IsOccluding=false;
        foreach(var member in party.ActiveMemberViews)
        {
            Vector2 p=member.Position;
            var b=upper.bounds;
            if(p.x>b.min.x&&p.x<b.max.x&&p.y>16&&p.y<b.max.y)
            {IsOccluding=true;break;}
        }
        var c=upper.color;c.a=Mathf.MoveTowards(c.a,IsOccluding?.28f:1f,Time.deltaTime*4);upper.color=c;
    }
}
