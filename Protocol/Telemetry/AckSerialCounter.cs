namespace TcpClientDevice.Protocol.Telemetry;

/// <summary>
/// Инкрементируемый ACK id (serial) для P69, хранится в ОЗУ на время работы процесса.
/// </summary>
public static class AckSerialCounter
{
	static int _current = -1;

	/// <summary>Serial из последнего сформированного пакета.</summary>
	public static byte Current => (byte)_current;

	public static byte Next() => (byte)Interlocked.Increment(ref _current);

	public static void Reset(int start = -1) => Interlocked.Exchange(ref _current, start);
}
