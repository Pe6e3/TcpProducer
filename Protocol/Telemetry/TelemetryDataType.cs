namespace TcpClientDevice.Protocol.Telemetry;

/// <summary>Младшая тетрада байта [7] (тип данных).</summary>
public enum TelemetryDataType : byte
{
	RealTimePosition = 1,
	AlarmData = 2,
	BlindAreaPosition = 3,
	SubNewPosition = 4,
}
