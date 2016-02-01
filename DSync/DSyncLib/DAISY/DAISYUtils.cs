using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DSyncLib.DAISY
{
    public static class DAISYUtils
    {
        public static TimeSpan ParseClipAttribute(XElement elem, XName attrName)
        {
            if (elem == null) throw new ArgumentNullException("elem");
            if (attrName == null) throw new ArgumentNullException("attrName");
            var attr = elem.Attribute(attrName);
            if (attr == null) throw new ApplicationException("Clip attribute " + attrName + " is missing");
            return ParseClipAttribute(attr);
        }

        public static TimeSpan ParseClipAttribute(XAttribute attr)
        {
            if (attr == null) throw new ArgumentNullException("attr");
            var m = Regex.Match(attr.Value, @"^npt=(\d+(\.\d+)?)s$");
            if (!m.Success) throw new ApplicationException("Clip attribute " + attr.Name + " has invalid value " + attr.Value);
            return TimeSpan.FromSeconds(Double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
        }

        public static string GetClipAttributeValue(TimeSpan offset)
        {
            return String.Format("npt={0}s", offset.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture));
        }
    }
}
