using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace DSyncLib.Xml
{
    public static class XmlUtils
    {
        public static long ParseSyncAttribute(XElement elem, XName attrName)
        {
            if (elem == null) throw new ArgumentNullException("elem");
            if (attrName == null) throw new ArgumentNullException("attrName");
            var attr = elem.Attribute(attrName);
            if (attr == null) throw new XmlException("Attribute " + attrName + " is missing");
            return ParseSyncAttribute(attr);
        }

        public static long ParseSyncAttribute(XAttribute syncAttr)
        {
            if (syncAttr == null) throw new ArgumentNullException("syncAttr");
            long res;
            if (Int64.TryParse(syncAttr.Value, out res)) return res;
            throw new XmlException(String.Format("Invalid synchronization attribute {0}", syncAttr));
        }

        public static bool HasSyncAttribute(XElement elem, XName attrName)
        {
            if (elem == null) throw new ArgumentNullException("elem");
            if (attrName == null) throw new ArgumentNullException("attrName");
            var attr = elem.Attribute(attrName);
            if (attr == null) return false;
            long v;
            return Int64.TryParse(attr.Value, out v);
        }

        public static void SaveDocuments(IDictionary<string, XDocument> documentCache)
        {
            foreach (var doc in documentCache.Values) doc.Save(new Uri(doc.BaseUri).LocalPath);
        }

        public static HashSet<string> GetDocumentIds(XDocument doc)
        {
            if (doc == null) return new HashSet<string>();
            return new HashSet<string>(doc.Descendants().Attributes(XmlUtils.IdAttributeName).Select(a => a.Value));
        }

        public static XAttribute GetIdAttr(HashSet<string> ids, string idPrefix)
        {
            return new XAttribute(IdAttributeName, GetId(ids, idPrefix));
        }

        public static XAttribute GetIdAttr(XDocument doc, string idPrefix)
        {
            return new XAttribute(IdAttributeName, GetId(doc, idPrefix));
        }

        public static string GetId(XDocument doc, string idPrefix)
        {
            if (idPrefix == null) throw new ArgumentNullException("idPrefix");
            if (doc == null) return idPrefix;
            var ids = new HashSet<string>(doc.Descendants().Attributes(IdAttributeName).Select(a => a.Value));
            return GetId(ids, idPrefix);
        }

        public static string GetId(HashSet<string> ids, string idPrefix)
        {
            if (ids == null) throw new ArgumentNullException("ids");
            if (idPrefix == null) throw new ArgumentNullException("idPrefix");
            string id = idPrefix;
            int idNo = 0;
            while (ids.Contains(id))
            {
                idNo++;
                id = String.Format("{0}_{1}", idPrefix, idNo);
            }
            ids.Add(id);
            return id;
        }

        public static ICollection<XElement> GetNonNestedDescendants(XElement elem, XName name)
        {
            var res = new List<XElement>();
            foreach (var child in elem.Elements())
            {
                if (child.Name == name)
                {
                    res.Add(child);
                }
                else
                {
                    res.AddRange(GetNonNestedDescendants(child, name));
                }
            }
            return res;
        }

        /// <summary>
        /// The name of the attribute used to identify elements during synchronization - default is <c>"id"</c>
        /// </summary>
        public static XName IdAttributeName = "id";

        /// <summary>
        /// The namespace used to store synchronization attributes (and temporary elements)
        /// </summary>
        public static XNamespace SyncNamespace = "http://www.nota.nu/DSyncLib/Xml";

        public static void RemoveSyncAttributes(XElement elem)
        {
            foreach (var attr in elem.Attributes().Where(a => a.Name.Namespace == SyncNamespace).ToList()) elem.SetAttributeValue(attr.Name, null);
            foreach (var child in elem.Elements().ToList()) RemoveSyncAttributes(child);
        }

        public static string GetLocalPath(XAttribute srcAttr)
        {
            if (srcAttr == null) throw new ArgumentNullException("srcAttr");
            if (String.IsNullOrWhiteSpace(srcAttr.BaseUri)) return new Uri(srcAttr.Value).LocalPath;
            return new Uri(new Uri(srcAttr.BaseUri), srcAttr.Value).LocalPath;
        }

        public static XDocument LoadDocumentWithBaseUri(string path)
        {
            var reader = XmlReader.Create(path, new XmlReaderSettings() {DtdProcessing = DtdProcessing.Ignore, IgnoreWhitespace = true});
            try
            {
                return XDocument.Load(reader, LoadOptions.SetBaseUri | LoadOptions.SetLineInfo);
            }
            finally
            {
                reader.Close();
            }
        }

        public static void SaveToBaseUri(XDocument doc)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            var writer = XmlWriter.Create(
                new Uri(doc.BaseUri).AbsolutePath,
                new XmlWriterSettings() {
                    Indent = true, 
                    IndentChars = "  ",
                    Encoding = Encoding.GetEncoding(doc.Declaration.Encoding)
                });
            try
            {
                doc.WriteTo(writer);
            }
            finally
            {
                writer.Close();
            }
        }

        public static string GetAbsPath(XAttribute a)
        {
            if (a == null) return null;
            return new Uri(new Uri(a.BaseUri), GetPathPart(a)).AbsolutePath;
        }

        public static string GetFragmentPart(XAttribute a)
        {
            if (a == null) return null;
            var index = a.Value.IndexOf('#');
            return index != -1 ? a.Value.Substring(index + 1).ToLowerInvariant() : null;
        }

        public static string GetPathPart(XAttribute a)
        {
            if (a == null) return null;
            var index = a.Value.IndexOf('#');
            return (index == -1 ? a.Value : a.Value.Substring(0, index)).ToLowerInvariant();
        }

        /// <summary>
        /// Adds word level markup to an text <see cref="XElement"/>
        /// </summary>
        /// <param name="textElement">The text <see cref="XElement"/></param>
        /// <param name="wordElementName">The name of the element to mark up words with</param>
        /// <param name="wordClassName">The class attribute value for word markup elements</param>
        /// <param name="recursive">A <see cref="bool"/> indicating if word markup should be recursively added for descendants of <paramref name="textElement"/></param>
        /// <param name="defaultIdPrefix">The default id prefix, used id the element does not have an id</param>
        /// <returns>A <see cref="bool"/> indicating if any word level markup was actually added</returns>
        public static bool AddWordMarkup(XElement textElement, XName wordElementName, string wordClassName, bool recursive = true, string defaultIdPrefix = null)
        {
            bool res = false;
            var idPrefix = textElement.Attributes("id").Select(a => a.Value).FirstOrDefault()
                              ?? (defaultIdPrefix??textElement.Name.LocalName);
            foreach (var child in textElement.Nodes().ToList())
            {
                var text = child as XText;
                if (text != null)
                {
                    if (AddWordMarkup(text, wordElementName, wordClassName, idPrefix)) res = true;
                }
                if (recursive)
                {
                    var element = child as XElement;
                    if (element != null)
                    {
                        if (AddWordMarkup(element, wordElementName, wordClassName, true, idPrefix)) res = true;
                    }
                }
            }
            return res;
        }

        /// <summary>
        /// Adds word level markup to an <see cref="XText"/>
        /// </summary>
        /// <param name="textNode">The <see cref="XText"/></param>
        /// <param name="wordElementName">The name of the element to mark up words with</param>
        /// <param name="wordClassName">The class attribute value for word markup elements</param>
        /// <param name="idPrefix">A prefix for id attributes of word markup elements</param>
        /// <returns>A <see cref="bool"/> indicating if any word level markup was actually added</returns>
        public static bool AddWordMarkup(XText textNode, XName wordElementName, string wordClassName, string idPrefix)
        {
            var doc = textNode.Document;
            var ids = doc == null
                          ? new HashSet<string>()
                          : new HashSet<string>(doc.Descendants().Attributes(IdAttributeName).Select(a => a.Value));
            var expr = new Regex(@"\b\w+\b");
            if (expr.Matches(textNode.Value).Count <= 1) return false;
            var match = expr.Match(textNode.Value);
            while (match.Success)
            {
                if (match.Index > 0) textNode.AddBeforeSelf(new XText(textNode.Value.Substring(0, match.Index)));
                var wordElem = new XElement(wordElementName, GetIdAttr(ids, idPrefix), match.Value);
                if (!String.IsNullOrWhiteSpace(wordClassName))
                {
                    wordElem.SetAttributeValue("class", wordClassName);
                }
                textNode.AddBeforeSelf(wordElem);
                textNode.Value = textNode.Value.Substring(match.Index + match.Length);
                if (textNode.Value.Length == 0) textNode.Remove();
                match = expr.Match(textNode.Value);
            }
            return true;
        }
    }
}
