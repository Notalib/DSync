using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace DSyncLib.Xml
{
    public interface IXmlSynchronizer
    {
        string Language { get; set; }

        IDictionary<string, XmlSyncPoint> Synchronize(
            XElement textElement, string audioFile, TimeSpan clipBegin, TimeSpan clipEnd, string classRegEx = null);
    }
}
