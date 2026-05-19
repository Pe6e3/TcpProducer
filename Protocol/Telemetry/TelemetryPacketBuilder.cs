using System.Text;
using TcpClientDevice.Configuration;

namespace TcpClientDevice.Protocol.Telemetry;

/// <summary>
/// Сборка пакета телеметрии (0x24, Position and alarm data format HEX).
/// </summary>
public sealed class TelemetryPacketBuilder
{
	public const int PayloadSize = 52;
	public const int PacketSize = 1 + 7 + 2 + PayloadSize;

	readonly TelemetryOptions _defaults;

	public TelemetryPacketBuilder(TelemetryOptions defaults) => _defaults = defaults;

	public byte[] Build(TelemetryPacketParameters parameters)
	{
		var packet = new byte[PacketSize];
		var offset = 0;

		packet[offset++] = 0x24;
		BcdEncoding.EncodeDigits(parameters.TerminalId, 5).CopyTo(packet, offset);
		offset += 5;

		packet[offset++] = parameters.ProtocolVersion;
		packet[offset++] = parameters.DeviceTypeDataTypeByte;

		var payloadOffset = offset + 2;
		WritePayload(packet, payloadOffset, parameters);

		WriteUInt16BigEndian(packet, offset, PayloadSize);
		return packet;
	}

	public byte[] Build(DateTime timestamp, string terminalId, byte ackSerial)
	{
		var parameters = TelemetryParameterFactory.CreateRandom(_defaults, terminalId, timestamp, ackSerial);
		return Build(parameters);
	}

	static void WritePayload(byte[] packet, int offset, TelemetryPacketParameters p)
	{
		BcdEncoding.WriteDate(packet, offset + 0, p.Timestamp);
		BcdEncoding.WriteTime(packet, offset + 3, p.Timestamp);

		BcdEncoding.WriteCoordinateMm(packet, offset + 6, p.Latitude);
		var dir = TelemetryDirectionFlags.Build(p.IsEast, p.IsNorth, p.GpsFixed);
		BcdEncoding.WriteLongitudeWithDirection(packet, offset + 10, p.Longitude, dir);

		packet[offset + 15] = p.SpeedKnots;
		packet[offset + 16] = p.CourseRaw;

		WriteUInt32BigEndian(packet, offset + 17, p.MileageKm);
		packet[offset + 21] = p.GpsSatellites;
		WriteUInt32BigEndian(packet, offset + 22, p.BindVehicleId);

		packet[offset + 26] = p.DeviceStatus1;
		packet[offset + 27] = p.DeviceStatus2;
		packet[offset + 28] = p.BatteryPercent;

		WriteUInt16BigEndian(packet, offset + 29, p.CellIdLow);
		WriteUInt16BigEndian(packet, offset + 31, p.Lac);
		packet[offset + 33] = p.GsmSignal;
		packet[offset + 34] = p.FenceAlarmId;
		packet[offset + 35] = p.ExtendedDeviceStatus;
		packet[offset + 36] = p.MncHigh;
		packet[offset + 37] = p.ExtendedDeviceStatus2;

		BcdEncoding.WriteImei(packet, offset + 38, p.Imei);
		WriteUInt16BigEndian(packet, offset + 46, p.CellIdHigh);
		WriteUInt16BigEndian(packet, offset + 48, p.Mcc);
		packet[offset + 50] = p.MncLow;
		packet[offset + 51] = p.Serial;
	}

	static void WriteUInt16BigEndian(byte[] buffer, int offset, ushort value)
	{
		buffer[offset] = (byte)(value >> 8);
		buffer[offset + 1] = (byte)value;
	}

	static void WriteUInt32BigEndian(byte[] buffer, int offset, uint value)
	{
		buffer[offset] = (byte)(value >> 24);
		buffer[offset + 1] = (byte)(value >> 16);
		buffer[offset + 2] = (byte)(value >> 8);
		buffer[offset + 3] = (byte)value;
	}
}
