using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
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
        public IDictionary<string, XmlSyncPoint> Synchronize(XElement textElement, string audioFile, TimeSpan clipBegin, TimeSpan clipEnd, string classRegEx = null)
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
                    var confString = String.Format(
                        "task_language={0}" +
                        "|is_text_type=unparsed" +
                        "|is_text_unparsed_id_sort=unsorted" +
                        "|os_task_file_format=xml" +
                        "|is_audio_file_head_length={1}" +
                        "|is_audio_file_process_length={2}",
                        Language,
                        clipBegin.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture),
                        (clipEnd - clipBegin).TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture));
                    if (String.IsNullOrWhiteSpace(classRegEx))
                    {
                        confString += "|is_text_unparsed_id_regex=.*";
                    }
                    else
                    {
                        confString += "|is_text_unparsed_class_regex=" + classRegEx;
                    }
                    var psi = new ProcessStartInfo
                    {
                        FileName = "python.exe",
                        WorkingDirectory = AeneasRoot,
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        Arguments = String.Format(
                            "-m " +
                            "aeneas.tools.execute_task " +
                            "\"{0}\" " +
                            "\"{1}\" " +
                            "\"{2}\" " +
                            "\"{3}\"",
                            audioFile,
                            inputFile,
                            confString,
                            outputFile)
                    };
                    var process = Process.Start(psi);
                    if (process == null)
                    {
                        throw new ApplicationException(String.Format("Could not start process {0} {1}", psi.FileName, psi.Arguments));
                    }
                    var err = process.StandardError.ReadToEnd();
                    Debug.Print("Standard out:\n" + process.StandardOutput.ReadToEnd());
                    Debug.Print("Standard error:\n" + err);
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        throw new ApplicationException(String.Format("Process {0} {1} exited with code {2}:\n{3}", psi.FileName, psi.Arguments, process.ExitCode, err));
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
                                Double.TryParse(e.Attributes("begin").Select(a => a.Value).FirstOrDefault() ?? "",
                                    out begin)
                                &&
                                Double.TryParse(e.Attributes("end").Select(a => a.Value).FirstOrDefault() ?? "", out end))
                            {
                                return new XmlSyncPoint()
                                {
                                    Id = id,
                                    ClipBegin = TimeSpan.FromMilliseconds(begin),
                                    ClipEnd = TimeSpan.FromMilliseconds(end),
                                    AudioFile = audioFile,
                                    Text = e.Value
                                };
                            }
                            return null;
                        })
                        .Where(s => s != null)
                        .ToList();
                    if (sps.Any())
                    {
                        sps.First().ClipBegin = clipBegin;
                        sps.Last().ClipEnd = clipEnd;
                    }
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