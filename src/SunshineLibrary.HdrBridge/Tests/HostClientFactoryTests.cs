using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using SunshineLibrary.Models;
using SunshineLibrary.Services.Hosts;

namespace SunshineLibrary.Tests
{
    [TestClass]
    public class HostClientFactoryTests
    {
        [TestMethod]
        public void StandardConfigShape_ClassifiesAsSunshineBeforeCapabilityProbe()
        {
            var type = HostClientFactory.ClassifyConfig(
                HostResult<JObject>.Ok(JObject.Parse(@"{""platform"":""windows"",""version"":""1""}")));
            Assert.AreEqual(ServerType.Sunshine, type);
        }

        [TestMethod]
        public void VirtualDisplayMarker_ClassifiesAsApolloBeforeCapabilityProbe()
        {
            var type = HostClientFactory.ClassifyConfig(
                HostResult<JObject>.Ok(JObject.Parse(@"{""platform"":""windows"",""version"":""1"",""vdisplayStatus"":{}}")));
            Assert.AreEqual(ServerType.Apollo, type);
        }

        [TestMethod]
        public void Vibeshine_RetainsLegacyNumericValue()
        {
            Assert.AreEqual(3, (int)ServerType.Vibeshine);
        }
    }
}
