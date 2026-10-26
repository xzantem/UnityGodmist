using UnityEngine;
public sealed class ArungardGroundingPreview : MonoBehaviour {
public Material material;public Vector4[] footprints;
void OnEnable(){if(material==null)return;material.SetVectorArray("_Footprints",footprints);material.SetInt("_FootprintCount",footprints.Length);}
}