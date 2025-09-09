using System.Globalization;
using System.Text;

namespace Selfix.Jobs.Shared.Utils;

public sealed class ArgumentsBuilder
{
    private readonly List<(string Text, bool IsSwitch)> _arguments = [];

    public ArgumentsBuilder AddParameter(string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            _arguments.Add(($"{name} {value}", false));
        }

        return this;
    }

    public ArgumentsBuilder AddSwitch(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            _arguments.Add((name, true));
        }

        return this;
    }

    public override string ToString()
    {
        var stringBuilder = new StringBuilder();

        foreach (var (text, _) in _arguments)
        {
            stringBuilder.Append(CultureInfo.InvariantCulture, $"{text} ");
        }

        if (stringBuilder.Length > 0)
        {
            stringBuilder.Remove(stringBuilder.Length - 1, 1);
        }
        
        return stringBuilder.ToString();
    }
}