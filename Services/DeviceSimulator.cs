using TcpClientDevice.Configuration;
using TcpClientDevice.Protocol;

namespace TcpClientDevice.Services;

public sealed class DeviceSimulator
{
	readonly AppOptions _options;
	readonly ProtocolRouter _router;
	readonly IPacketTypeResolver _typeResolver;
	readonly OutboundPayloadFactory _payloadFactory;
	readonly Action<string> _log;

	public DeviceSimulator(
		AppOptions options,
		ProtocolRouter router,
		IPacketTypeResolver typeResolver,
		Action<string> log)
	{
		_options = options;
		_router = router;
		_typeResolver = typeResolver;
		_payloadFactory = new OutboundPayloadFactory(options);
		_log = log;
	}

	public async Task RunAsync(CancellationToken cancellationToken)
	{
		var d = _options.Device;
		_log($"SN={d.SerialNumber} | online {d.ConnectInterval} | data {d.PacketInterval}");

		while (!cancellationToken.IsCancellationRequested)
		{
			await RunSessionAsync(cancellationToken);
			try
			{
				await Task.Delay(d.ConnectInterval, cancellationToken);
			}
			catch (OperationCanceledException)
			{
				break;
			}
		}
	}

	async Task RunSessionAsync(CancellationToken cancellationToken)
	{
		await using var session = new TcpSession(_options.Tcp, _log);
		try
		{
			await session.ConnectAsync(cancellationToken);
			session.StartReceiving(
				msg => OnIncomingAsync(session, msg, cancellationToken),
				cancellationToken);

			var ackOk = await ExecuteConnectSequenceAsync(session, cancellationToken);
			if (ackOk)
				await ExecutePacketScheduleAsync(session, cancellationToken);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_log($"! {ex.Message}");
		}
	}

	async Task<bool> ExecuteConnectSequenceAsync(TcpSession session, CancellationToken cancellationToken)
	{
		var protocol = _options.Protocol;
		var onConnect = protocol.OnConnect;

		if (OutboundPayloadFactory.IsConfigured(onConnect))
		{
			var text = _payloadFactory.GetTextPayload(onConnect);
			var bytes = _payloadFactory.Create(onConnect);
			_log(PacketLogFormatter.Outbound(onConnect, bytes, text));
			await session.SendAsync(bytes, cancellationToken);

			var connectAck = AckMatcher.FromConfig(onConnect.ExpectedAck);
			if (connectAck is not null && await session.WaitForMessageAsync(connectAck, TimeSpan.FromSeconds(30), cancellationToken) is null)
				_log("! ack connect timeout");
		}

		var dataMessage = protocol.AfterConnect.Message;
		if (!OutboundPayloadFactory.IsConfigured(dataMessage))
			return true;

		if (!protocol.AfterConnect.WaitForConnectAck || onConnect.ExpectedAck is null)
			return await SendDataPacketAsync(session, dataMessage, cancellationToken);

		return true;
	}

	async Task ExecutePacketScheduleAsync(TcpSession session, CancellationToken cancellationToken)
	{
		var dataMessage = _options.Protocol.AfterConnect.Message;
		if (!OutboundPayloadFactory.IsConfigured(dataMessage))
			return;

		var sessionEnd = DateTime.UtcNow + _options.Device.SessionDuration;
		var nextPacketAt = DateTime.UtcNow + _options.Device.PacketInterval;

		while (DateTime.UtcNow < sessionEnd && !cancellationToken.IsCancellationRequested)
		{
			var delay = nextPacketAt - DateTime.UtcNow;
			if (delay > TimeSpan.Zero)
				await Task.Delay(delay, cancellationToken);

			if (DateTime.UtcNow >= sessionEnd)
				break;

			if (!await SendDataPacketAsync(session, dataMessage, cancellationToken))
				break;

			nextPacketAt = DateTime.UtcNow + _options.Device.PacketInterval;
		}
	}

	async Task<bool> SendDataPacketAsync(TcpSession session, OutboundMessageOptions message, CancellationToken cancellationToken)
	{
		var bytes = _payloadFactory.Create(message);
		var sentSerial = bytes.Length > 0 ? bytes[^1] : (byte)0;
		_log(PacketLogFormatter.Outbound(message, bytes, _payloadFactory.GetTextPayload(message)));
		await session.SendAsync(bytes, cancellationToken);

		var needsAck = message.ExpectedAck is not null
			&& !string.IsNullOrWhiteSpace(message.ExpectedAck.Pattern);

		if (!needsAck)
			return true;

		var ack = await session.WaitForMessageAsync(
			msg => TelemetryAck.IsValid(msg, sentSerial),
			TimeSpan.FromSeconds(30),
			cancellationToken);

		if (ack is not null)
			return true;

		_log($"! ack timeout (expected P69 s={sentSerial})");
		return false;
	}

	async Task OnIncomingAsync(TcpSession session, string message, CancellationToken cancellationToken)
	{
		var packet = _typeResolver.Resolve(message);

		if (packet.PacketType == TelemetryAck.PacketType)
			_log(PacketLogFormatter.InboundAck(packet.RawMessage));
		else if (packet.IsServerCommand)
			_log(PacketLogFormatter.InboundCommand(packet.RawMessage));
		else if (!string.IsNullOrWhiteSpace(packet.RawMessage))
			_log(PacketLogFormatter.Inbound(packet.RawMessage));

		var result = await _router.RouteIncomingAsync(_options.Device.SerialNumber, message, cancellationToken);
		if (result.ResponsePayload is not { Length: > 0 } response)
			return;

		var text = PacketEncoding.DecodeToDisplay(response, "Ascii");
		_log(PacketLogFormatter.OutboundResponse(text));
		await session.SendAsync(response, cancellationToken);
	}
}
