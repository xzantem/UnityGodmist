using System;
using Godmist.Core.Enums;

[Serializable]
public class ResourcePool
{
    public ResourceType ResourceType;
    public int CurrentValue;
    public int MaxValue;
    public int Regeneration;

    // Used by resource rules that need to remember the previous turn.
    public bool HasStartedTurn;
    public bool HasGainedResourceSinceLastTurn;

    // Preserves fractional resource gain, currently used by Momentum above 100.
    public float FractionalGainRemainder;

    public ResourcePool()
    {
    }

    public ResourcePool(
        ResourceType resourceType,
        int currentValue,
        int maxValue,
        int regeneration)
    {
        ResourceType = resourceType;
        CurrentValue = currentValue;
        MaxValue = maxValue;
        Regeneration = regeneration;
    }

    public int Restore(int amount)
    {
        if (amount <= 0 || MaxValue <= 0)
        {
            return 0;
        }

        var previousValue = CurrentValue;

        CurrentValue = Math.Min(
            MaxValue,
            CurrentValue + amount);

        return CurrentValue - previousValue;
    }

    public int Drain(int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        var previousValue = CurrentValue;

        CurrentValue = Math.Max(
            0,
            CurrentValue - amount);

        return previousValue - CurrentValue;
    }
}