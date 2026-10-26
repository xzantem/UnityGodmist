using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(1000)]
public sealed class ArungardCityDepthPreview : MonoBehaviour
{
    [System.Serializable] public struct Entry {public Transform feet;public SortingGroup group;}
    public Entry[] actors;
    public static int Order(float y)=>-Mathf.RoundToInt(y*32)*4;
    void LateUpdate()=>RefreshDepth();
    public void RefreshDepth()
    {
        if(actors==null)return;
        foreach(var entry in actors)
            if(entry.feet!=null&&entry.group!=null)
            {entry.group.enabled=true;entry.group.sortingOrder=Order(entry.feet.position.y);}
    }
}
