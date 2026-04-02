using Microsoft.AspNetCore.Http;

namespace FaceAnonymizer.Api.Contracts;

public sealed class ImageForm
{
    public IFormFile Image { get; init; } = default!;
}