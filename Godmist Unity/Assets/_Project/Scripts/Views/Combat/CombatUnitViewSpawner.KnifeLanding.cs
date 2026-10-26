using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Views.Combat
{
    public partial class CombatUnitViewSpawner
    {
        private static void PlaceGroundKnife(KnifeCombatMapObject knife, SpriteRenderer renderer, Vector3 destination)
        {
            // The ground pickup has no flight/landing presentation. Its logical
            // destination is also its first rendered position, even after reveal.
            knife.LandingOrigin = null;
            renderer.transform.position = destination;
            renderer.transform.localRotation = Quaternion.identity;
        }
    }
}
