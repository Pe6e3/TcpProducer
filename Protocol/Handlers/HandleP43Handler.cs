using TcpClientDevice.Protocol;

namespace TcpClientDevice.Protocol.Handlers;

/// <summary>
/// Пример собственного обработчика. Можно зарегистрировать в Program.cs вместо шаблона из конфигурации.
/// Вход: (P43,383838,13#5314992) → Выход: (8252599445,P43,1,0,13#5314992)
/// </summary>
public sealed class HandleP43Handler : IProtocolHandler
{
	public string HandlerName => "HandleP43";

	public Task<HandlerResult> HandleAsync(PacketContext context, CancellationToken cancellationToken)
	{
		var last = context.Packet.Parameters.Count > 0 ? context.Packet.Parameters[^1] : "";
		var response = $"({context.SerialNumber},P43,1,0,{last})";
		var bytes = PacketEncoding.Encode(response, "Ascii");
		return Task.FromResult(HandlerResult.Respond(bytes, $"[HandleP43] {response}"));
	}
}
