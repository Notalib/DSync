using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            if (doc == null) return idPrefix + "_1";
            var ids = new HashSet<string>(doc.Descendants().Attributes(IdAttributeName).Select(a => a.Value));
            return GetId(ids, idPrefix);
        }

        public static string GetId(HashSet<string> ids, string idPrefix)
        {
            if (ids == null) throw new ArgumentNullException("ids");
            if (idPrefix == null) throw new ArgumentNullException("idPrefix");
            string id = null;
            int idNo = 0;
            do
            {
                idNo++;
                id = String.Format("{0}_{1}", idPrefix, idNo);
            }
            while (ids.Contains(id));
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
            var reader = XmlReader.Create(path, new XmlReaderSettings() {DtdProcessing = DtdProcessing.Ignore});
            try
            {
                return XDocument.Load(reader, LoadOptions.SetBaseUri | LoadOptions.SetLineInfo);
            }
            finally
            {
                reader.Close();
            }
        }

        public static string GetAbsPath(XAttribute a)
        {
            if (a == null) return null;
            return new Uri(new Uri(a.BaseUri), GetPathPart(a)).AbsolutePath;
        }

        public static string GetFragmentpart(XAttribute a)
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
    }
}
