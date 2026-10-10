using System;
using System.Linq;
using System.Text.Json;

namespace Frontier.ReferenceFix;

internal static class PartyPolicy
{
	internal static string Canonical(string s)
	{
		if (!Guid.TryParse(s, out var result))
		{
			return s.Trim().ToLowerInvariant();
		}
		return result.ToString("N");
	}

	internal static string Label(string? s)
	{
		return new string((s ?? "").Where((char c) => !char.IsControl(c) && c != '<' && c != '>').Take(32).ToArray()).Trim();
	}

	internal static bool Same(string? a, string? b)
	{
		if (!string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b))
		{
			return Canonical(a) == Canonical(b);
		}
		return false;
	}

	internal static bool Valid(PartyIdentity? p, string session, string map)
	{
		if (p != null && p.Version == 1 && p.PlayerId >= 0 && p.PlayerId < 1000000 && Same(p.Session, session) && p.Map == map)
		{
			if (!Guid.TryParse(p.Equipment, out var result) || !(result != Guid.Empty))
			{
				if (Guid.TryParse(p.Character, out var result2))
				{
					return result2 != Guid.Empty;
				}
				return false;
			}
			return true;
		}
		return false;
	}

	internal static PartyIdentity? Decode(string s, string session, string map)
	{
		if (s.Length == 0 || s.Length > 900)
		{
			return null;
		}
		try
		{
			PartyIdentity partyIdentity = JsonSerializer.Deserialize<PartyIdentity>(s);
			return Valid(partyIdentity, session, map) ? partyIdentity : null;
		}
		catch (JsonException)
		{
			return null;
		}
	}

	internal static bool FreshSnapshot(long wantedWorld, long actualWorld, string wantedSession, string actualSession, double age)
	{
		if (wantedWorld != 0L && wantedWorld == actualWorld && Same(wantedSession, actualSession) && age >= 0.0)
		{
			return age < 0.5;
		}
		return false;
	}

	internal static bool Matches(PartyIdentity p, int id, string equipment, string character)
	{
		if (p.PlayerId == id)
		{
			if (!Same(p.Equipment, equipment))
			{
				return Same(p.Character, character);
			}
			return true;
		}
		return false;
	}
}
