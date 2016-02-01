using System;

namespace DSyncLib.Xml
{
    public class XmlSyncPoint : IComparable<XmlSyncPoint>
    {
        //public XmlSyncPoint(string id, TimeSpan clipBegin, TimeSpan clipEnd, string audioFile)
        //{
        //    Id = id;
        //    ClipBegin = clipBegin;
        //    ClipEnd = clipEnd;
        //    AudioFile = audioFile;
        //}

        public string Id { get; set; }
        public TimeSpan ClipBegin { get; set; }
        public TimeSpan ClipEnd { get; set; }
        public string AudioFile { get; set; }
        public string Text { get; set; }
        
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