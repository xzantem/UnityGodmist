using UnityEngine;

// Isolated first-city art slice. References are authored in the review scene.
public sealed class ArungardForgeFirePreview : MonoBehaviour
{
    public SpriteRenderer flame;
    public Sprite[] frames;
    public Material[] receivers;
    public float framesPerSecond=10;
    public float meanStrength=.32f;
    public float variation=.055f;
    public bool freezeForReview;
    public int CurrentFrame { get; private set; }
    public float CurrentStrength { get; private set; }
    float elapsed;
    void OnEnable(){elapsed=0;Sample(0);}
    void Update(){if(freezeForReview)return;elapsed+=Time.deltaTime;Sample(elapsed);}
    public void Sample(float seconds)
    {
        if(flame==null||frames==null||frames.Length!=4)return;
        CurrentFrame=Mathf.FloorToInt(Mathf.Max(0,seconds)*framesPerSecond)%frames.Length;
        flame.sprite=frames[CurrentFrame];
        CurrentStrength=meanStrength+variation*(.75f*Mathf.Sin(seconds*Mathf.PI*5)+.25f*Mathf.Sin(seconds*Mathf.PI*7.6f));
        if(receivers!=null)foreach(var material in receivers)
            if(material!=null)material.SetFloat("_ForgeStrength",CurrentStrength);
    }
    void OnDisable(){if(receivers!=null)foreach(var material in receivers)if(material!=null)material.SetFloat("_ForgeStrength",0);}
}
