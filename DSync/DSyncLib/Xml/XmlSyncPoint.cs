using System;

namespace DSyncLib.Xml
{
    public class XmlSyncPoint : IComparable<XmlSyncPoint>
    {
        public XmlSyncPoint(string id, TimeSpan clipBegin, TimeSpan clipEnd)
        {
            Id = id;
            ClipBegin = clipBegin;
            ClipEnd = clipEnd;
        }

        public string Id { get; private set; }
        public TimeSpan ClipBegin { get; private set; }
        public TimeSpan ClipEnd { get; private set; }
        
        public int CompareTo(XmlSyncPoint other)
        {
            if (other == null) return 1;
            var res = ClipBegin.CompareTo(other.ClipBegin);
            if (res == 0)
            {
                return ClipEnd.CompareTo(other.ClipEnd);
            }
            return res;
        }
    }
}