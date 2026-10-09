#nullable enable
using System;
using System.IO;
using System.Text;
using System.Threading;
namespace Frontier.Reserve;
internal static class ReportFile
{
    static int shared;
    internal static string Mode => Volatile.Read(ref shared) == 0 ? "atomic-replace" : "shared-overwrite";
    internal static void Publish(string temporary, string destination, string text)
    {
        if (Volatile.Read(ref shared) == 0)
        {
            try { File.Move(temporary, destination, true); return; }
            catch (UnauthorizedAccessException) { SharedMode(); }
            catch (IOException e) when ((e.HResult & 65535) is 32 or 33) { SharedMode(); }
        }
        // A reader may allow writes but deny replacement. No permission/ACL changes.
        // Readers of a concurrently published snapshot should retry incomplete JSON.
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        using var file = new FileStream(destination, FileMode.Create, FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete);
        file.Write(bytes, 0, bytes.Length);
        file.Flush();
    }
    static void SharedMode()
    {
        if (Interlocked.Exchange(ref shared, 1) == 0)
            Trace.Log.LogWarning("Report snapshot replacement is blocked; publishing through a shared write handle instead.");
    }
}
