using Microsoft.OpenApi;

namespace FindThatBook.Api.Common.OpenApi;

public static class OpenApiSetup
{
    /// <summary>
    /// Publishes the OpenAPI document at /openapi/v1.json. The Angular client is generated from it.
    /// </summary>
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
            options.AddDocumentTransformer((document, _, _) =>
            {
                foreach (var schema in document.Components?.Schemas?.Values ?? [])
                {
                    NormalizeNumberTypes(schema);
                }

                return Task.CompletedTask;
            }));

        return services;
    }

    // .NET describes an int as ["integer", "string"] because System.Text.Json can
    // read one from either. Generated clients should see a plain number instead of
    // a number-or-string union.
    private static void NormalizeNumberTypes(IOpenApiSchema schema)
    {
        if (schema is OpenApiSchema concrete
            && concrete.Type is { } type
            && type.HasFlag(JsonSchemaType.String)
            && (type.HasFlag(JsonSchemaType.Integer) || type.HasFlag(JsonSchemaType.Number)))
        {
            concrete.Type = type & ~JsonSchemaType.String;
            concrete.Pattern = null;
        }

        foreach (var property in schema.Properties?.Values ?? [])
        {
            NormalizeNumberTypes(property);
        }

        if (schema.Items is { } items)
        {
            NormalizeNumberTypes(items);
        }
    }
}
