using System;
using System.Collections.Generic;
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
            var smilSync = new DAISY202Synchronizer(AeneasXmlSynchronizerTests.AeanasRoot, "da");
            smilSync.LoadDTB(NccPath);
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
            int i = 0;
            foreach (var smilText in smilSync.SmilDocuments.Descendants("text"))
            {
                Assert.IsNotNull(smilText.Attributes("src"));
                var textElement = smilSync.GetTextElement(smilText);
                Assert.IsNotNull(textElement, String.Format("Could not find text element matching {0}", smilText));
                Assert.IsNotNull(textElement.Attribute("id"));
                Assert.IsTrue(
                    String
                        .Format("{0}#{1}", textElement.BaseUri, textElement.Attribute("id").Value)
                        .EndsWith(smilText.Attribute("src").Value),
                    "The BaseUri and id of the found text element does not match the src attribute of the smil text element");
                i++;
            }
            Console.WriteLine("Found {0} text elements", i);
        }

        [TestMethod]
        public void GetSyncPointFromSmilParTest()
        {
            var smilSync = LoadNcc();
            var warnings = new List<DAISYSyncWarningEventArgs>();
            smilSync.SyncWarning += (sender, args) => warnings.Add(args);
            foreach (var smilPar in smilSync.SmilDocuments.Descendants("body").Elements("seq").Elements("par"))
            {
                warnings.Clear();
                var textElem = smilSync.GetTextElement(smilPar.Element("text"));
                Assert.IsNotNull(textElem);
                var sp = smilSync.GetSyncPointFromSmilPar(smilPar, textElem);
                if (sp == null)
                {
                    Assert.IsTrue(warnings.Count>0, "No sync point returned and no warnings");
                    Console.WriteLine(
                        "Could not get sync point for {0}: {1}", 
                        smilPar,
                        warnings.Select(w => w.Message).Aggregate((s,v) => s + ";" + v));
                }
            }
        }

        
        [TestMethod]
        public void GetSyncPointFromSmilParStaticTest()
        {
            var smilPar =
                XElement.Parse(
                    "<par endsync='last' id='sfe_par_0012_0014'>" +
                    "<text src='17929.htm#pvxg00265' id='pvxg00265' />" +
                    "<seq id='sfe_seq_0012_0015'>" +
                    "<audio src='hod_12.mp3' clip-begin='npt=105.399s' clip-end='npt=108.159s' id='qwrt_000e' />" +
                    "<audio src='hod_12.mp3' clip-begin='npt=108.159s' clip-end='npt=108.758s' id='qwrt_000f' />" +
                    "</seq>" +
                    "</par>");
            
            var textElem = XElement.Parse("<p id='pvxg00265'>Nå, sagde Hodja.</p>");
            var sp = DAISY202Synchronizer.GetSyncPointFromSmilPar(smilPar, textElem, TimeSpan.FromMilliseconds(1), null);
            Assert.IsNotNull(sp);
            Assert.AreEqual("hod_12.mp3", sp.AudioFile);
            Assert.AreEqual(TimeSpan.FromMilliseconds(105399), sp.ClipBegin);
            Assert.AreEqual(TimeSpan.FromMilliseconds(108758), sp.ClipEnd);
            Assert.AreEqual("pvxg00265", sp.Id);
        }


        [TestMethod]
        [Ignore]
        public void SynchronizeSmilParToWordLevelTest()
        {
            var smilSync = LoadNcc();
            var warnings = new List<DAISYSyncWarningEventArgs>();
            smilSync.SyncWarning += (sender, args) => warnings.Add(args);
            var textsBefore = smilSync.SmilDocuments.Descendants("text").Count();
            foreach (var smilPar in smilSync
                .SmilDocuments
                .Descendants("body")
                .Elements("seq")
                .Elements("par").ToList())
            {
                smilSync.SynchronizeSmilParToWordLevel(smilPar);
            }
            var textsAfter = smilSync.SmilDocuments.Descendants("text").Count();
            Assert.IsTrue(textsBefore < textsAfter, "No smil texts were added during sync");
            Console.WriteLine(
                "{0} sync warnings occured, there was {1} text sync points before and {2} after", 
                warnings.Count, textsBefore, textsAfter);
        }
    }
}
