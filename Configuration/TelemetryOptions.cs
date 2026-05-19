using TcpClientDevice.Protocol.Telemetry;

namespace TcpClientDevice.Configuration;

public sealed class TelemetryOptions
{
	public const string SectionName = "Telemetry";

	public byte ProtocolVersion { get; set; } = 0x19;
	public byte DeviceType { get; set; } = 0x01;
	public TelemetryDataType DataType { get; set; } = TelemetryDataType.RealTimePosition;
	public string Imei { get; set; } = "860000000000000";
	public ushort Mcc { get; set; } = 250;
	public byte MncLow { get; set; } = 1;

	[Obsolete("Используйте DeviceType и DataType")]
	public byte PacketType
	{
		get => (byte)((DeviceType << 4) | ((byte)DataType & 0x0F));
		set
		{
			DeviceType = (byte)(value >> 4);
			DataType = (TelemetryDataType)(value & 0x0F);
		}
	}
}
