using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontier.Engine;

public sealed class CoopRoster
{
	private HashSet<ulong>? frozen;

	private ulong owner;

	private string epoch = "";

	public bool IsFrozen => frozen != null;

	public ulong[] FrozenMembers => frozen?.OrderBy((ulong x) => x).ToArray() ?? Array.Empty<ulong>();

	public void Clear()
	{
		frozen = null;
		owner = 0uL;
		epoch = "";
	}

	public ReadinessResult Check(ulong currentOwner, ulong localMember, string currentEpoch, string expectedToken, IReadOnlyList<PeerReadiness> members)
	{
		if (currentOwner == 0L || localMember == 0L || !CoopProtocol.IsHex(currentEpoch, 32) || !CoopProtocol.IsReadyToken(expectedToken, currentEpoch))
		{
			return new ReadinessResult(Ready: false, "Handshake not initialized", 0, members.Count);
		}
		int count = members.Count;
		bool flag = ((count < 1 || count > 250) ? true : false);
		if (flag || members.Any((PeerReadiness m) => m.Id == 0) || members.Select((PeerReadiness m) => m.Id).Distinct().Count() != members.Count)
		{
			return new ReadinessResult(Ready: false, "Invalid or changing lobby membership", 0, members.Count);
		}
		if (!members.Any((PeerReadiness m) => m.Id == currentOwner) || !members.Any((PeerReadiness m) => m.Id == localMember))
		{
			return new ReadinessResult(Ready: false, "Owner or local member is not present", 0, members.Count);
		}
		if (frozen != null && (owner != currentOwner || epoch != currentEpoch))
		{
			return new ReadinessResult(Ready: false, "Host or lobby epoch changed; recreate the lobby", 0, members.Count);
		}
		if (frozen != null && members.Any((PeerReadiness m) => !frozen.Contains(m.Id)))
		{
			return new ReadinessResult(Ready: false, "New member after deployment lock; return to lobby", 0, members.Count);
		}
		int num = members.Count((PeerReadiness m) => m.Token == expectedToken);
		if (num != members.Count)
		{
			return new ReadinessResult(Ready: false, "Waiting for identical loaded catalogs: " + num + "/" + members.Count, num, members.Count);
		}
		return new ReadinessResult(Ready: true, "READY", num, members.Count);
	}

	public ReadinessResult Freeze(ulong currentOwner, ulong localMember, string currentEpoch, string expectedToken, IReadOnlyList<PeerReadiness> members)
	{
		if (currentOwner != localMember)
		{
			return new ReadinessResult(Ready: false, "Only the lobby owner can deploy", 0, members.Count);
		}
		ReadinessResult readinessResult = Check(currentOwner, localMember, currentEpoch, expectedToken, members);
		if (readinessResult.Ready && frozen == null)
		{
			frozen = members.Select((PeerReadiness m) => m.Id).ToHashSet();
			owner = currentOwner;
			epoch = currentEpoch;
		}
		return readinessResult;
	}
}
