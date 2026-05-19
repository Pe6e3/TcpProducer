namespace TcpClientDevice.Protocol.Telemetry;

public static class TelemetryDirectionFlags
{
	public const byte FixedBit = 0x08;
	public const byte East = 0x04;
	public const byte West = 0x00;
	public const byte North = 0x02;
	public const byte South = 0x00;
	public const byte GpsFixed = 0x01;

	public static byte Build(bool isEast, bool isNorth, bool gpsFixed)
	{
		byte flags = FixedBit;
		if (isEast) flags |= East;
		if (isNorth) flags |= North;
		if (gpsFixed) flags |= GpsFixed;
		return flags;
	}

	public static string Describe(byte flags)
	{
		var gps = (flags & GpsFixed) != 0;
		var ns = (flags & North) != 0 ? "N" : "S";
		var ew = (flags & East) != 0 ? "E" : "W";
		return $"GPS={gps}, {ns}, {ew}";
	}
}
