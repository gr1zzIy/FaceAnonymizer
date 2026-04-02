using System.Diagnostics;
using FaceAnonymizer.Application.Evaluation;
using FaceAnonymizer.Core.Abstractions;
using FaceAnonymizer.Core.Models;
using FaceAnonymizer.Infrastructure.Anonymizers;
using FaceAnonymizer.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenCvSharp;

namespace FaceAnonymizer.Application.Services;

public sealed class FaceAnonymizationService
{
    private readonly IFaceDetectorFactory _detectorFactory;
    private readonly IImageCodec _codec;
    private readonly IReadOnlyDictionary<AnonymizationMethod, IAnonymizer> _anonymizers;
    private readonly IFaceAuthenticityClassifier _auth;
    private readonly AntiSpoofOptions _antiSpoof;
    private readonly ILogger<FaceAnonymizationService> _logger;

    public FaceAnonymizationService(
        IFaceDetectorFactory detectorFactory,
        IImageCodec codec,
        IEnumerable<IAnonymizer> anonymizers,
        IFaceAuthenticityClassifier auth,
        IOptions<AntiSpoofOptions> antiSpoof,
        ILogger<FaceAnonymizationService> logger)
    {
        _detectorFactory = detectorFactory;
        _codec = codec;
        _auth = auth;
        _antiSpoof = antiSpoof.Value;
        _logger = logger;

        _anonymizers = anonymizers.ToDictionary(a => a.Name switch
        {
            "gaussian-blur" => AnonymizationMethod.GaussianBlur,
            "pixelation" => AnonymizationMethod.Pixelation,
            "solid-color" => AnonymizationMethod.SolidColor,
            _ => throw new InvalidOperationException($"Unknown anonymizer name '{a.Name}'.")
        });
    }

    public AnonymizeResult Anonymize(
        ReadOnlySpan<byte> imageBytes,
        string engine,
        AnonymizationMethod method,
        float strength,
        bool evaluateRedetection,
        double iouThreshold = 0.5,
        string? colorHex = null)
    {
        strength = Math.Clamp(strength, 0f, 1f);

        // Визначаємо формат вхідного зображення (PNG→PNG, інше→JPEG)
        var outputFormat = IImageCodec.DetectFormat(imageBytes);

        var swTotal = Stopwatch.StartNew();

        // ── Етап 1: Виявлення облич ──
        var swDetect = Stopwatch.StartNew();
        var detector = _detectorFactory.Create(engine);
        if (!detector.IsAvailable(out var reason))
            throw new InvalidOperationException(reason);

        var before = detector.Detect(imageBytes);
        swDetect.Stop();

        _logger.LogInformation(
            "Виявлення завершено: двигун={Engine}, облич={FaceCount}, час={DetectMs:F1} мс",
            engine, before.Faces.Count, swDetect.Elapsed.TotalMilliseconds);

        // ── Етап 2: Декодування ──
        using var bgr = _codec.DecodeToBgr(imageBytes);

        // Клон оригіналу для обчислення SSIM/PSNR (до модифікації bgr)
        using var originalClone = evaluateRedetection ? bgr.Clone() : null;

        // ── Етап 3: Фільтрація Anti-Spoof ──
        var swAntiSpoof = Stopwatch.StartNew();
        IReadOnlyList<FaceBox> faces = _antiSpoof.Enabled && before.Faces.Count > 0
            ? FilterByAntiSpoof(before.Faces, bgr)
            : before.Faces;
        swAntiSpoof.Stop();

        // ── Етап 4: Анонімізація ──
        var swAnon = Stopwatch.StartNew();
        
        if (method == AnonymizationMethod.SolidColor && !string.IsNullOrWhiteSpace(colorHex))
        {
            var solidAnon = (SolidColorAnonymizer)_anonymizers[method];
            solidAnon.ApplyWithColor(bgr, faces, strength, colorHex);
        }
        else
        {
            var anonymizer = _anonymizers[method];
            anonymizer.Apply(bgr, faces, strength);
        }
        
        swAnon.Stop();

        _logger.LogInformation(
            "Анонімізація завершена: метод={Method}, сила={Strength:F2}, облич={FaceCount}, час={AnonMs:F1} мс",
            method, strength, faces.Count, swAnon.Elapsed.TotalMilliseconds);

        // ── Етап 5: Кодування (у вихідному форматі) ──
        var swEncode = Stopwatch.StartNew();
        var outputBytes = _codec.Encode(bgr, outputFormat);
        swEncode.Stop();

        _logger.LogInformation(
            "Кодування: формат={Format}, розмір={SizeKB:F0} КБ",
            outputFormat, outputBytes.Length / 1024.0);

        // ── Етап 6: Оцінка (опціонально) ──
        EvaluationMetrics? metrics = null;
        double redetectMs = 0;
        double evaluationMs = 0;

        if (evaluateRedetection)
        {
            var eval = RunEvaluation(detector, faces, outputBytes, originalClone, bgr, iouThreshold);
            metrics = eval.Metrics;
            redetectMs = eval.RedetectMs;
            evaluationMs = eval.EvalMs;
        }

        swTotal.Stop();

        _logger.LogInformation(
            "Пайплайн завершено: загалом={TotalMs:F1} мс [detect={DetectMs:F1}, antiSpoof={AntiSpoofMs:F1}, anon={AnonMs:F1}, encode={EncodeMs:F1}, eval={EvalMs:F1}]",
            swTotal.Elapsed.TotalMilliseconds, swDetect.Elapsed.TotalMilliseconds,
            swAntiSpoof.Elapsed.TotalMilliseconds, swAnon.Elapsed.TotalMilliseconds,
            swEncode.Elapsed.TotalMilliseconds, evaluationMs);

        return new AnonymizeResult(
            Engine: detector.Name,
            Method: method,
            Strength: strength,
            Faces: faces,
            OutputImage: outputBytes,
            OutputFormat: outputFormat,
            Metrics: metrics,
            Timings: new TimingInfo(
                DetectMs: swDetect.Elapsed.TotalMilliseconds,
                AnonymizeMs: swAnon.Elapsed.TotalMilliseconds,
                RedetectMs: redetectMs,
                TotalMs: swTotal.Elapsed.TotalMilliseconds,
                AntiSpoofMs: swAntiSpoof.Elapsed.TotalMilliseconds,
                EncodeMs: swEncode.Elapsed.TotalMilliseconds,
                EvaluationMs: evaluationMs)
        );
    }

