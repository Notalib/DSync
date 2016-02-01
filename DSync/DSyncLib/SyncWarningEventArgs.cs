namespace DSyncLib
{
    public class SyncWarningEventArgs
    {
        public SyncWarningEventArgs(string message, string text)
        {
            Message = message;
            Text = text;
        }

        public string Message { get; private set; }
        public string Text { get; private set; }
    }
}