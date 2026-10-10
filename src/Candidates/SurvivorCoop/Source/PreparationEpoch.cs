#nullable enable
using System;
using System.Collections.Generic;

namespace Frontier.Survivor;

/// <summary>
/// One dependency preparation attempt per observed repository version.
/// The caller supplies managed version identities at repository-load boundaries;
/// this object never polls native data or starts background work.
/// </summary>
internal sealed class PreparationEpoch
{
    internal enum State { Waiting, Preparing, Ready, Failed }
    readonly Dictionary<string, string> versions = new(StringComparer.Ordinal);
    long attempted = -1;
    internal long Revision { get; private set; }
    internal State Current { get; private set; } = State.Waiting;
    internal string Reason { get; private set; } = "Загрузка данных…";
    internal long AttemptCount { get; private set; }
    internal bool Ready => Current == State.Ready && attempted == Revision;

    internal bool Observe(string repositoryIdentity, string dataVersion)
    {
        if (string.IsNullOrWhiteSpace(repositoryIdentity)) throw new ArgumentException("Repository identity is required", nameof(repositoryIdentity));
        if (string.IsNullOrWhiteSpace(dataVersion)) throw new ArgumentException("Repository data version is required", nameof(dataVersion));
        if (versions.TryGetValue(repositoryIdentity, out string? previous) && previous == dataVersion) return false;
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
            if (Current == State.Waiting) Reason = "Загрузка данных…";
            return false;
        }
        if (attempted == Revision) return false;
        attempted = Revision;
        AttemptCount++;
        Current = State.Preparing;
        Reason = "Подготовка локации…";
        return true;
    }

    internal bool Complete(long preparedRevision, string? failure)
    {
        // Reject a stale result; a newly observed version needs its own validation.
        if (Current != State.Preparing || preparedRevision != Revision || attempted != preparedRevision) return false;
        Current = failure == null ? State.Ready : State.Failed;
        Reason = failure ?? "";
        return true;
    }

    internal bool RequestExplicitRetry()
    {
        if (Current != State.Failed) return false;
        attempted = -1;
        Current = State.Waiting;
        Reason = "Повторная проверка локации…";
        return true;
    }
}
