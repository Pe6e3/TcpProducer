using System.Text;

namespace TcpClientDevice.Services;

/// <summary>
/// Собирает фрагменты TCP в целые сообщения: в скобках (...), по строкам (\r\n).
/// </summary>
public sealed class IncomingMessageBuffer
{
	readonly StringBuilder _buffer = new();

	public IEnumerable<string> Append(string chunk)
	{
		_buffer.Append(chunk);
		var messages = new List<string>();

		while (TryExtractParenthesized(out var parenthesized))
			messages.Add(parenthesized);

		while (TryExtractLine(out var line))
			messages.Add(line);

		return messages;
	}

	bool TryExtractParenthesized(out string message)
	{
		message = "";
		var text = _buffer.ToString();
		var start = text.IndexOf('(');
		if (start < 0)
			return false;

		if (start > 0)
			_buffer.Remove(0, start);

		text = _buffer.ToString();
		var end = text.IndexOf(')');
		if (end < 0)
			return false;

		message = text[..(end + 1)].Trim();
		_buffer.Remove(0, end + 1);
		return message.Length > 0;
	}

	bool TryExtractLine(out string message)
	{
		message = "";
		var text = _buffer.ToString();
		var lineEnd = text.IndexOfAny(['\r', '\n']);
		if (lineEnd < 0)
			return false;

		var line = text[..lineEnd].Trim();
		var skip = lineEnd + 1;
		if (skip < text.Length && text[lineEnd] == '\r' && text[skip] == '\n')
			skip++;

		_buffer.Remove(0, skip);

		if (line.Length == 0)
			return false;

		if (!line.StartsWith("(P", StringComparison.OrdinalIgnoreCase)
			&& !line.StartsWith("P", StringComparison.OrdinalIgnoreCase))
			return false;

		message = line;
		return true;
	}
}
