namespace TcpClientDevice.Protocol;

/// <summary>
/// Пакет регистрации при подключении: (SerialNumber,@JT)
/// </summary>
public static class ConnectPacketBuilder
{
	public static string Build(string serialNumber) => $"({serialNumber},@JT)";
}
