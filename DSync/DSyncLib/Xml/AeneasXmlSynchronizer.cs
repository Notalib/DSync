using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace DSyncLib.Xml
{
    public class AeneasXmlSynchronizer : IXmlSynchronizer
    {
        public AeneasXmlSynchronizer(string aeneasRoot)
        {
            AeneasRoot = aeneasRoot;
        }

        public string AeneasRoot { get; private set; }

        public string Language { get; set; }
        public IDictionary<string, XmlSyncPoint> Synchronize(XElement textElement, string audioFile, TimeSpan clipBegin, TimeSpan clipEnd)
        {
            var inputFile = Path.GetTempFileName();
            try
            {
                var writer = XmlWriter.Create(inputFile);
                try
                {
                    textElement.WriteTo(writer);
                }
                finally
                {
                    writer.Close();
                }
                var outputFile = Path.GetTempFileName();
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "python.exe",
                        WorkingDirectory = AeneasRoot,
                        UseShellExecute = false,
                        Arguments = String.Format(
                            "-m aeneas.tools.execute_task \"{0}\" \"{1}\" \"task_language={2}|is_text_type=unparsed|is_text_unparsed_id_sort=unsorted|is_text_unparsed_id_regex=.*|os_task_file_format=xml\" \"{3}\"",
                            audioFile,
                            inputFile,
                            Language,
                            outputFile)
                    };
                    var process = Process.Start(psi);
                    if (process == null)
                    {
                        throw new ApplicationException(String.Format("Could not start process {0} {1}", psi.FileName, psi.Arguments));
                    }
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        throw new ApplicationException(String.Format("Process {0} {1} exited with code {2}", psi.FileName, psi.Arguments, process.ExitCode));
                    }
                    var map = XDocument.Load(outputFile).Root;
                    if (map == null) throw new ApplicationException("No sync map returned from aeneas");
                    var res = new Dictionary<string, XmlSyncPoint>();
                    var sps = map
                        .Elements("fragment")
                        .Select(e =>
                        {
                            var id = e.Attributes("id").Select(a => a.Value).FirstOrDefault();
                            double begin, end;
                            if (
                                Double.TryParse(e.Attributes("begin").Select(a => a.Value).FirstOrDefault() ?? "", out begin)
                                &&
                                Double.TryParse(e.Attributes("end").Select(a => a.Value).FirstOrDefault() ?? "", out end))
                            {
                                return new XmlSyncPoint(
                                    id, TimeSpan.FromMilliseconds(begin), TimeSpan.FromMilliseconds(end));
                            }
                            return null;
                        })
                        .Where(s => s != null);
                    foreach (var sp in sps)
                    {
                        res.Add(sp.Id, sp);
                    }
                    return res;
                }
                finally
                { 
                    File.Delete(outputFile);
                }
            }
            finally
            {
                File.Delete(inputFile);
            }
        }
    }
}