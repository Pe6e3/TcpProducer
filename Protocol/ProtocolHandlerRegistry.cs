using TcpClientDevice.Configuration;
using TcpClientDevice.Protocol.Handlers;

namespace TcpClientDevice.Protocol;

public sealed class ProtocolHandlerRegistry
{
	readonly Dictionary<string, IProtocolHandler> _handlersByName = new(StringComparer.OrdinalIgnoreCase);
	readonly Dictionary<string, ServerCommandOptions> _commandsByPacketType = new(StringComparer.OrdinalIgnoreCase);

	public ProtocolHandlerRegistry(ProtocolOptions protocol, DeviceOptions device)
	{
		RegisterBuiltIn(new LogOnlyHandler());

		foreach (var command in protocol.ServerCommands)
		{
			if (string.IsNullOrWhiteSpace(command.PacketType))
				continue;

			_commandsByPacketType[command.PacketType] = command;

			if (string.IsNullOrWhiteSpace(command.Handler))
				continue;

			if (!_handlersByName.ContainsKey(command.Handler))
				RegisterBuiltIn(new TemplateResponseHandler(command.Handler));
		}

		_ = device;
	}

	public ServerCommandOptions? GetCommandConfig(string packetType) =>
		_commandsByPacketType.TryGetValue(packetType, out var cfg) ? cfg : null;

	public IProtocolHandler? GetHandler(string handlerName) =>
		_handlersByName.TryGetValue(handlerName, out var handler) ? handler : null;

	public void Register(IProtocolHandler handler) => _handlersByName[handler.HandlerName] = handler;

	void RegisterBuiltIn(IProtocolHandler handler) => Register(handler);
}
