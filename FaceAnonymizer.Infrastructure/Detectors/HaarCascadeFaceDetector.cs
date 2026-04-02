using FaceAnonymizer.Core.Abstractions;
using FaceAnonymizer.Core.Models;
using FaceAnonymizer.Infrastructure.Options;
using FaceAnonymizer.Infrastructure.Services;
using Microsoft.Extensions.Options;
using OpenCvSharp;

namespace FaceAnonymizer.Infrastructure.Detectors;

public sealed class HaarCascadeFaceDetector : IFaceDetector, IDisposable
{
    private readonly string _cascadePath;
    private readonly IImageCodec _codec;
    private CascadeClassifier? _cascade;

    public string Name => "haar";

    public HaarCascadeFaceDetector(IOptions<FaceDetectionOptions> options, IImageCodec codec)
    {
        _codec = codec;
        var o = options.Value;
        _cascadePath = Path.Combine(AppContext.BaseDirectory, o.AssetsPath, o.HaarCascadeFile);
    }

    public bool IsAvailable(out string reason)
    {
        if (!File.Exists(_cascadePath))
        {
            reason = $"Haar cascade not found at '{_cascadePath}'.";
            return false;
        }

        try
        {
            _cascade ??= new CascadeClassifier(_cascadePath);
            if (_cascade.Empty())
            {
                reason = "Failed to load Haar cascade.";
                return false;
            }

            reason = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            reason = $"Failed to init Haar detector: {ex.Message}";
            return false;
        }
    }

    public DetectResult Detect(ReadOnlySpan<byte> imageBytes)
    {
        if (_cascade is null)
            throw new InvalidOperationException("Cascade is not initialized. Call IsAvailable() first.");

        using var bgr = _codec.DecodeToBgr(imageBytes);
        return DetectMat(bgr);
    }

    public DetectResult DetectMat(Mat bgr)
    {
        if (_cascade is null)
            throw new InvalidOperationException("Cascade is not initialized. Call IsAvailable() first.");

        using var gray = new Mat();
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.EqualizeHist(gray, gray);

        var rects = _cascade.DetectMultiScale(gray, scaleFactor: 1.1, minNeighbors: 4, flags: HaarDetectionTypes.ScaleImage);

        var faces = rects
            .Select(r => new FaceBox(r.X, r.Y, r.Width, r.Height, 1.0f))
            .ToList();

        return new DetectResult(faces);
    }

    public void Dispose()
    {
        _cascade?.Dispose();
        _cascade = null;
    }
}
