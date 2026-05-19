namespace TcpClientDevice.Protocol.Handlers;

public sealed class LogOnlyHandler : IProtocolHandler
{
	public string HandlerName => "LogOnly";

	public Task<HandlerResult> HandleAsync(PacketContext context, CancellationToken cancellationToken) =>
		Task.FromResult(HandlerResult.Acknowledged($"[LogOnly] {context.Packet.PacketType}: {context.Packet.RawMessage}"));
}
