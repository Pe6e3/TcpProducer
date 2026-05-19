namespace TcpClientDevice.Configuration;

public sealed class SealOptions
{
	public string SerialNumber { get; set; } = "";
	public TimeSpan? ConnectInterval { get; set; }
	public TimeSpan? PacketInterval { get; set; }
}
