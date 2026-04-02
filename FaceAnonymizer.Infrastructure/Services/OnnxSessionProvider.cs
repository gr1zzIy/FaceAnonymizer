using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using FaceAnonymizer.Infrastructure.Options;

namespace FaceAnonymizer.Infrastructure.Services;

public interface IOnnxSessionProvider : IDisposable
{
    InferenceSession GetYuNetSession();
}

public sealed class OnnxSessionProvider : IOnnxSessionProvider
{
    private readonly string _modelPath;
    private readonly Lazy<InferenceSession> _lazySession;

    public OnnxSessionProvider(IOptions<FaceDetectionOptions> options)
    {
        var o = options.Value;
        _modelPath = Path.Combine(AppContext.BaseDirectory, o.AssetsPath, o.YuNetOnnxFile);

        _lazySession = new Lazy<InferenceSession>(() =>
        {
            if (!File.Exists(_modelPath))
                throw new FileNotFoundException("YuNet ONNX model not found.", _modelPath);

            var so = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
            };

            return new InferenceSession(_modelPath, so);
        }, isThreadSafe: true);
    }

    public InferenceSession GetYuNetSession() => _lazySession.Value;

    public void Dispose()
    {
        if (_lazySession.IsValueCreated)
            _lazySession.Value.Dispose();
    }
}
