using System.Text;

namespace TcpClientDevice.Protocol.Telemetry;

/// <summary>Расшифровка пакета телеметрии для отладочного лога.</summary>
public static class TelemetryPacketDecoder
{
	public static string FormatDebugLog(byte[] packet)
	{
		if (packet.Length < TelemetryPacketBuilder.PacketSize)
			return $"Некорректная длина пакета: {packet.Length} байт (ожидалось {TelemetryPacketBuilder.PacketSize})";

		if (packet[0] != 0x24)
			return $"Неизвестный заголовок: 0x{packet[0]:X2}";

		var terminalId = BcdEncoding.DecodeDigits(packet.AsSpan(1, 5), 5).TrimStart('0');
		var protocolVersion = packet[6];
		var deviceType = (byte)(packet[7] >> 4);
		var dataType = (TelemetryDataType)(packet[7] & 0x0F);
		var dataLength = (packet[8] << 8) | packet[9];

		var p = packet.AsSpan(10, TelemetryPacketBuilder.PayloadSize);
		var date = BcdEncoding.DecodeDigits(p[..3], 3);
		var time = BcdEncoding.DecodeDigits(p.Slice(3, 3), 3);
		var latitude = BcdEncoding.DecodeCoordinateMm(p.Slice(6, 4));
		var longitude = BcdEncoding.DecodeLongitude(p.Slice(10, 5), out var dirFlags);

		var speedKnots = p[15];
		var courseRaw = p[16];
		var mileage = ReadUInt32BigEndian(p.Slice(17, 4));
		var satellites = p[21];
		var bindVehicle = ReadUInt32BigEndian(p.Slice(22, 4));
		var status1 = p[26];
		var status2 = p[27];
		var battery = p[28];
		var cellLow = ReadUInt16BigEndian(p.Slice(29, 2));
		var lac = ReadUInt16BigEndian(p.Slice(31, 2));
		var gsm = p[33];
		var fence = p[34];
		var extStatus = p[35];
		var mncHigh = p[36];
		var extStatus2 = p[37];
		var imei = DecodeImei(p.Slice(38, 8));
		var cellHigh = ReadUInt16BigEndian(p.Slice(46, 2));
		var mcc = ReadUInt16BigEndian(p.Slice(48, 2));
		var mncLow = p[50];
		var serial = p[51];

		var sb = new StringBuilder();
		sb.AppendLine("─── Телеметрия (расшифровка) ───");
		sb.AppendLine($"  Terminal ID     : {terminalId}");
		sb.AppendLine($"  Protocol ver.   : 0x{protocolVersion:X2} ({protocolVersion})");
		sb.AppendLine($"  Device/Data type: 0x{packet[7]:X2} (device={deviceType}, data={dataType})");
		sb.AppendLine($"  Data length     : {dataLength} байт");
		sb.AppendLine($"  Date/Time       : {FormatDate(date)} {FormatTime(time)} (BCD {date} {time})");
		sb.AppendLine($"  Latitude        : {latitude:F6}° ({BcdEncoding.DecodeDigits(p.Slice(6, 4), 4)} DDMM.MMMM)");
		sb.AppendLine($"  Longitude       : {longitude:F6}° | {TelemetryDirectionFlags.Describe(dirFlags)}");
		sb.AppendLine($"  Speed           : {speedKnots} узл. ({speedKnots * 1.85:F1} km/h)");
		sb.AppendLine($"  Course          : {courseRaw * 2}° (raw={courseRaw})");
		sb.AppendLine($"  Mileage         : {mileage} km");
		sb.AppendLine($"  GPS satellites  : {satellites}");
		sb.AppendLine($"  Bind vehicle ID : 0x{bindVehicle:X8}");
		sb.AppendLine($"  Device status   : 0x{status1:X2} 0x{status2:X2} {DescribeDeviceStatus(status1, status2)}");
		sb.AppendLine($"  Battery         : {battery}% (0x{battery:X2})");
		sb.AppendLine($"  Cell ID low/LAC : {cellLow} / {lac}");
		sb.AppendLine($"  GSM signal      : {gsm}");
		sb.AppendLine($"  Fence alarm ID  : {fence}");
		sb.AppendLine($"  Ext. status     : 0x{extStatus:X2} (wake), 0x{extStatus2:X2}");
		sb.AppendLine($"  MNC             : high=0x{mncHigh:X2}, low=0x{mncLow:X2}");
		sb.AppendLine($"  IMEI            : {imei}");
		sb.AppendLine($"  Cell ID high    : {cellHigh}");
		sb.AppendLine($"  MCC             : {mcc}");
		sb.AppendLine($"  Serial (ACK id) : {serial}");
		sb.Append($"  HEX             : {Convert.ToHexString(packet)}");
		return sb.ToString();
	}

