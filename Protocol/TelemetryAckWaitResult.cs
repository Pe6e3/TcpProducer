namespace TcpClientDevice.Protocol;

public enum TelemetryAckWaitResult
{
	Valid,
	Timeout,
	InvalidSerial,
}
