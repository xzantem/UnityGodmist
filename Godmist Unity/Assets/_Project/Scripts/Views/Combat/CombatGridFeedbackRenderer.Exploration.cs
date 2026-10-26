using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Views.Combat
{
    public partial class CombatGridFeedbackRenderer
    {
        Func<GridPosition,Vector2?> explorationMarker;
        public Func<GridPosition,Vector2?> ResolveExplorationMarker {
            get=>explorationMarker;
            set {
                if(ReferenceEquals(value,explorationMarker))return;
                explorationMarker=value;
                foreach(var batch in cornerBatches.Values)batch.Cells=null;
            }
        }
        readonly List<Vector3> movingMarkerVertices=new(128);
        private void LateUpdate()
        {
            if(explorationMarker==null)return;
            foreach(var pair in cornerBatches)
            {
                var batch=pair.Value;
                if(!batch.Renderer.enabled||batch.Cells==null)continue;
                batch.Mesh.GetVertices(movingMarkerVertices);var changed=false;
                for(var i=0;i<batch.Cells.Length;i++)
                {
                    var first=i*4;if(first+3>=movingMarkerVertices.Count)break;
                    var old=(movingMarkerVertices[first]+movingMarkerVertices[first+2])*.5f;
                    var delta=MarkerToLocal(pair.Key,batch.Cells[i])-old;
                    if(delta.sqrMagnitude<.00000001f)continue;
                    for(var j=0;j<4;j++)movingMarkerVertices[first+j]+=delta;
                    changed=true;
                }
                if(changed){batch.Mesh.SetVertices(movingMarkerVertices);batch.Mesh.RecalculateBounds();}
            }
        }
        Vector3 MarkerToLocal(CombatGridFeedbackLayer layer,GridPosition cell)
        {
            if (layer is CombatGridFeedbackLayer.SkillTarget or CombatGridFeedbackLayer.ChosenSkillTargets
                && ResolveExplorationMarker?.Invoke(cell) is Vector2 feet)
                return new Vector3(feet.x,feet.y,feedbackZ);
            return GridToLocal(cell);
        }
    }
}
