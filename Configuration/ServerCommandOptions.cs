namespace TcpClientDevice.Configuration;

public sealed class ServerCommandOptions
{
	public string PacketType { get; set; } = "";
	public string Handler { get; set; } = "";
	public string ResponseTemplate { get; set; } = "";
	public string ResponseEncoding { get; set; } = "Ascii";
}
