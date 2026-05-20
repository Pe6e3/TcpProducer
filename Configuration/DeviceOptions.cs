namespace TcpClientDevice.Configuration;

public sealed class DeviceOptions
{
	public const string SectionName = "Device";

	public string SerialNumbersFile { get; set; } = "deviceserials.txt";
	/// <summary>Максимум устройств из файла (0 = все).</summary>
	public int MaxDevices { get; set; }
	public string SerialNumber { get; set; } = "";
	/// <summary>Период между выходами на связь (время «сна» и накопления очереди).</summary>
	public TimeSpan ConnectInterval { get; set; } = TimeSpan.FromMinutes(10);
	/// <summary>Длительность сессии: сколько отправлять live-телеметрию после слива очереди, затем отключение.</summary>
	public TimeSpan SessionDuration { get; set; } = TimeSpan.FromMinutes(5);
	public TimeSpan PacketInterval { get; set; } = TimeSpan.FromMinutes(5);
}
