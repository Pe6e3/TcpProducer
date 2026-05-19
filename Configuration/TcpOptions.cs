namespace TcpClientDevice.Configuration;

public sealed class TcpOptions
{
	public const string SectionName = "Tcp";

	public string Host { get; set; } = "127.0.0.1";
	public int Port { get; set; } = 3255;
	public bool AppendNewline { get; set; }
}
