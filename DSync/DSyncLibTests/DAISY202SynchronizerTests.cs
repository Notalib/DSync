using System;
using System.Linq;
using System.Xml.Linq;
using DSyncLib.DAISY;
using DSyncLib.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSyncLibTests
{
    [TestClass]
    public class DAISY202SynchronizerTests
    {
        public const string NccPath = @"C:\DTBs\17929\Hodja_fra_Pjort\17929\ncc.html";

        private DAISY202Synchronizer LoadNcc()
        {
            var xmlSync = new AeneasXmlSynchronizer(AeneasXmlSynchronizerTests.AeanasRoot);
            var smilSync = new DAISY202Synchronizer(xmlSync);
            smilSync.LoadNcc(NccPath);
            return smilSync;
        }

        [TestMethod]
        public void LoadNccTest()
        {
            var smilSync = LoadNcc();
            Assert.IsNotNull(smilSync.Ncc.Root);
            var nccNs = smilSync.Ncc.Root.Name.Namespace;
            var smilCount =
                smilSync
                    .Ncc
                    .Descendants(nccNs + "a")
                    .Attributes("href")
                    .Select(XmlUtils.GetPathPart)
                    .Distinct()
                    .Count();
            Assert.AreEqual(smilCount, smilSync.SmilDocuments.Count, "Unexpected number of smil files");
            var textCount =
                smilSync
                    .SmilDocuments
                    .Descendants("text")
                    .Attributes("src")
                    .Select(XmlUtils.GetPathPart)
                    .Distinct()
                    .Count();
            Assert.AreEqual(textCount, smilSync.TextDocuments.Count, "Unexpected number of text files");
        }

        [TestMethod]
        public void GetTextElementTest()
        {
            var smilSync = LoadNcc();
            var smilText = smilSync.SmilDocuments.Descendants("text").FirstOrDefault();
            Assert.IsNotNull(smilText, "Fould no smil text element");
            var textElement = smilSync.GetTextElement(smilText);
            Assert.IsNotNull(textElement, String.Format("Could not find text element matching {0}", smilText));
        }
    }
}
