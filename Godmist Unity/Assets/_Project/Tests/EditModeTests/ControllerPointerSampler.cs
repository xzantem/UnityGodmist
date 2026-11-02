using System;
using System.Collections.Generic;
using UnityEngine.LowLevel;

namespace Godmist.Tests.EditMode
{
    /// <summary>Read-only observation after the real camera/target projection LateUpdates; no Game View dependency.</summary>
    internal sealed class ControllerPointerSampler : IDisposable
    {
        private readonly PlayerLoopSystem original;
        public ControllerPointerSampler(Action sample)
        {
            original=UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
            var loop=UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
            if(!Insert(ref loop,sample))throw new InvalidOperationException("The native LateUpdate stage is missing.");
            UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(loop);
        }
        private static bool Insert(ref PlayerLoopSystem owner,Action sample)
        {
            if(owner.subSystemList==null)return false;
            var children=new List<PlayerLoopSystem>(owner.subSystemList);
            for(var i=0;i<children.Count;i++)
            {
                var child=children[i];
                if(child.type==typeof(UnityEngine.PlayerLoop.PreLateUpdate.ScriptRunBehaviourLateUpdate))
                {
                    children.Insert(i+1,new PlayerLoopSystem{type=typeof(ControllerPointerSampler),updateDelegate=()=>sample()});
                    owner.subSystemList=children.ToArray();return true;
                }
                if(Insert(ref child,sample)){children[i]=child;owner.subSystemList=children.ToArray();return true;}
            }
            return false;
        }
        public void Dispose()=>UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(original);
    }
}
