namespace TcpClientDevice.Configuration;

public sealed class AppOptions
{
	public TcpOptions Tcp { get; set; } = new();
	public DeviceOptions Device { get; set; } = new();
	public List<SealOptions> Seals { get; set; } = [];
	public StorageOptions Storage { get; set; } = new();
	public ProtocolOptions Protocol { get; set; } = new();
	public TelemetryOptions Telemetry { get; set; } = new();
}
