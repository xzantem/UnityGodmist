using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public sealed class CatacombsTilesetConfigurationTests
    {
        [Test]
        public void ApprovedCatacombsTilesetBindingsRemainComplete()
        {
            Assert.DoesNotThrow(VS22CatacombsTilesetSetup.VerifyAll);
        }
    }
}
