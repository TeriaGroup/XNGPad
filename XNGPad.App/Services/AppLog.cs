namespace XNGPad.App.Services;

public static class AppLog
{
    private static readonly object Gate = new();
    private static readonly Queue<string> Lines = new();

    public static void Write(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;
        var text = DateTime.Now.ToString("HH:mm:ss") + "  " + line.Replace('\r', ' ').Replace('\n', ' ');
        lock (Gate)
        {
            Lines.Enqueue(text);
            while (Lines.Count > 500)
                Lines.Dequeue();
        }
    }

    public static string Text()
    {
        lock (Gate)
            return Lines.Count == 0 ? "" : string.Join(Environment.NewLine, Lines);
    }
}
