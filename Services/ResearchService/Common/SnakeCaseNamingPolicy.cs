using System.Text;
using System.Text.Json;

namespace ResearchService.Common;

/// <summary>
/// Converts property names to snake_case so responses match the shape the
/// Flutter research module expects (e.g. FileUrl -> file_url, PaymentStatus ->
/// payment_status). The same policy binds snake_case request bodies too.
/// </summary>
public sealed class SnakeCaseNamingPolicy : JsonNamingPolicy
{
    public static readonly SnakeCaseNamingPolicy Instance = new();

    public override string ConvertName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var builder = new StringBuilder(name.Length + 8);

        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];

            if (char.IsUpper(current))
            {
                if (i > 0 && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1])))
                    builder.Append('_');

                builder.Append(char.ToLowerInvariant(current));
            }
            else
            {
                builder.Append(current);
            }
        }

        return builder.ToString();
    }
}
