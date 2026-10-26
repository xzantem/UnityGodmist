using Unity.Profiling;

namespace Godmist.Views.Exploration
{
    public partial class ExplorationPartyController
    {
        private static readonly ProfilerMarker LeaderTickSample = new("Godmist.Exploration.LeaderTick");
        private static readonly ProfilerMarker NewTargetSample = new("Godmist.Exploration.NewTarget");
        private static readonly ProfilerMarker TargetResolutionSample = new("Godmist.Exploration.NewTarget.Resolve");
        private static readonly ProfilerMarker TargetEndpointSample = new("Godmist.Exploration.TargetEndpoint");
        private static readonly ProfilerMarker TargetSmoothingSample = new("Godmist.Exploration.NewTarget.Smooth");
        private static readonly ProfilerMarker FollowersTickSample = new("Godmist.Exploration.FollowersTick");
        private static readonly ProfilerMarker PersistPositionSample = new("Godmist.Exploration.PersistPosition");
        private static readonly ProfilerMarker FollowerGoalSample = new("Godmist.Exploration.FollowerGoal");
        private static readonly ProfilerMarker FollowerDirectSample = new("Godmist.Exploration.FollowerDirect");
        private static readonly ProfilerMarker FollowerPlanSample = new("Godmist.Exploration.FollowerPlan");
        private static readonly ProfilerMarker FollowerMoveSample = new("Godmist.Exploration.FollowerMove");
        private static readonly ProfilerMarker NavigationRevisionSample = new("Godmist.Navigation.Revision");
        private static readonly ProfilerMarker ObstacleIndexSample = new("Godmist.Navigation.ObstacleIndex");
        private static readonly ProfilerMarker DirectValidationSample = new("Godmist.Navigation.DirectValidation");
        private static readonly ProfilerMarker MeshPlanSample = new("Godmist.Navigation.MeshPlan");
        private static readonly ProfilerMarker TraversalPlanSample = new("Godmist.Navigation.TraversalPlan");
    }
}
