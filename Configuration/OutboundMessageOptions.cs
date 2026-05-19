namespace TcpClientDevice.Configuration;

public sealed class OutboundMessageOptions
{
	/// <summary>Static — Payload из конфига; Connect — (SerialNumber,@JT); Telemetry — бинарная телеметрия.</summary>
	public string PayloadSource { get; set; } = "Static";
	public string Payload { get; set; } = "";
	public string Encoding { get; set; } = "Ascii";
	public ExpectedAckOptions? ExpectedAck { get; set; }

	public bool UsesTelemetryBuilder =>
		PayloadSource.Equals("Telemetry", StringComparison.OrdinalIgnoreCase);

	public bool UsesConnectBuilder =>
		PayloadSource.Equals("Connect", StringComparison.OrdinalIgnoreCase);
}
