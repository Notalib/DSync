namespace DSyncLib.DAISY
{
    public class DAISYSyncWarningEventArgs : SyncWarningEventArgs
    {
        public DAISYSyncWarningEventArgs(string message, string text, string textElementUri, string smilElementUri) : base(message, text)
        {
            TextElementUri = textElementUri;
            SmilElementUri = smilElementUri;
        }

        public string TextElementUri { get; private set; }
        public string SmilElementUri { get; private set; }
    }
}
