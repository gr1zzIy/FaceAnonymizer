using FaceAnonymizer.Core.Abstractions;
using FaceAnonymizer.Core.Models;

namespace FaceAnonymizer.Application.Services;

public sealed class FaceDetectionService
{
    private readonly IFaceDetectorFactory _factory;

    public FaceDetectionService(IFaceDetectorFactory factory)
    {
        _factory = factory;
    }

    public (IFaceDetector Detector, DetectResult Result) Detect(ReadOnlySpan<byte> imageBytes, string engine)
    {
        var detector = _factory.Create(engine);
        if (!detector.IsAvailable(out var reason))
            throw new InvalidOperationException(reason);

        var result = detector.Detect(imageBytes);
        return (detector, result);
    }
}
