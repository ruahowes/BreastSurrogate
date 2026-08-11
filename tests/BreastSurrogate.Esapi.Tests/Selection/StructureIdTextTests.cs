using BreastSurrogate.Esapi.Esapi;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BreastSurrogate.Esapi.Tests.Selection
{
    [TestClass]
    public class StructureIdTextTests
    {
        [TestMethod]
        public void NormalizeRemovesSeparatorsAndUsesInvariantUppercase()
        {
            Assert.AreEqual("LLUNG1", StructureIdText.Normalize(" L_Lung-1 "));
            Assert.AreEqual(string.Empty, StructureIdText.Normalize(null));
        }

        [TestMethod]
        public void ContainsIsCaseInsensitiveWithoutFuzzyMatching()
        {
            Assert.IsTrue(StructureIdText.Contains("avoid_HEART_prv", "Heart"));
            Assert.IsFalse(StructureIdText.Contains("Cardiac", "Heart"));
        }

        [TestMethod]
        public void EditDistanceUsesStandardInsertDeleteSubstituteCost()
        {
            Assert.AreEqual(0, StructureIdText.EditDistance("HEART", "HEART"));
            Assert.AreEqual(3, StructureIdText.EditDistance("HEARTPRV", "HEART"));
            Assert.AreEqual(3, StructureIdText.EditDistance("KITTEN", "SITTING"));
        }
    }
}
