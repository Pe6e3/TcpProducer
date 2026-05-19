using TcpClientDevice.Configuration;
using TcpClientDevice.Seals;

namespace TcpClientDevice.Protocol.Telemetry;

/// <summary>Создаёт параметры телеметрии (в т.ч. случайные для симулятора).</summary>
public static class TelemetryParameterFactory
{
	public static TelemetryPacketParameters CreateForSeal(
		TelemetryOptions defaults,
		string terminalId,
		SealState sealState,
		DateTime timestamp,
		byte ackSerial,
		Random? random = null)
	{
		var parameters = CreateRandom(defaults, terminalId, timestamp, ackSerial, random);
		ApplySealStatus(parameters, sealState);
		return parameters;
	}

	public static TelemetryPacketParameters CreateRandom(
		TelemetryOptions defaults,
		string terminalId,
		DateTime timestamp,
		byte ackSerial,
		Random? random = null)
	{
		var rng = random ?? Random.Shared;
		var lat = 55.0 + rng.NextDouble() * 2.0;
		var lon = 37.0 + rng.NextDouble() * 2.0;
		var speedKnots = (byte)rng.Next(0, 120);
		var courseDeg = rng.Next(0, 180);

		return new TelemetryPacketParameters
		{
			TerminalId = terminalId,
			ProtocolVersion = defaults.ProtocolVersion,
			DeviceType = defaults.DeviceType,
			DataType = defaults.DataType,
			Timestamp = timestamp,
			Latitude = lat,
			Longitude = lon,
			IsEast = lon >= 0,
			IsNorth = lat >= 0,
			GpsFixed = true,
			SpeedKnots = speedKnots,
			CourseRaw = (byte)(courseDeg / 2),
			MileageKm = (uint)rng.Next(1000, 500000),
			GpsSatellites = (byte)rng.Next(4, 15),
			BindVehicleId = 0,
			DeviceStatus1 = 0x20,
			DeviceStatus2 = 0x20,
			BatteryPercent = (byte)rng.Next(50, 100),
			CellIdLow = (ushort)rng.Next(0, 65535),
			Lac = (ushort)rng.Next(1000, 9999),
			GsmSignal = (byte)rng.Next(10, 31),
			FenceAlarmId = 0,
			ExtendedDeviceStatus = (byte)rng.Next(0, 8),
			MncHigh = (byte)rng.Next(0, 9),
			ExtendedDeviceStatus2 = 0,
			Imei = defaults.Imei,
			CellIdHigh = (ushort)rng.Next(0, 255),
			Mcc = defaults.Mcc,
			MncLow = defaults.MncLow,
			Serial = ackSerial,
		};
	}

	static void ApplySealStatus(TelemetryPacketParameters parameters, SealState sealState)
	{
		if (sealState.Status == SealStatus.Open)
		{
			parameters.DeviceStatus1 = 0x20;
			parameters.DeviceStatus2 = 0x00;
			return;
		}

		parameters.DeviceStatus1 = 0xE0;
		parameters.DeviceStatus2 = 0x20;
	}
}
