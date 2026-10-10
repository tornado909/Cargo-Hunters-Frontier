using System;
using System.Text;
using System.Text.Json;

namespace Frontier.Survivor;

internal static class SurvivorCoopProtocol
{
	internal const int Version = 1;

	internal const int MaxBytes = 1000;

	internal const long MaxSequence = 1000000000L;

	internal const long ChoiceWindow = 120L;

	internal static bool Id(string? s)
	{
		if (Guid.TryParse(s, out var result))
		{
			return result != Guid.Empty;
		}
		return false;
	}

	internal static bool Valid(RunHeader? h)
	{
		if (h != null && h.Version == 1 && Id(h.Session) && Id(h.Run) && h.Map == "Frontier_Survivor" && Hex(h.Geometry) && h.Sequence > 0)
		{
			return h.Sequence <= 1000000000;
		}
		return false;
	}

	private static bool Hex(string? s)
	{
		if (s == null || s.Length != 64)
		{
			return false;
		}
		foreach (char c in s)
		{
			if ((c < '0' || c > '9') && (c < 'a' || c > 'f'))
			{
				return false;
			}
		}
		return true;
	}

	internal static bool Same(RunHeader? a, RunHeader? b)
	{
		if (Valid(a) && Valid(b) && a.Session == b.Session && a.Map == b.Map && a.Geometry == b.Geometry)
		{
			return a.Run == b.Run;
		}
		return false;
	}

	internal static T? Decode<T>(string? text) where T : class
	{
		if (string.IsNullOrEmpty(text) || Encoding.UTF8.GetByteCount(text) > 1000)
		{
			return null;
		}
		try
		{
			return JsonSerializer.Deserialize<T>(text, new JsonSerializerOptions
			{
				MaxDepth = 8
			});
		}
		catch (JsonException)
		{
			return null;
		}
	}

	internal static string Encode<T>(T value)
	{
		string text = JsonSerializer.Serialize(value);
		if (Encoding.UTF8.GetByteCount(text) > 1000)
		{
			throw new ArgumentException("Survivor metadata exceeds transport budget");
		}
		return text;
	}

	internal static bool Valid(PersonalSnapshot? s)
	{
		if (s != null && Valid(s.Header) && s.Member != 0L && s.PlayerId >= 0 && Id(s.Equipment) && Id(s.Character) && double.IsFinite(s.Seconds) && s.Seconds >= 0.0 && s.Seconds <= 604800.0 && s.Alive >= 0 && s.Alive <= 28)
		{
			return RunProgress.Valid(s.Progress);
		}
		return false;
	}

	internal static bool Apply(BonusChoice? c, RunHeader current, ulong sender, int nativePlayer, RunProgress progress, ref long acceptedSequence)
	{
		if (c == null || !Same(c.Header, current) || c.Member != sender || sender == 0L || c.PlayerId != nativePlayer || c.Header.Sequence > current.Sequence || c.Header.Sequence < Math.Max(1L, current.Sequence - 120) || c.ChoiceSequence <= acceptedSequence || c.ChoiceSequence > 1000000000 || c.OfferSerial != progress.OfferSerial || progress.PendingChoices < 1 || c.Index < 0 || c.Index >= progress.Offer.Length)
		{
			return false;
		}
		if (!progress.Choose(c.Index))
		{
			return false;
		}
		acceptedSequence = c.ChoiceSequence;
		return true;
	}
}
