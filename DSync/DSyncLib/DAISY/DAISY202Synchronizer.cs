using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using DSyncLib.Xml;

namespace DSyncLib.DAISY
{
    public class DAISY202Synchronizer
    {
        public event EventHandler<DAISYSyncWarningEventArgs> SyncWarning;

        protected void FireSyncWarning(string message, XElement textElem, XElement smilElem)
        {
            var d = SyncWarning;
            if (d == null) return;
            d(
                this,
                new DAISYSyncWarningEventArgs(
                    message,
                    textElem == null ? "" : textElem.Value,
                    textElem == null
                        ? ""
                        : textElem.BaseUri +
                          (textElem.Attributes(XmlUtils.IdAttributeName).Select(a => "#" + a.Value).FirstOrDefault() ??
                           ""),
                    smilElem == null
                        ? ""
                        : smilElem.BaseUri +
                          (smilElem.Attributes("id").Select(a => "#" + a.Value).FirstOrDefault() ?? "")));
        }

        private readonly List<XDocument> smilDocuments = new List<XDocument>();
        private readonly List<XDocument> textDocuments = new List<XDocument>();

        public DAISY202Synchronizer(string aeneasRoot, string defaultLanguage) 
            : this(lang => new AeneasXmlSynchronizer(aeneasRoot) {Language = lang}, defaultLanguage)
        {
        }

        public DAISY202Synchronizer(Func<string, IXmlSynchronizer> syncFactoryFunction, string defaultLanguage)
        {
            if (syncFactoryFunction == null) throw new ArgumentNullException("syncFactoryFunction");

            if (defaultLanguage == null) throw new ArgumentNullException("defaultLanguage");
            syncFactory = syncFactoryFunction;
            DefaultLanguage = defaultLanguage;
            MaximalAudioClipGap = TimeSpan.FromMilliseconds(1);
        }

        public string DefaultLanguage { get; private set; }

        public TimeSpan MaximalAudioClipGap { get; set; }

        private readonly Func<string, IXmlSynchronizer> syncFactory;

        public IXmlSynchronizer GetXmlSynchronizer(string language)
        {
            if (String.IsNullOrWhiteSpace(language))
            {
                language = DefaultLanguage;
            }
            return syncFactory(language);
        }

        public IList<XDocument> SmilDocuments
        {
            get { return smilDocuments; }
        }

        public IList<XDocument> TextDocuments
        {
            get { return textDocuments; }
        }

        public XDocument Ncc { get; private set; }

        public void LoadDTB(string nccPath)
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

        public void SaveDTB(Func<string, int, bool> cancellableProgressDelegate = null)
        {
            if (cancellableProgressDelegate == null)
            {
                cancellableProgressDelegate = (m, p) => false;
            }
            if (Ncc==null)
            {
                throw new InvalidOperationException("No DTB was loaded");
            }
            int i = 0;
            int count = SmilDocuments.Count + TextDocuments.Count + 1;
            if (cancellableProgressDelegate("Saving NCC document", 100*i/count)) return;
            XmlUtils.SaveToBaseUri(Ncc);
            i++;
            foreach (var smilDoc in SmilDocuments)
            {
                if (cancellableProgressDelegate("Saving SMIL documents", 100 * i / count)) return;
                XmlUtils.SaveToBaseUri(smilDoc);
                i++;
            }
            foreach (var textDoc in TextDocuments)
            {
                if (cancellableProgressDelegate("Saving text documents", 100 * i / count)) return;
                XmlUtils.SaveToBaseUri(textDoc);
                i++;
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
            var nccPath = new Uri(Ncc.BaseUri).AbsolutePath.ToLowerInvariant();
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
                            .Select(s => new Uri(new Uri(smil.BaseUri), s).AbsolutePath)
                            .Where(path => path.ToLowerInvariant() != nccPath))
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
            var id = XmlUtils.GetFragmentPart(smilText.Attribute("src"));
            if (id == null)
            {
                return null;
            }
            return TextDocuments
                .Where(doc => new Uri(doc.BaseUri).AbsolutePath == textDocPath)
                .Descendants()
                .FirstOrDefault(e => e.Attributes(XmlUtils.IdAttributeName).Any(a => a.Value==id));
        }

