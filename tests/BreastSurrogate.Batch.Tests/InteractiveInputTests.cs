using System.IO;
using BreastSurrogate.Batch;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BreastSurrogate.Batch.Tests
{
    [TestClass]
    public class InteractiveInputTests
    {
        [TestMethod]
        public void PromptsForBothInputPaths()
        {
            var output = new StringWriter();
            string[] arguments = InteractiveInput.PromptForPaths(
                new StringReader("C:\\Audit\r\nC:\\Config\r\n"),
                output);

            CollectionAssert.AreEqual(
                new[] { "C:\\Audit", "C:\\Config" },
                arguments);
            StringAssert.Contains(output.ToString(), "Patient-list CSV");
            StringAssert.Contains(output.ToString(), "JSON configuration");
        }

        [TestMethod]
        public void StartupCheckShortcutIsRetained()
        {
            string[] arguments = InteractiveInput.PromptForPaths(
                new StringReader("T\r\n"),
                new StringWriter());

            CollectionAssert.AreEqual(new[] { "--check-esapi" }, arguments);
        }
    }
}
