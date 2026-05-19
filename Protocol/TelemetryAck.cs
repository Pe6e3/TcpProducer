namespace TcpClientDevice.Protocol;

public static class TelemetryAck
{
	public const string PacketType = "P69";

	public static bool TryParseSerial(string message, out byte serial)
	{
		serial = 0;
		var trimmed = message.Trim();
		if (!trimmed.StartsWith("(P69,", StringComparison.OrdinalIgnoreCase))
			return false;

		var inner = trimmed.TrimStart('(').TrimEnd(')');
		var parts = inner.Split(',', StringSplitOptions.TrimEntries);
		if (parts.Length < 3)
			return false;

		return byte.TryParse(parts[^1], out serial);
	}

	public static bool IsValid(string message, byte expectedSerial) =>
		TryParseSerial(message, out var serial) && serial == expectedSerial;
}
