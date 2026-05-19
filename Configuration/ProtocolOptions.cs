namespace TcpClientDevice.Configuration;

public sealed class ProtocolOptions
{
	public const string SectionName = "Protocol";

	public string Mode { get; set; } = "Simulator";
	public OutboundMessageOptions OnConnect { get; set; } = new();
	public AfterConnectOptions AfterConnect { get; set; } = new();
	public List<ServerCommandOptions> ServerCommands { get; set; } = [];
}

public sealed class AfterConnectOptions
{
	public bool WaitForConnectAck { get; set; }
	public OutboundMessageOptions Message { get; set; } = new();
}
