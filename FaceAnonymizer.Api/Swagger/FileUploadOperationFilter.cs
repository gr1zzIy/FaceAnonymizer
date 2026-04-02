using FaceAnonymizer.Api.Contracts;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace FaceAnonymizer.Api.Swagger;

public sealed class FileUploadOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasImageForm = context.MethodInfo
            .GetParameters()
            .Any(p => p.ParameterType == typeof(ImageForm));

        if (!hasImageForm) return;

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content =
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Required = new HashSet<string> { "image" },
                        Properties =
                        {
                            ["image"] = new OpenApiSchema { Type = "string", Format = "binary" }
                        }
                    }
                }
            }
        };
    }
}