using System.Text;
using System.Text.Json;

namespace FeedService.Common;

/// <summary>
/// Converts property names to snake_case so responses match the shape the
/// Flutter feed models expect (e.g. ReactionsCount -> reactions_count,
/// CurrentUserReaction -> current_user_reaction). Incoming snake_case bodies
/// bind back to their properties through the same policy.
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
