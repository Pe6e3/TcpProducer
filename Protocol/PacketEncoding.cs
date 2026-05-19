using System.Text;

namespace TcpClientDevice.Protocol;

public static class PacketEncoding
{
	public static byte[] Encode(string payload, string encodingName)
	{
		var encoding = encodingName.Trim();
		if (encoding.Equals("Hex", StringComparison.OrdinalIgnoreCase))
			return ParseHex(payload);

		var textEncoding = encoding.Equals("Utf8", StringComparison.OrdinalIgnoreCase)
			? Encoding.UTF8
			: Encoding.ASCII;

		return textEncoding.GetBytes(payload);
	}

	public static string Decode(byte[] data, string encodingName)
	{
		var encoding = encodingName.Trim();
		if (encoding.Equals("Hex", StringComparison.OrdinalIgnoreCase))
			return BitConverter.ToString(data).Replace("-", "", StringComparison.Ordinal);

		var textEncoding = encoding.Equals("Utf8", StringComparison.OrdinalIgnoreCase)
			? Encoding.UTF8
			: Encoding.ASCII;

		return textEncoding.GetString(data);
	}

	public static string DecodeToDisplay(byte[] data, string encodingName) =>
		Decode(data, encodingName);

	static byte[] ParseHex(string hex)
	{
		var cleaned = hex.Replace(" ", "", StringComparison.Ordinal)
			.Replace("-", "", StringComparison.Ordinal);
		if (cleaned.Length % 2 != 0)
			throw new FormatException($"Нечётная длина HEX: {cleaned.Length}");

		var bytes = new byte[cleaned.Length / 2];
		for (var i = 0; i < bytes.Length; i++)
			bytes[i] = Convert.ToByte(cleaned.Substring(i * 2, 2), 16);
		return bytes;
	}
}
