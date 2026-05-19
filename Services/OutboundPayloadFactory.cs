using TcpClientDevice.Configuration;
using TcpClientDevice.Protocol;
using TcpClientDevice.Protocol.Telemetry;

namespace TcpClientDevice.Services;

public sealed class OutboundPayloadFactory
{
	readonly TelemetryPacketBuilder _telemetryBuilder;
	readonly TelemetryOptions _telemetry;
	readonly DeviceOptions _device;

	public OutboundPayloadFactory(AppOptions options)
	{
		_device = options.Device;
		_telemetry = options.Telemetry;
		_telemetryBuilder = new TelemetryPacketBuilder(options.Telemetry);
	}

	public string? GetTextPayload(OutboundMessageOptions message)
	{
		if (message.UsesConnectBuilder)
			return ConnectPacketBuilder.Build(_device.SerialNumber);
		if (message.UsesTelemetryBuilder)
			return null;
		return string.IsNullOrWhiteSpace(message.Payload) ? null : message.Payload;
	}

	public byte[] Create(OutboundMessageOptions message, DateTime? timestamp = null)
	{
		if (message.UsesConnectBuilder)
			return PacketEncoding.Encode(ConnectPacketBuilder.Build(_device.SerialNumber), message.Encoding);

		if (message.UsesTelemetryBuilder)
		{
			var ackSerial = AckSerialCounter.Next();
			var parameters = TelemetryParameterFactory.CreateRandom(
				_telemetry,
				_device.SerialNumber,
				timestamp ?? DateTime.UtcNow,
				ackSerial);
			return _telemetryBuilder.Build(parameters);
		}

		return PacketEncoding.Encode(message.Payload, message.Encoding);
	}

	public byte[] Create(TelemetryPacketParameters parameters) => _telemetryBuilder.Build(parameters);

	public static string DescribeTelemetry(byte[] bytes) =>
		bytes.Length >= TelemetryPacketBuilder.PacketSize
			? $"s={bytes[^1]}"
			: $"{bytes.Length}b";

	public static bool IsConfigured(OutboundMessageOptions message) =>
		message.UsesTelemetryBuilder
		|| message.UsesConnectBuilder
		|| !string.IsNullOrWhiteSpace(message.Payload);
}