        public static XmlSyncPoint GetSyncPointFromSmilPar(XElement smilPar, XElement textElem, TimeSpan maxClipGap, Action<string, XElement, XElement> fireWarningDelegate)
        {
            if (fireWarningDelegate == null)
            {
                fireWarningDelegate = (m, s, t) => { };
            }
            TimeSpan? begin = null, end = null;
            string audioFile = null;
            foreach (var audio in smilPar.Descendants("audio"))
            {
                var af = audio.Attributes("src").Select(a => a.Value).FirstOrDefault();
                if (String.IsNullOrWhiteSpace(af))
                {
                    fireWarningDelegate("Audio file src is missing", textElem, audio);
                    return null;
                }
                if (audioFile == null)
                {
                    audioFile = af;
                }
                else if (audioFile.ToLowerInvariant() != af.ToLowerInvariant())
                {
                    fireWarningDelegate("Audio file src is differs from the previous", textElem, audio);
                    return null;
                }
                TimeSpan audioBegin, audioEnd;
                try
                {
                    audioBegin = DAISYUtils.ParseClipAttribute(audio, "clip-begin");
                }
                catch (ApplicationException e)
                {
                    fireWarningDelegate("Invalid clip-begin value: " + e.Message, textElem, audio);
                    return null;
                }
                try
                {
                    audioEnd = DAISYUtils.ParseClipAttribute(audio, "clip-end");
                }
                catch (ApplicationException e)
                {
                    fireWarningDelegate("Invalid clip-end value: " + e.Message, textElem, audio);
                    return null;
                }
                if (begin == null)
                {
                    begin = audioBegin;
                }
                if (end != null)
                {
                    if (end.Value.Subtract(audioBegin) > maxClipGap)
                    {
                        fireWarningDelegate("The gap to the previous audio clip is too large", textElem, audio);
                        return null;
                    }
                }
                end = audioEnd;
            }
            if (audioFile == null)
            {
                fireWarningDelegate("Found to audio", textElem, smilPar);
                return null;
            }
            if (!String.IsNullOrWhiteSpace(smilPar.BaseUri))
            {
                audioFile = new Uri(new Uri(smilPar.BaseUri), audioFile).AbsolutePath;
            }
            return new XmlSyncPoint()
            {
                Id = textElem.Attributes(XmlUtils.IdAttributeName).Select(a => a.Value).FirstOrDefault(),
                ClipBegin = begin.Value,
                ClipEnd = end.Value,
                AudioFile = audioFile
            };
        }

        public XmlSyncPoint GetSyncPointFromSmilPar(XElement smilPar, XElement textElem)
        {
            return GetSyncPointFromSmilPar(smilPar, textElem, MaximalAudioClipGap, FireSyncWarning);
        }

