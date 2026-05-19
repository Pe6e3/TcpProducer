namespace TcpClientDevice.Protocol;

public static class AckMatcher
{
	public static bool Matches(string received, ExpectedAckPattern? expected)
	{
		if (expected is null || string.IsNullOrWhiteSpace(expected.Pattern))
			return true;

		var pattern = expected.Pattern.Trim();
		if (!pattern.Contains('*', StringComparison.Ordinal))
			return string.Equals(received.Trim(), pattern, StringComparison.Ordinal);

		return MatchWildcard(pattern, received.Trim());
	}

	public static ExpectedAckPattern? FromConfig(Configuration.ExpectedAckOptions? options)
	{
		if (options is null || string.IsNullOrWhiteSpace(options.Pattern))
			return null;

		return new ExpectedAckPattern(options.Pattern, options.Encoding);
	}

	static bool MatchWildcard(string pattern, string value)
	{
		var parts = pattern.Split('*', StringSplitOptions.None);
		if (parts.Length == 1)
			return pattern == value;

		var index = 0;
		for (var i = 0; i < parts.Length; i++)
		{
			var part = parts[i];
			if (part.Length == 0)
				continue;

			var pos = value.IndexOf(part, index, StringComparison.Ordinal);
			if (pos < 0)
				return false;

			if (i == 0 && pos != 0)
				return false;

			index = pos + part.Length;
		}

		var last = parts[^1];
		if (last.Length == 0)
			return true;

		return value.EndsWith(last, StringComparison.Ordinal);
	}
}

public sealed record ExpectedAckPattern(string Pattern, string Encoding);
