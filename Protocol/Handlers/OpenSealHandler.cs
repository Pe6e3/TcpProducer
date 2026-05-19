using TcpClientDevice.Configuration;
using TcpClientDevice.Seals;
using TcpClientDevice.Storage;

namespace TcpClientDevice.Protocol.Handlers;

/// <summary>
/// P43 и аналоги: помечает пломбу открытой и формирует ответ по шаблону.
/// </summary>
public sealed class OpenSealHandler : IProtocolHandler
{
	readonly ISealStateStore _store;

	public OpenSealHandler(ISealStateStore store) => _store = store;

	public string HandlerName => "HandleP43";

	public async Task<HandlerResult> HandleAsync(PacketContext context, CancellationToken cancellationToken)
	{
		await _store.SetStatusAsync(context.SerialNumber, SealStatus.Open, cancellationToken);

		var cfg = context.CommandConfig;
		if (string.IsNullOrWhiteSpace(cfg.ResponseTemplate))
			return HandlerResult.Acknowledged();

		var responseText = TemplateEngine.Apply(cfg.ResponseTemplate, context.SerialNumber, context.Packet.Parameters);
		var bytes = PacketEncoding.Encode(responseText, cfg.ResponseEncoding);
		return HandlerResult.Respond(bytes);
	}
}