    /// <summary>Фільтрує обличчя, залишаючи лише справжні (не спуф).</summary>
    private IReadOnlyList<FaceBox> FilterByAntiSpoof(IReadOnlyList<FaceBox> faces, Mat bgr)
    {
        var filtered = new List<FaceBox>(faces.Count);

        foreach (var f in faces)
        {
            if (f.IsEmpty) continue;

            var roi = RoiHelper.Clip(f, bgr.Width, bgr.Height);
            if (roi.Width <= 0 || roi.Height <= 0) continue;

            using var faceRoi = new Mat(bgr, roi);
            var auth = _auth.Classify(faceRoi);

            _logger.LogDebug("AntiSpoof: realScore={RealScore:F3}, label={Label}", auth.RealScore, auth.Label);

            if (auth.Label == FaceAuthenticity.Real && auth.RealScore >= _antiSpoof.RealThreshold)
                filtered.Add(f);
        }

        var rejected = faces.Count - filtered.Count;
        if (rejected > 0)
            _logger.LogInformation("AntiSpoof: відхилено {Rejected}/{Total} облич", rejected, faces.Count);

        return filtered;
    }

    /// <summary>Обчислює метрики повторного виявлення та якості зображення.</summary>
    private (EvaluationMetrics Metrics, double RedetectMs, double EvalMs) RunEvaluation(
        IFaceDetector detector, IReadOnlyList<FaceBox> faces, byte[] outputBytes,
        Mat? original, Mat anonymized, double iouThreshold)
    {
        var swEval = Stopwatch.StartNew();

        // Re-detect безпосередньо з Mat — уникаємо зайвого циклу encode→decode
        var swRedetect = Stopwatch.StartNew();
        var after = detector.DetectMat(anonymized);
        swRedetect.Stop();

        var detection = MetricsCalculator.CompareDetections(faces, after.Faces, iouThreshold);

        double? ssim = null;
        double? psnr = null;

        if (original is not null)
        {
            // Зменшуємо до max 1024px для SSIM/PSNR — значно швидше, метрики практично ідентичні
            const int MaxEvalSide = 1024;
            var (evalOrig, evalAnon) = DownscaleForEval(original, anonymized, MaxEvalSide);

            ssim = MetricsCalculator.CalculateSsim(evalOrig, evalAnon);
            psnr = MetricsCalculator.CalculatePsnr(evalOrig, evalAnon);

            if (evalOrig != original) evalOrig.Dispose();
            if (evalAnon != anonymized) evalAnon.Dispose();

            _logger.LogInformation("Якість зображення: SSIM={Ssim:F4}, PSNR={PsnrDb:F2} дБ", ssim, psnr);
        }

        var metrics = detection with { Ssim = ssim, PsnrDb = psnr };
        swEval.Stop();

        return (metrics, swRedetect.Elapsed.TotalMilliseconds, swEval.Elapsed.TotalMilliseconds);
    }

    /// <summary>Зменшує пару зображень для обчислення метрик якості (якщо більші за maxSide).</summary>
    private static (Mat orig, Mat anon) DownscaleForEval(Mat original, Mat anonymized, int maxSide)
    {
        int maxDim = Math.Max(original.Width, original.Height);
        if (maxDim <= maxSide)
            return (original, anonymized);

        double scale = maxSide / (double)maxDim;
        var newSize = new Size((int)(original.Width * scale), (int)(original.Height * scale));

        var smallOrig = new Mat();
        var smallAnon = new Mat();
        Cv2.Resize(original, smallOrig, newSize, 0, 0, InterpolationFlags.Area);
        Cv2.Resize(anonymized, smallAnon, newSize, 0, 0, InterpolationFlags.Area);

        return (smallOrig, smallAnon);
    }
}

public sealed record AnonymizeResult(
    string Engine,
    AnonymizationMethod Method,
    float Strength,
    IReadOnlyList<FaceBox> Faces,
    byte[] OutputImage,
    ImageOutputFormat OutputFormat,
    EvaluationMetrics? Metrics,
    TimingInfo Timings
);