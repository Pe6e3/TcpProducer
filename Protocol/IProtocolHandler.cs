namespace TcpClientDevice.Protocol;

public interface IProtocolHandler
{
	string HandlerName { get; }
	Task<HandlerResult> HandleAsync(PacketContext context, CancellationToken cancellationToken);
}
