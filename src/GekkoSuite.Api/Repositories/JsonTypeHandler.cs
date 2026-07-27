using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

using Dapper;

namespace GekkoSuite.Api.Repositories;

/// <summary>Dapper type handler that maps a Postgres json/jsonb column to T by (de)serializing JSON.</summary>
public class JsonTypeHandler<T> : SqlMapper.TypeHandler<T>
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
    {
        // json_build_object keys are camelCase (e.g. permissionId); entity properties are PascalCase
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Deserializes the JSON text from the DB column into T.</summary>
    public override T? Parse(object value)
    {
        return JsonSerializer.Deserialize<T>((string)value, Options);
    }

    /// <summary>Serializes T back to JSON text for a write parameter.</summary>
    public override void SetValue(IDbDataParameter parameter, T? value)
    {
        parameter.Value = JsonSerializer.Serialize(value, Options);
    }
}