        public void SynchronizeSmilParToWordLevel(XElement smilPar)
        {
            var smilText = smilPar.Element("text");
            if (smilText == null)
            {
                FireSyncWarning("Found no child <text> of smil par", null, smilPar);
                return;
            }
            if (smilText.Attribute("id") == null || String.IsNullOrWhiteSpace(smilText.Attribute("id").Value))
            {
                FireSyncWarning("id is missing from smil text", null, smilText);
                return;
            }
            var textElem = GetTextElement(smilText);
            if (textElem == null)
            {
                FireSyncWarning(
                    String.Format("Found no text file element matching src {0}",
                        smilText.Attributes("src").Select(a => a.Value).FirstOrDefault() ?? ""),
                    null,
                    smilText);
                return;
            }
            if (!XmlUtils.AddWordMarkup(textElem, textElem.Name.Namespace + "span", "word", true))
            {
                return;
            }
            var lang = textElem.AncestorsAndSelf().Attributes("lang").Select(a => a.Value).FirstOrDefault();
            
            var syncer = GetXmlSynchronizer(lang);
            if (syncer == null)
            {
                FireSyncWarning("Could not get synchronizer for language " + (lang ?? "<null>"), textElem, smilText);
                return;
            }
            var textFileUri = XmlUtils.GetPathPart(smilText.Attribute("src"));
            var parSyncPoint = GetSyncPointFromSmilPar(smilPar, textElem);
            var audioSrc = smilPar.Descendants("audio").Attributes("src").Select(a => a.Value).First();
            if (parSyncPoint == null)
            {
                return;//SyncWarning has already been fired
            }
            var syncPoints = syncer.Synchronize(textElem, parSyncPoint.AudioFile, parSyncPoint.ClipBegin, parSyncPoint.ClipEnd, "word");
            foreach (var id in syncPoints.Keys.Where(k => syncPoints[k].ClipBegin>=syncPoints[k].ClipEnd).ToList())
            {
                syncPoints.Remove(id);
            }
            if (syncPoints.Values.Count(sp => sp.ClipBegin < sp.ClipEnd) <= 1) return;
            var oldSmilParId = smilPar.Attributes("id").Select(a => a.Value).FirstOrDefault();
            var oldSmilTextId = smilPar.Descendants("text").Attributes("id").Select(a => a.Value).FirstOrDefault();
            XElement firstPar = null, firstText = null;
            smilPar.RemoveAttributes();
            smilPar.RemoveNodes();
            foreach (var sp in syncPoints.Values.OrderBy(o => o))
            {
                var newText = new XElement(
                    "text",
                    new XAttribute("src", String.Concat(textFileUri, "#", sp.Id)));
                if (firstText == null)
                {
                    firstText = newText;
                }
                var newPar = new XElement(
                    "par",
                    new XAttribute("endsync", "last"),
                    newText,
                    new XElement(
                        "audio",
                        new XAttribute("clip-begin", DAISYUtils.GetClipAttributeValue(sp.ClipBegin)),
                        new XAttribute("clip-end", DAISYUtils.GetClipAttributeValue(sp.ClipEnd)),
                        new XAttribute("src", audioSrc)));
                if (firstPar == null)
                {
                    firstPar = newPar;
                }
                smilPar.AddBeforeSelf(newPar);
                newText.SetAttributeValue("id", XmlUtils.GetId(newText.Document, sp.Id));
            }
            smilPar.Remove();
            if (oldSmilParId != null && firstPar != null)
            {
                firstPar.SetAttributeValue("id", oldSmilParId);
            }
            if (oldSmilTextId != null && firstText != null)
            {
                firstText.SetAttributeValue("id", oldSmilTextId);
            }
        }

        public void SynchronizeSmilParsToWordLevel(Func<string,int,bool> cancellableProgressDelegate = null)
        {
            if (cancellableProgressDelegate == null)
            {
                cancellableProgressDelegate = (m, p) => false;
            }
            if (Ncc == null)
            {
                throw new InvalidOperationException("No DTB was loaded");
            }
            if (TextDocuments.Count==0)
            {
                FireSyncWarning("Cannot synchronize what seems to be a audio only DAISY 2.02 DTB", null, null);
                return;
            }
            var smilPars =
                SmilDocuments
                    .Descendants("body")
                    .Elements("seq")
                    .Elements("par").ToList();
            var nccUri = new Uri(Ncc.BaseUri);
            for (int i = 0; i < smilPars.Count; i++)
            {
                var msg = String.Format(
                    "Synchronizing smil par {0}#{1}",
                    nccUri.MakeRelativeUri(new Uri(smilPars[i].BaseUri)), smilPars[i].Attributes("id").Select(a => a.Value).FirstOrDefault()??"");
                if (cancellableProgressDelegate(msg, i*100/smilPars.Count)) break;
                SynchronizeSmilParToWordLevel(smilPars[i]);
            }
        }

    }
}
