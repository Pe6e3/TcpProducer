using TcpClientDevice.Configuration;

namespace TcpClientDevice.Protocol;

public sealed class ProtocolRouter
{
	readonly IPacketTypeResolver _typeResolver;
	readonly ProtocolHandlerRegistry _registry;
	readonly DeviceOptions _device;

	public ProtocolRouter(
		IPacketTypeResolver typeResolver,
		ProtocolHandlerRegistry registry,
		DeviceOptions device)
	{
		_typeResolver = typeResolver;
		_registry = registry;
		_device = device;
	}

	public async Task<HandlerResult> RouteIncomingAsync(
		string serialNumber,
		string message,
		CancellationToken cancellationToken)
	{
		var packet = _typeResolver.Resolve(message);

		if (!packet.IsServerCommand)
			return HandlerResult.NotHandled();

		var commandConfig = _registry.GetCommandConfig(packet.PacketType!);
		if (commandConfig is null)
			return HandlerResult.NotHandled();

		var handler = _registry.GetHandler(commandConfig.Handler);
		if (handler is null)
			return HandlerResult.NotHandled();

		var context = new PacketContext
		{
			SerialNumber = serialNumber,
			Packet = packet,
			Device = _device,
			CommandConfig = commandConfig,
		};

		return await handler.HandleAsync(context, cancellationToken);
	}
}
