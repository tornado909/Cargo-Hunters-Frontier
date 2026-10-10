using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Frontier.Survivor;

internal static class ReportFile
{
	private static int shared;

	internal static string Mode
	{
		get
		{
			if (Volatile.Read(ref shared) != 0)
			{
				return "shared-overwrite";
			}
			return "atomic-replace";
		}
	}

	internal static void Publish(string temporary, string destination, string text)
	{
		if (Volatile.Read(ref shared) == 0)
		{
			try
			{
				File.Move(temporary, destination, overwrite: true);
				return;
			}
			catch (UnauthorizedAccessException)
			{
				SharedMode();
			}
			catch (IOException ex2) when ((uint)((ex2.HResult & 0xFFFF) - 32) <= 1u)
			{
				SharedMode();
			}
		}
		byte[] bytes = Encoding.UTF8.GetBytes(text);
		using FileStream fileStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
		fileStream.Write(bytes, 0, bytes.Length);
		fileStream.Flush();
	}

	private static void SharedMode()
	{
		if (Interlocked.Exchange(ref shared, 1) == 0)
		{
			Trace.Log.LogWarning((object)"Report snapshot replacement is blocked; publishing through a shared write handle instead.");
		}
	}
}
