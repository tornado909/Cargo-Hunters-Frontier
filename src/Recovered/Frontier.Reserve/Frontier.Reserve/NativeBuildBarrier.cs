using System;
using System.Threading;

namespace Frontier.Reserve;

public sealed class NativeBuildBarrier
{
	private readonly int thread;

	private readonly Action assertQuiescent;

	public NativeBuildBarrier(Action assertQuiescent)
	{
		this.assertQuiescent = assertQuiescent ?? throw new ArgumentNullException("assertQuiescent");
		thread = Thread.CurrentThread.ManagedThreadId;
		Check();
	}

	public void Check()
	{
		if (Thread.CurrentThread.ManagedThreadId != thread)
		{
			throw new InvalidOperationException("World geometry must be built/disposed on its owning main thread");
		}
		assertQuiescent();
	}
}
