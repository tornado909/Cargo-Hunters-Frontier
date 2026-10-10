using System;
using System.Collections.Generic;

namespace Frontier.Reserve;

internal sealed class PreparationEpoch
{
	internal enum State
	{
		Waiting,
		Preparing,
		Ready,
		Failed
	}

	private readonly Dictionary<string, string> versions = new Dictionary<string, string>(StringComparer.Ordinal);

	private long attempted = -1L;

	internal long Revision { get; private set; }

	internal State Current { get; private set; }

	internal string Reason { get; private set; } = "Загрузка данных…";

	internal long AttemptCount { get; private set; }

	internal bool Ready
	{
		get
		{
			if (Current == State.Ready)
			{
				return attempted == Revision;
			}
			return false;
		}
	}

	internal bool Observe(string repositoryIdentity, string dataVersion)
	{
		if (string.IsNullOrWhiteSpace(repositoryIdentity))
		{
			throw new ArgumentException("Repository identity is required", "repositoryIdentity");
		}
		if (string.IsNullOrWhiteSpace(dataVersion))
		{
			throw new ArgumentException("Repository data version is required", "dataVersion");
		}
		if (versions.TryGetValue(repositoryIdentity, out string value) && value == dataVersion)
		{
			return false;
		}
		versions[repositoryIdentity] = dataVersion;
		Revision++;
		Current = State.Waiting;
		Reason = "Обновление данных локации…";
		return true;
	}

	internal bool TryBegin(bool dependenciesAvailable, out long revision)
	{
		revision = Revision;
		if (!dependenciesAvailable)
		{
			if (Current == State.Waiting)
			{
				Reason = "Загрузка данных…";
			}
			return false;
		}
		if (attempted == Revision)
		{
			return false;
		}
		attempted = Revision;
		AttemptCount++;
		Current = State.Preparing;
		Reason = "Подготовка локации…";
		return true;
	}

	internal bool Complete(long preparedRevision, string? failure)
	{
		if (Current != State.Preparing || preparedRevision != Revision || attempted != preparedRevision)
		{
			return false;
		}
		Current = ((failure == null) ? State.Ready : State.Failed);
		Reason = failure ?? "";
		return true;
	}

	internal bool RequestExplicitRetry()
	{
		if (Current != State.Failed)
		{
			return false;
		}
		attempted = -1L;
		Current = State.Waiting;
		Reason = "Повторная проверка локации…";
		return true;
	}
}
