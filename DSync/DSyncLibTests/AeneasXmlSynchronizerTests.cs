using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using DSyncLib.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSyncLibTests
{
    [TestClass]
    public class AeneasXmlSynchronizerTests
    {
        public const string AeanasRoot = @"C:\Users\oha\Projects\aeneas-master";

        [TestMethod]
        public void Synchronize()
        {
            var sync = new AeneasXmlSynchronizer(AeanasRoot) {Language = "en"};
            var audioFile = Path.Combine(sync.AeneasRoot, @"aeneas\tests\res\audioformats\p001.mp3");
            var textElem = new XElement(
                "div",
                new XElement("p", new XAttribute("id", "f000001"), "1"),
                new XElement("p", new XAttribute("id", "f000002"), "From fairest creatures we desire increase"),
                new XElement("p", new XAttribute("id", "f000003"), "That thereby beauty's rose might never die"));
            var sps = sync.Synchronize(textElem, audioFile, TimeSpan.Zero, TimeSpan.Zero);
            Assert.AreEqual(textElem.Descendants("p").Count(e => e.Attribute("id") != null), sps.Count);
        }
    }
}
