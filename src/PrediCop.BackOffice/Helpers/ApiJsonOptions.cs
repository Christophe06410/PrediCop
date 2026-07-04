using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrediCop.BackOffice.Helpers;

internal static class ApiJsonOptions
{
    internal static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
