using BreastSurrogate.Esapi.Esapi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VMS.TPS.Common.Model.Types;

namespace BreastSurrogate.Esapi.Tests.Calculation
{
    [TestClass]
    public class EsapiBeamGeometryFactoryTests
    {
        [TestMethod]
        public void StaticPlanTypeUsesMlcAperture()
        {
            Assert.IsTrue(EsapiBeamGeometryFactory.UsesStaticMlc(MLCPlanType.Static));
        }

        [TestMethod]
        public void NotDefinedPlanTypeUsesJawOnlyAperture()
        {
            Assert.IsFalse(EsapiBeamGeometryFactory.UsesStaticMlc(MLCPlanType.NotDefined));
            Assert.IsTrue(EsapiBeamGeometryFactory.IsSupportedMlcPlanType(
                MLCPlanType.NotDefined));
        }

        [TestMethod]
        public void DynamicMlcPlanTypesRemainUnsupported()
        {
            Assert.IsFalse(EsapiBeamGeometryFactory.IsSupportedMlcPlanType(
                MLCPlanType.DoseDynamic));
            Assert.IsFalse(EsapiBeamGeometryFactory.IsSupportedMlcPlanType(
                MLCPlanType.ArcDynamic));
            Assert.IsFalse(EsapiBeamGeometryFactory.IsSupportedMlcPlanType(
                MLCPlanType.VMAT));
        }
    }
}
