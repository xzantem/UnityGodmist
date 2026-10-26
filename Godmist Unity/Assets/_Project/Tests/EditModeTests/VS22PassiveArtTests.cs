using NUnit.Framework;

public sealed class VS22PassiveArtTests
{
    [Test]
    public void AllProductionPassiveTreesUseApprovedIconsAndGrantedSkillIconReuse()
    {
        Assert.DoesNotThrow(VS22RemainingPassiveSetup.VerifyAllProduction);
    }
}
