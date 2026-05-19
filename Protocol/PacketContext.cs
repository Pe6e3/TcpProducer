using TcpClientDevice.Configuration;

namespace TcpClientDevice.Protocol;

public sealed class PacketContext
{
	public required string SerialNumber { get; init; }
	public required PacketTypeInfo Packet { get; init; }
	public required DeviceOptions Device { get; init; }
	public required ServerCommandOptions CommandConfig { get; init; }
}
