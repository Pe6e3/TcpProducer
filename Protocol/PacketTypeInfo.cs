namespace TcpClientDevice.Protocol;

public sealed class PacketTypeInfo
{
	public string RawMessage { get; init; } = "";
	public string? PacketType { get; init; }
	public bool IsServerCommand => PacketType is not null;
	public IReadOnlyList<string> Parameters { get; init; } = [];
}
