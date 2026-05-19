namespace TcpClientDevice.Protocol.Telemetry;

public static class BcdEncoding
{
	public static byte[] EncodeDigits(string digits, int byteCount)
	{
		var onlyDigits = new string(digits.Where(char.IsDigit).ToArray());
		onlyDigits = onlyDigits.PadLeft(byteCount * 2, '0');
		if (onlyDigits.Length > byteCount * 2)
			onlyDigits = onlyDigits[^ (byteCount * 2)..];

		var result = new byte[byteCount];
		for (var i = 0; i < byteCount; i++)
		{
			var high = onlyDigits[i * 2] - '0';
			var low = onlyDigits[i * 2 + 1] - '0';
			result[i] = (byte)((high << 4) | low);
		}

		return result;
	}

	public static void WriteDigits(byte[] buffer, int offset, string digits, int byteCount) =>
		EncodeDigits(digits, byteCount).CopyTo(buffer, offset);

	public static void WriteDate(byte[] buffer, int offset, DateTime time) =>
		WriteDigits(buffer, offset, $"{time.Day:00}{time.Month:00}{time.Year % 100:00}", 3);

	public static void WriteTime(byte[] buffer, int offset, DateTime time) =>
		WriteDigits(buffer, offset, $"{time.Hour:00}{time.Minute:00}{time.Second:00}", 3);

	/// <summary>Широта/долгота в формате DDMM.MMMM (4 байта BCD).</summary>
	public static void WriteCoordinateMm(byte[] buffer, int offset, double decimalDegrees)
	{
		decimalDegrees = Math.Abs(decimalDegrees);
		var degrees = (int)decimalDegrees;
		var minutes = (decimalDegrees - degrees) * 60.0;
		var digits = $"{degrees:00}{(int)Math.Round(minutes * 10000):000000}";
		if (digits.Length > 8)
			digits = digits[^8..];
		digits = digits.PadLeft(8, '0');
		WriteDigits(buffer, offset, digits, 4);
	}

	/// <summary>Долгота DDDMM.MMMM — 4.5 байта BCD + 0.5 байта direction в байте [offset+4].</summary>
	public static void WriteLongitudeWithDirection(
		byte[] buffer, int offset, double decimalDegrees, byte directionNibble)
	{
		decimalDegrees = Math.Abs(decimalDegrees);
		var degrees = (int)decimalDegrees;
		var minutes = (decimalDegrees - degrees) * 60.0;
		var digits = $"{degrees:000}{(int)Math.Round(minutes * 10000):000000}";
		if (digits.Length > 9)
			digits = digits[^9..];
		digits = digits.PadLeft(9, '0');

		WriteDigits(buffer, offset, digits[..8], 4);
		var lastDigit = (byte)(digits[8] - '0');
		buffer[offset + 4] = (byte)((lastDigit << 4) | (directionNibble & 0x0F));
	}

	public static void WriteImei(byte[] buffer, int offset, string imei)
	{
		const int imeiNibbles = 16;
		var digits = new string(imei.Where(char.IsDigit).ToArray());
		digits = digits.PadRight(imeiNibbles, 'F');
		if (digits.Length > imeiNibbles)
			digits = digits[..imeiNibbles];

		for (var i = 0; i < 8; i++)
		{
			var c1 = digits[i * 2];
			var c2 = digits[i * 2 + 1];
			var high = NibbleValue(c1);
			var low = NibbleValue(c2);
			buffer[offset + i] = (byte)((high << 4) | low);
		}
	}

	public static string DecodeDigits(ReadOnlySpan<byte> data, int byteCount)
	{
		var chars = new char[byteCount * 2];
		for (var i = 0; i < byteCount; i++)
		{
			chars[i * 2] = (char)('0' + ((data[i] >> 4) & 0x0F));
			chars[i * 2 + 1] = (char)('0' + (data[i] & 0x0F));
		}

		return new string(chars);
	}

	public static double DecodeCoordinateMm(ReadOnlySpan<byte> data)
	{
		var digits = DecodeDigits(data, 4);
		var dd = int.Parse(digits[..2]);
		var mmmm = int.Parse(digits[2..]);
		return dd + mmmm / 600000.0;
	}

	public static double DecodeLongitude(ReadOnlySpan<byte> data, out byte directionNibble)
	{
		var digits = DecodeDigits(data, 4) + ((data[4] >> 4) & 0x0F).ToString();
		directionNibble = (byte)(data[4] & 0x0F);
		var ddd = int.Parse(digits[..3]);
		var mmmm = int.Parse(digits[3..]);
		return ddd + mmmm / 600000.0;
	}

	static int NibbleValue(char c) =>
		c is >= '0' and <= '9' ? c - '0' : 0x0F;
}
