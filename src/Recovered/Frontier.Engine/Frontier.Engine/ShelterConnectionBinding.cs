using System;

namespace Frontier.Engine;

public sealed class ShelterConnectionBinding
{
	private object? state;

	public bool IntentObserved { get; private set; }

	public bool HasNativeState
	{
		get
		{
			if (IntentObserved)
			{
				return state != null;
			}
			return false;
		}
	}

	public void Enter()
	{
		IntentObserved = true;
		state = null;
	}

	public void Leave()
	{
		IntentObserved = false;
		state = null;
	}

	public void ObserveNativeCall(object ownerState)
	{
		if (ownerState == null)
		{
			throw new ArgumentNullException("ownerState");
		}
		IntentObserved = true;
		state = ownerState;
	}

	public bool MatchesCurrent(object? incoming, Func<object, object?> readConnection, Func<object?, object?, bool> sameNative)
	{
		object obj = state;
		if (!IntentObserved || obj == null || incoming == null)
		{
			return false;
		}
		object obj2 = readConnection(obj);
		if (obj2 != null)
		{
			if (obj2 != incoming)
			{
				return sameNative(obj2, incoming);
			}
			return true;
		}
		return false;
	}

	public void ClearIfOwnedBy(object ownerState, Func<object?, object?, bool> sameNative)
	{
		if (state != null && (state == ownerState || sameNative(state, ownerState)))
		{
			Leave();
		}
	}
}
