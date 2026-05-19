using System.Text.RegularExpressions;

namespace TcpClientDevice.Protocol;

/// <summary>
/// Определяет тип входящего пакета. Команды сервера начинаются с "(P", далее идёт тип (P43, P69 и т.д.).
/// </summary>
public sealed partial class PacketTypeResolver : IPacketTypeResolver
{
	[GeneratedRegex(@"^\(?P(\w+)", RegexOptions.CultureInvariant)]
	private static partial Regex ServerCommandRegex();

	public PacketTypeInfo Resolve(string message)
	{
		var trimmed = Normalize(message);
		var match = ServerCommandRegex().Match(trimmed);
		if (!match.Success)
			return new PacketTypeInfo { RawMessage = trimmed, PacketType = null, Parameters = [] };

		var packetType = "P" + match.Groups[1].Value.TrimStart('P');
		var parameters = ParseParameters(trimmed, packetType);
		return new PacketTypeInfo
		{
			RawMessage = trimmed,
			PacketType = packetType,
			Parameters = parameters,
		};
	}

	static IReadOnlyList<string> ParseParameters(string message, string packetType)
	{
		var inner = message.Trim();
		if (inner.StartsWith('('))
			inner = inner[1..];
		if (inner.EndsWith(')'))
			inner = inner[..^1];

		var parts = inner.Split(',', StringSplitOptions.TrimEntries);
		if (parts.Length == 0)
			return [];

		if (parts[0].Equals(packetType, StringComparison.OrdinalIgnoreCase))
			return parts.Skip(1).ToArray();

		if (parts[0].StartsWith('P') && parts[0].Equals(packetType, StringComparison.OrdinalIgnoreCase))
			return parts.Skip(1).ToArray();

		return parts.Skip(1).ToArray();
	}

	static string Normalize(string message)
	{
		var trimmed = message.Trim().TrimEnd('\r', '\n');
		if (trimmed.Length == 0)
			return trimmed;

		if (trimmed.StartsWith('('))
			return trimmed;

		if (trimmed.StartsWith("P", StringComparison.OrdinalIgnoreCase))
		{
			trimmed = trimmed.TrimEnd(')');
			return $"({trimmed})";
		}

		return trimmed;
	}
}
