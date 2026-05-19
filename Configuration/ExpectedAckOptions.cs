namespace TcpClientDevice.Configuration;

public sealed class ExpectedAckOptions
{
	/// <summary>Шаблон ответа, например (P69,0,*) или точная строка.</summary>
	public string? Pattern { get; set; }

	/// <summary>Ascii, Utf8 или Hex.</summary>
	public string Encoding { get; set; } = "Ascii";
}
