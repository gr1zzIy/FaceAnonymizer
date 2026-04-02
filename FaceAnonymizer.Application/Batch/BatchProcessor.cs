using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using FaceAnonymizer.Application.Services;
using FaceAnonymizer.Core.Abstractions;
using FaceAnonymizer.Core.Models;
using FaceAnonymizer.Infrastructure.Imaging;
using Microsoft.Extensions.Logging;

namespace FaceAnonymizer.Application.Batch;

public sealed class BatchProcessor
{
    private static readonly string[] SupportedExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".webp"];

    private readonly FaceAnonymizationService _anonymize;
    private readonly ILogger<BatchProcessor> _logger;

    private readonly IBatchReportGenerator _report;

    public BatchProcessor(FaceAnonymizationService anonymize, IBatchReportGenerator report, ILogger<BatchProcessor> logger)
    {
        _anonymize = anonymize;
        _report = report;
        _logger = logger;
    }

    public BatchResult Run(BatchRequest request, CancellationToken ct = default)
    {
        var runId = string.IsNullOrWhiteSpace(request.RunId)
            ? $"run_{DateTime.UtcNow:yyyyMMdd_HHmmss_ffff}"
            : request.RunId;

        var runDir = Path.Combine(request.OutputDirectory, runId);
        var anonDir = Path.Combine(runDir, "images", "anon");
        var detectDir = Path.Combine(runDir, "images", "detect");

        Directory.CreateDirectory(anonDir);
        Directory.CreateDirectory(detectDir);

        var files = Directory.EnumerateFiles(request.InputDirectory, "*.*", SearchOption.AllDirectories)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _logger.LogInformation(
            "Batch started: runId={RunId}, files={FileCount}, engine={Engine}, method={Method}",
            runId, files.Count, request.Engine, request.Method);

        var csvPath = Path.Combine(runDir, "report.csv");
        var jsonPath = Path.Combine(runDir, "summary.json");
        var pdfPath = Path.Combine(runDir, "report.pdf");

        int ok = 0, failed = 0;
        var startedAt = DateTime.UtcNow;
        var swTotal = Stopwatch.StartNew();

        var rows = new ConcurrentBag<(int Index, BatchFileRow Row)>();
        int okCount = 0, failCount = 0;

        Parallel.For(0, files.Count, new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2),
            CancellationToken = ct
        }, i =>
        {
            var file = files[i];
            try
            {
                var bytes = File.ReadAllBytes(file);
                var res = _anonymize.Anonymize(bytes, request.Engine, request.Method, request.Strength,
                    request.EvaluateRedetection, request.IoUThreshold, request.ColorHex);

                var relPath = Path.GetRelativePath(request.InputDirectory, file);
                var relNoExt = Path.ChangeExtension(relPath, null);
                var ext = IImageCodec.Extension(res.OutputFormat);

                // 1) anonymized — у форматі джерела
                var anonRel = relNoExt + ".anon" + ext;
                var anonPath = Path.Combine(anonDir, anonRel);
                Directory.CreateDirectory(Path.GetDirectoryName(anonPath)!);
                File.WriteAllBytes(anonPath, res.OutputImage);

                // 2) detected (оригінал + рамки облич) — у форматі джерела
                var detectRel = relNoExt + ".detect" + ext;
                var detectPath = Path.Combine(detectDir, detectRel);
                Directory.CreateDirectory(Path.GetDirectoryName(detectPath)!);
                var detectBytes = DetectionDraw.Draw(bytes, res.Faces, res.OutputFormat);
                File.WriteAllBytes(detectPath, detectBytes);

                var m = res.Metrics;

                rows.Add((i, new BatchFileRow(
                    File: file,
                    FacesBefore: res.Faces.Count,
                    Precision: m?.Precision,
                    Recall: m?.Recall,
                    F1: m?.F1,
                    MeanIoU: m?.MeanIoU,
                    Ssim: m?.Ssim,
                    PsnrDb: m?.PsnrDb,
                    DetectMs: res.Timings.DetectMs,
                    AnonymizeMs: res.Timings.AnonymizeMs,
                    RedetectMs: res.Timings.RedetectMs,
                    TotalMs: res.Timings.TotalMs,
                    OutputFile: anonPath
                )));

                Interlocked.Increment(ref okCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process '{File}'", file);
                Interlocked.Increment(ref failCount);
            }
        });

        ok = okCount;
        failed = failCount;

        var sortedRows = rows.OrderBy(r => r.Index).Select(r => r.Row).ToList();

        swTotal.Stop();
        var finishedAt = DateTime.UtcNow;

        _logger.LogInformation(
            "Пакетна обробка завершена: runId={RunId}, успішно={Ok}, помилок={Failed}, час={ElapsedMs:F1} мс",
            runId, ok, failed, swTotal.Elapsed.TotalMilliseconds);

        // CSV
        using (var writer = new StreamWriter(csvPath, false, new System.Text.UTF8Encoding(true)))
        {
            writer.WriteLine("file,faces_before,precision,recall,f1,mean_iou,ssim,psnr_db,detect_ms,anonymize_ms,redetect_ms,total_ms,output_file");

            foreach (var r in sortedRows)
            {
                writer.WriteLine(string.Join(",",
                    Csv(r.File),
                    r.FacesBefore.ToString(CultureInfo.InvariantCulture),
                    CsvNum(r.Precision),
                    CsvNum(r.Recall),
                    CsvNum(r.F1),
                    CsvNum(r.MeanIoU),
                    CsvNum(r.Ssim),
                    CsvNum(r.PsnrDb),
                    CsvNum(r.DetectMs),
                    CsvNum(r.AnonymizeMs),
                    CsvNum(r.RedetectMs),
                    CsvNum(r.TotalMs),
                    Csv(r.OutputFile)
                ));
            }
        }

        // summary
        double Avg(IEnumerable<double> xs) => xs.Any() ? xs.Average() : 0;
        double? AvgN(IEnumerable<double?> xs)
        {
            var v = xs.Where(x => x.HasValue).Select(x => x!.Value).ToArray();
            return v.Length == 0 ? null : v.Average();
        }

        var summary = new BatchRunSummary(
            RunId: runId,
            UtcStartedAt: startedAt,
            UtcFinishedAt: finishedAt,
            Engine: request.Engine,
            Method: request.Method,
            Strength: request.Strength,
            EvaluateRedetection: request.EvaluateRedetection,
            IoUThreshold: request.IoUThreshold,
            TotalFiles: files.Count,
            ProcessedOk: ok,
            Failed: failed,
            ElapsedMs: swTotal.Elapsed.TotalMilliseconds,
            AvgDetectMs: Avg(sortedRows.Select(x => x.DetectMs)),
            AvgAnonymizeMs: Avg(sortedRows.Select(x => x.AnonymizeMs)),
            AvgRedetectMs: Avg(sortedRows.Select(x => x.RedetectMs)),
            AvgTotalMs: Avg(sortedRows.Select(x => x.TotalMs)),
            AvgPrecision: AvgN(sortedRows.Select(x => x.Precision)),
            AvgRecall: AvgN(sortedRows.Select(x => x.Recall)),
            AvgF1: AvgN(sortedRows.Select(x => x.F1)),
            AvgMeanIoU: AvgN(sortedRows.Select(x => x.MeanIoU)),
            AvgSsim: AvgN(sortedRows.Select(x => x.Ssim)),
            AvgPsnrDb: AvgN(sortedRows.Select(x => x.PsnrDb))
        );

        File.WriteAllText(jsonPath,
            System.Text.Json.JsonSerializer.Serialize(summary, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }),
            new System.Text.UTF8Encoding(true));

        var pdfBytes = _report.GeneratePdf(summary, sortedRows);
        File.WriteAllBytes(pdfPath, pdfBytes);

        return new BatchResult(
            RunId: runId,
            TotalFiles: files.Count,
            ProcessedOk: ok,
            Failed: failed,
            ElapsedMs: swTotal.Elapsed.TotalMilliseconds,
            CsvReportPath: csvPath,
            PdfReportPath: pdfPath
        );
    }

    private static string Csv(string s) => '"' + s.Replace("\"", "\"\"") + '"';
    private static string CsvNum(double? v) => v is null ? "" : v.Value.ToString("0.####", CultureInfo.InvariantCulture);
}

public sealed record BatchRequest(
    string RunId,
    string InputDirectory,
    string OutputDirectory,
    string Engine,
    AnonymizationMethod Method,
    float Strength,
    bool EvaluateRedetection,
    double IoUThreshold,
    string? ColorHex = null
);

public sealed record BatchResult(
    string RunId,
    int TotalFiles,
    int ProcessedOk,
    int Failed,
    double ElapsedMs,
    string CsvReportPath,
    string? PdfReportPath
);