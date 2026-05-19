using TcpClientDevice.Configuration;

namespace TcpClientDevice.Protocol.Handlers;

/// <summary>
/// Универсальный обработчик: подставляет параметры входящего пакета в шаблон ответа из конфигурации.
/// </summary>
public sealed class TemplateResponseHandler : IProtocolHandler
{
	readonly string _handlerName;

	public TemplateResponseHandler(string handlerName) => _handlerName = handlerName;

	public string HandlerName => _handlerName;

	public Task<HandlerResult> HandleAsync(PacketContext context, CancellationToken cancellationToken)
	{
		var cfg = context.CommandConfig;
		if (string.IsNullOrWhiteSpace(cfg.ResponseTemplate))
			return Task.FromResult(HandlerResult.Acknowledged());

		var responseText = TemplateEngine.Apply(cfg.ResponseTemplate, context.SerialNumber, context.Packet.Parameters);
		var bytes = PacketEncoding.Encode(responseText, cfg.ResponseEncoding);
		return Task.FromResult(HandlerResult.Respond(bytes));
	}
}
