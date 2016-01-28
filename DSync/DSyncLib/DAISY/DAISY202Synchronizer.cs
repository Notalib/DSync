using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using DSyncLib.Xml;

namespace DSyncLib.DAISY
{
    public class DAISY202Synchronizer
    {
        private readonly List<XDocument> smilDocuments = new List<XDocument>();
        private readonly List<XDocument> textDocuments = new List<XDocument>();

        public DAISY202Synchronizer(IXmlSynchronizer xmlSynchronizer)
        {
            XmlSynchronizer = xmlSynchronizer;
        }

        public IXmlSynchronizer XmlSynchronizer { get; private set; }

        public IList<XDocument> SmilDocuments
        {
            get { return smilDocuments; }
        }

        public IList<XDocument> TextDocuments
        {
            get { return textDocuments; }
        }

        public XDocument Ncc { get; private set; }

        public void LoadNcc(string nccPath)
        {
            try
            {
                Ncc = XmlUtils.LoadDocumentWithBaseUri(nccPath);
                LoadSmilDocuments();
                LoadTextDocuments();
            }
            catch (Exception)
            {
                Ncc = null;
                SmilDocuments.Clear();
                TextDocuments.Clear();
                throw;
            }
        }

        private void LoadSmilDocuments()
        {
            if (Ncc.Root == null)
            {
                SmilDocuments.Clear();
                return;
            }
            var ns = Ncc.Root.Name.Namespace;
            var nccUri = new Uri(Ncc.BaseUri);
            var smils =
                Ncc
                    .Descendants()
                    .Where(e => new[] {"h1", "h2", "h3", "h4", "h5", "h6"}.Contains(e.Name.LocalName))
                    .Elements(ns + "a")
                    .Attributes("href")
                    .Select(XmlUtils.GetPathPart)
                    .Where(s => !String.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .Select(s => new Uri(nccUri, s).AbsolutePath.ToLowerInvariant())
                    .Select(XmlUtils.LoadDocumentWithBaseUri)
                    .ToList();
            smilDocuments.Clear();
            smilDocuments.AddRange(smils);

        }

        private void LoadTextDocuments()
        {
            var nccUri = new Uri(Ncc.BaseUri);
            var texts =
                SmilDocuments
                    .Select(smil => smil.Root)
                    .Where(smil => smil != null)
                    .SelectMany(smil => 
                        smil
                            .Descendants(smil.Name.Namespace + "text")
                            .Attributes("src")
                            .Select(XmlUtils.GetPathPart)
                            .Where(s => !String.IsNullOrWhiteSpace(s))
                            .Select(s => new Uri(new Uri(smil.BaseUri), s).AbsolutePath))
                    .Distinct()
                    .Select(XmlUtils.LoadDocumentWithBaseUri)
                    .ToList();
            textDocuments.Clear();
            textDocuments.AddRange(texts);
        }

        public XElement GetTextElement(XElement smilText)
        {
            var textDocPath = XmlUtils.GetAbsPath(smilText.Attribute("src"));
            if (textDocPath == null)
            {
                return null;
            }
            var id = XmlUtils.GetFragmentpart(smilText.Attribute("src"));
            if (id == null)
            {
                return null;
            }
            return TextDocuments
                .Where(doc => new Uri(doc.BaseUri).AbsolutePath == textDocPath)
                .Descendants()
                .FirstOrDefault(e => e.Attributes("id").Any(a => a.Value==id));
        }
    }
}
