using TcpClientDevice.Configuration;
using TcpClientDevice.Protocol.Telemetry;

namespace TcpClientDevice.Protocol;

public static class PacketLogFormatter
{
	public static string Outbound(
		OutboundMessageOptions message,
		byte[] bytes,
		string? textPayload = null,
		int backlogCount = 0,
		bool isFresh = false)
	{
		if (message.UsesTelemetryBuilder)
		{
			var serial = bytes.Length > 0 ? bytes[^1] : (byte)0;
			var freshTag = isFresh ? " свежий" : "";
			var queueSuffix = backlogCount > 0 ? $" (очередь: {backlogCount})" : "";
			return $"→ telemetry s={serial}{freshTag}{queueSuffix}";
		}

		var ascii = textPayload;
		if (string.IsNullOrEmpty(ascii) && !message.Encoding.Equals("Hex", StringComparison.OrdinalIgnoreCase))
			ascii = PacketEncoding.DecodeToDisplay(bytes, message.Encoding);

		return string.IsNullOrEmpty(ascii) ? $"→ [{bytes.Length}b]" : $"→ {ascii}";
	}

	public static string Inbound(string message) => $"← {message}";

	public static string InboundCommand(string message) => $"← cmd {message}";

	public static string InboundAck(string message) => $"← ack {message}";

	public static string OutboundAscii(string ascii) => $"→ {ascii}";

	public static string OutboundResponse(string ascii) => $"→ rsp {ascii}";
}
