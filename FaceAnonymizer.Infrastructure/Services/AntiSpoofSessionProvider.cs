using FaceAnonymizer.Core.Abstractions;
using FaceAnonymizer.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;

namespace FaceAnonymizer.Infrastructure.Services;

public sealed class AntiSpoofSessionProvider : IAntiSpoofSessionProvider, IDisposable
{
    private readonly Lazy<InferenceSession> _session;

    public AntiSpoofSessionProvider(IOptions<AntiSpoofOptions> opt, ILogger<AntiSpoofSessionProvider> log)
    {
        var o = opt.Value;

        _session = new Lazy<InferenceSession>(() =>
        {
            var full = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, o.ModelPath));
            if (!File.Exists(full))
                throw new FileNotFoundException($"Anti-spoof model not found: {full}");

            var so = new SessionOptions { EnableCpuMemArena = true };

            var session = new InferenceSession(full, so);

            log.LogInformation("[AntiSpoof] Model: {Path}", full);

            foreach (var kv in session.InputMetadata)
                log.LogInformation("[AntiSpoof] Input {Name} => {Shape} {Type}",
                    kv.Key, string.Join("x", kv.Value.Dimensions), kv.Value.ElementType);

            foreach (var kv in session.OutputMetadata)
                log.LogInformation("[AntiSpoof] Output {Name} => {Shape} {Type}",
                    kv.Key, string.Join("x", kv.Value.Dimensions), kv.Value.ElementType);

            return session;
        }, isThreadSafe: true);
    }

    public InferenceSession Get() => _session.Value;

    public void Dispose()
    {
        if (_session.IsValueCreated)
            _session.Value.Dispose();
    }
}