	public static TelemetryPacketParameters Parse(byte[] packet)
	{
		var p = packet.AsSpan(10, TelemetryPacketBuilder.PayloadSize);
		var dirFlags = (byte)(p[14] & 0x0F);
		var dateStr = BcdEncoding.DecodeDigits(p[..3], 3);
		var timeStr = BcdEncoding.DecodeDigits(p.Slice(3, 3), 3);

		return new TelemetryPacketParameters
		{
			TerminalId = BcdEncoding.DecodeDigits(packet.AsSpan(1, 5), 5).TrimStart('0'),
			ProtocolVersion = packet[6],
			DeviceType = (byte)(packet[7] >> 4),
			DataType = (TelemetryDataType)(packet[7] & 0x0F),
			Timestamp = ParseDateTime(dateStr, timeStr),
			Latitude = BcdEncoding.DecodeCoordinateMm(p.Slice(6, 4)),
			Longitude = BcdEncoding.DecodeLongitude(p.Slice(10, 5), out _),
			IsEast = (dirFlags & TelemetryDirectionFlags.East) != 0,
			IsNorth = (dirFlags & TelemetryDirectionFlags.North) != 0,
			GpsFixed = (dirFlags & TelemetryDirectionFlags.GpsFixed) != 0,
			SpeedKnots = p[15],
			CourseRaw = p[16],
			MileageKm = ReadUInt32BigEndian(p.Slice(17, 4)),
			GpsSatellites = p[21],
			BindVehicleId = ReadUInt32BigEndian(p.Slice(22, 4)),
			DeviceStatus1 = p[26],
			DeviceStatus2 = p[27],
			BatteryPercent = p[28],
			CellIdLow = ReadUInt16BigEndian(p.Slice(29, 2)),
			Lac = ReadUInt16BigEndian(p.Slice(31, 2)),
			GsmSignal = p[33],
			FenceAlarmId = p[34],
			ExtendedDeviceStatus = p[35],
			MncHigh = p[36],
			ExtendedDeviceStatus2 = p[37],
			Imei = DecodeImei(p.Slice(38, 8)),
			CellIdHigh = ReadUInt16BigEndian(p.Slice(46, 2)),
			Mcc = ReadUInt16BigEndian(p.Slice(48, 2)),
			MncLow = p[50],
			Serial = p[51],
		};
	}

	static string FormatDate(string bcd) =>
		bcd.Length >= 6 ? $"{bcd[..2]}.{bcd.Substring(2, 2)}.20{bcd[4..]}" : bcd;

	static string FormatTime(string bcd) =>
		bcd.Length >= 6 ? $"{bcd[..2]}:{bcd.Substring(2, 2)}:{bcd[4..]}" : bcd;

	static DateTime ParseDateTime(string date, string time)
	{
		if (date.Length < 6 || time.Length < 6)
			return DateTime.UtcNow;

		var day = int.Parse(date[..2]);
		var month = int.Parse(date.Substring(2, 2));
		var year = 2000 + int.Parse(date.Substring(4, 2));
		var hour = int.Parse(time[..2]);
		var minute = int.Parse(time.Substring(2, 2));
		var second = int.Parse(time.Substring(4, 2));
		return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
	}

	static string DescribeDeviceStatus(byte s1, byte s2)
	{
		var parts = new List<string>();
		if ((s1 & 0x20) != 0) parts.Add("ACK required");
		if ((s1 & 0x40) != 0) parts.Add("lock rope");
		if ((s1 & 0x80) != 0) parts.Add("motor locked");
		if ((s2 & 0x20) != 0) parts.Add("back cover closed");
		return parts.Count > 0 ? $"[{string.Join(", ", parts)}]" : "";
	}

	static string DecodeImei(ReadOnlySpan<byte> data)
	{
		var sb = new StringBuilder(16);
		foreach (var b in data)
		{
			var high = (b >> 4) & 0x0F;
			var low = b & 0x0F;
			sb.Append(high <= 9 ? (char)('0' + high) : 'F');
			sb.Append(low <= 9 ? (char)('0' + low) : 'F');
		}

		return sb.ToString().TrimEnd('F');
	}

	static ushort ReadUInt16BigEndian(ReadOnlySpan<byte> data) => (ushort)((data[0] << 8) | data[1]);

	static uint ReadUInt32BigEndian(ReadOnlySpan<byte> data) =>
		(uint)((data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3]);
}
