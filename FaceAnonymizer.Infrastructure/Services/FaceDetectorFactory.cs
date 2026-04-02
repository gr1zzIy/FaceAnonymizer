using FaceAnonymizer.Core.Abstractions;
using FaceAnonymizer.Infrastructure.Detectors;
using Microsoft.Extensions.DependencyInjection;

namespace FaceAnonymizer.Infrastructure.Services;

public sealed class FaceDetectorFactory : IFaceDetectorFactory
{
    private readonly IServiceProvider _sp;

    public FaceDetectorFactory(IServiceProvider sp)
    {
        _sp = sp;
    }

    public IFaceDetector Create(string engine)
    {
        engine = (engine ?? string.Empty).Trim().ToLowerInvariant();

        return engine switch
        {
            "haar" or "haarcascade" => _sp.GetRequiredService<HaarCascadeFaceDetector>(),
            "yunet" or "onnx" or "onnx-yunet" => _sp.GetRequiredService<YuNetOnnxFaceDetector>(),
            "" => _sp.GetRequiredService<YuNetOnnxFaceDetector>(),
            _ => throw new ArgumentOutOfRangeException(nameof(engine), $"Unsupported engine '{engine}'.")
        };
    }

    public IReadOnlyCollection<string> GetSupportedEngines() => new[] { "haar", "yunet" };
}
