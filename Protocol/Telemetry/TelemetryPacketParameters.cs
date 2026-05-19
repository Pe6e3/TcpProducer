namespace TcpClientDevice.Protocol.Telemetry;

/// <summary>Параметры для сборки пакета телеметрии (Position and alarm data, HEX).</summary>
public sealed class TelemetryPacketParameters
{
	public string TerminalId { get; init; } = "";
	public byte ProtocolVersion { get; init; } = 0x19;
	public byte DeviceType { get; init; } = 0x01;
	public TelemetryDataType DataType { get; init; } = TelemetryDataType.RealTimePosition;

	public DateTime Timestamp { get; init; } = DateTime.UtcNow;

	public double Latitude { get; init; }
	public double Longitude { get; init; }
	public bool IsEast { get; init; } = true;
	public bool IsNorth { get; init; } = true;
	public bool GpsFixed { get; init; } = true;

	/// <summary>Скорость в узлах (km/h ≈ value * 1.85).</summary>
	public byte SpeedKnots { get; init; }

	/// <summary>Курс: градусы = значение * 2.</summary>
	public byte CourseRaw { get; init; }

	public uint MileageKm { get; init; }
	public byte GpsSatellites { get; init; }
	public uint BindVehicleId { get; init; }

	public byte DeviceStatus1 { get; set; }
	public byte DeviceStatus2 { get; set; }
	public byte BatteryPercent { get; init; } = 100;

	public ushort CellIdLow { get; init; }
	public ushort Lac { get; init; }
	public byte GsmSignal { get; init; }
	public byte FenceAlarmId { get; init; }
	public byte ExtendedDeviceStatus { get; init; }
	public byte MncHigh { get; init; }
	public byte ExtendedDeviceStatus2 { get; init; }
	public string Imei { get; init; } = "";
	public ushort CellIdHigh { get; init; }
	public ushort Mcc { get; init; }
	public byte MncLow { get; init; }
	public byte Serial { get; init; }

	public byte DeviceTypeDataTypeByte => (byte)((DeviceType << 4) | ((byte)DataType & 0x0F));

	public double CourseDegrees => CourseRaw * 2.0;
	public double SpeedKmH => SpeedKnots * 1.85;
}
