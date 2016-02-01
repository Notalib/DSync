using System;
using DSyncLib.DAISY;

namespace DSyncCon
{
    class Program
    {
        static bool ProgressHandler(string msg, int prog)
        {
            WriteStatus("{0} ({1} %)", msg, prog);
            return false;
        }

        static void WriteStatus(string format, params object[] args)
        {
            while (Console.CursorLeft > 0) Console.Write("\b");
            Console.Write(format, args);
        }

        static void WriteMessage(string format, params object[] args)
        {
            Console.WriteLine(format, args);
        }

        private const string Usage = "DSyncCon <lang> <aeneas_dir> <ncc>";

        static int Main(string[] args)
        {
            try
            {
                if (args.Length != 3)
                {
                    WriteMessage("Invalid number of arguments");
                    return -1;
                }
                var syncer = new DAISY202Synchronizer(args[1], args[0]);
                syncer.LoadDTB(args[2]);
                syncer.SyncWarning +=
                    (sender, eventArgs) =>
                        WriteMessage("[SYNCWARN]: {0} (smil {1}, text {2}", eventArgs.Message, eventArgs.SmilElementUri,
                            eventArgs.Text);
                syncer.SynchronizeSmilParsToWordLevel(ProgressHandler);
                syncer.SaveDTB(ProgressHandler);
                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine(
                    "An unexpected {0} occured:\n{1}\nStack Trace:\n{2}",
                    e.GetType(),
                    e.Message,
                    e.StackTrace);
                return e.HResult == 0 ? -2 : e.HResult;
            }
        }
    }
}
