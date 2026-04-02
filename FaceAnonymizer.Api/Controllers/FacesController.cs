using FaceAnonymizer.Api.Options;
using FaceAnonymizer.Api.Services;
using FaceAnonymizer.Application.Batch;
using FaceAnonymizer.Application.Services;
using FaceAnonymizer.Core.Abstractions;
using FaceAnonymizer.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.IO.Compression;
using FaceAnonymizer.Infrastructure.Imaging;

namespace FaceAnonymizer.Api.Controllers;

[ApiController]
[Route("api/faces")]
public sealed class FacesController : ControllerBase
{
    private const long MaxImageSize = 25 * 1024 * 1024; // 25 MB
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/bmp", "image/webp"
    };

    private readonly FaceDetectionService _detect;
    private readonly FaceAnonymizationService _anonymize;
    private readonly IFaceDetectorFactory _factory;
    private readonly BatchOptions _batch;
    private readonly BatchProcessor _batchProcessor;
    private readonly ResultStore _store;

    public FacesController(
        FaceDetectionService detect,
        FaceAnonymizationService anonymize,
        IFaceDetectorFactory factory,
        IOptions<BatchOptions> batch,
        BatchProcessor batchProcessor,
        ResultStore store)
    {
        _detect = detect;
        _anonymize = anonymize;
        _factory = factory;
        _batch = batch.Value;
        _batchProcessor = batchProcessor;
        _store = store;
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "ok", utc = DateTime.UtcNow });

    [HttpGet("capabilities")]
    public IActionResult Capabilities()
    {
        var methods = Enum.GetNames(typeof(AnonymizationMethod));
        return Ok(new
        {
            engines = _factory.GetSupportedEngines(),
            methods,
            batch = new
            {
                inputRoot = _batch.InputRoot,
                outputRoot = _batch.OutputRoot
            }
        });
    }

    [HttpPost("detect")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<DetectResponse>> Detect(
        [FromForm] FaceAnonymizer.Api.Contracts.ImageForm form,
        [FromQuery] string engine = "yunet",
        [FromQuery] bool draw = true)
    {
        var image = form.Image;
        var validation = ValidateImage(image);
        if (validation is not null) return validation;

        var bytes = await ReadAllBytes(image!);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var (detector, result) = _detect.Detect(bytes, engine);
        sw.Stop();

        string? imageUrl = null;
        if (draw)
        {
            var format = IImageCodec.DetectFormat(bytes);
            var drawn = DetectionDraw.Draw(bytes, result.Faces, format);
            var id = _store.Save(drawn, format);
            imageUrl = $"/api/faces/result/{id}";
        }

        return Ok(new DetectResponse(
            Engine: detector.Name,
            ElapsedMs: sw.Elapsed.TotalMilliseconds,
            Faces: result.Faces,
            ImageUrl: imageUrl,
            SupportedEngines: _factory.GetSupportedEngines()
        ));
    }

    [HttpPost("anonymize")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<AnonymizeResponse>> Anonymize(
        [FromForm] FaceAnonymizer.Api.Contracts.ImageForm form,
        [FromQuery] string engine = "yunet",
        [FromQuery] AnonymizationMethod method = AnonymizationMethod.GaussianBlur,
        [FromQuery] float strength = 0.75f,
        [FromQuery] bool evaluate = true,
        [FromQuery] double iouThreshold = 0.5,
        [FromQuery] string? color = null)
    {
        var image = form.Image;
        var validation = ValidateImage(image);
        if (validation is not null) return validation;

        var bytes = await ReadAllBytes(image!);

        var res = _anonymize.Anonymize(bytes, engine, method, strength, evaluate, iouThreshold, color);

        var id = _store.Save(res.OutputImage, res.OutputFormat);
        var imageUrl = $"/api/faces/result/{id}";

        return Ok(new AnonymizeResponse(
            Engine: res.Engine,
            Method: res.Method,
            Strength: res.Strength,
            Faces: res.Faces,
            ImageUrl: imageUrl,
            Metrics: res.Metrics,
            Timings: res.Timings
        ));
    }

    /// <summary>
    /// Повертає результат обробки як бінарний потік (image/png або image/jpeg).
    /// Це production-підхід: замість base64 (що роздуває трафік на ~33%)
    /// клієнт отримує URL і завантажує зображення напряму.
    /// </summary>
    [HttpGet("result/{id}")]
    public IActionResult GetResult(string id)
    {
        var entry = _store.Get(id);
        if (entry is null)
            return NotFound(new { error = "Результат не знайдено або термін зберігання закінчився." });

        return PhysicalFile(entry.Value.Path, entry.Value.ContentType);
    }

    /// <summary>Валідація завантаженого зображення: наявність, розмір, MIME-тип.</summary>
    private ActionResult? ValidateImage(IFormFile? image)
    {
        if (image is null || image.Length == 0)
            return BadRequest(new { error = "Поле 'image' є обов'язковим." });

        if (image.Length > MaxImageSize)
            return BadRequest(new { error = $"Зображення занадто велике ({image.Length / 1024 / 1024} МБ). Максимум: {MaxImageSize / 1024 / 1024} МБ." });

        if (!string.IsNullOrEmpty(image.ContentType) && !AllowedMimeTypes.Contains(image.ContentType))
            return BadRequest(new { error = $"Непідтримуваний MIME-тип '{image.ContentType}'. Дозволені: {string.Join(", ", AllowedMimeTypes)}." });

        return null;
    }

    [HttpPost("batch")]
    public ActionResult<BatchResultResponse> Batch([FromBody] BatchRequestDto dto)
    {
        var runId = string.IsNullOrWhiteSpace(dto.RunId)
            ? $"run_{DateTime.UtcNow:yyyyMMdd_HHmmss_ffff}"
            : dto.RunId;

        var expectedInputFolder = $"{runId}/input";

        var inputFolder = string.IsNullOrWhiteSpace(dto.InputFolder)
            ? expectedInputFolder
            : dto.InputFolder.Replace('\\', '/');

        if (!string.Equals(inputFolder, expectedInputFolder, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = $"InputFolder має бути '{expectedInputFolder}' для runId '{runId}'." });

        var inDir = ResolveUnderRoot(_batch.InputRoot, inputFolder);

        var outRoot = ResolveUnderRoot(_batch.OutputRoot, string.Empty);

        var req = new BatchRequest(
            RunId: runId,
            InputDirectory: inDir,
            OutputDirectory: outRoot,
            Engine: dto.Engine ?? "yunet",
            Method: dto.Method,
            Strength: dto.Strength,
            EvaluateRedetection: dto.Evaluate,
            IoUThreshold: dto.IoUThreshold,
            ColorHex: dto.ColorHex
        );

        var result = _batchProcessor.Run(req);

        return Ok(new BatchResultResponse(
            RunId: result.RunId,
            TotalFiles: result.TotalFiles,
            ProcessedOk: result.ProcessedOk,
            Failed: result.Failed,
            ElapsedMs: result.ElapsedMs,
            CsvReport: $"/api/faces/batch/report.csv?runId={Uri.EscapeDataString(result.RunId)}",
            PdfReport: $"/api/faces/batch/report.pdf?runId={Uri.EscapeDataString(result.RunId)}"
        ));
    }


    [HttpGet("batch/report.csv")]
    public IActionResult DownloadBatchCsv([FromQuery] string runId)
    {
        if (string.IsNullOrWhiteSpace(runId))
            return BadRequest(new { error = "Параметр 'runId' є обов'язковим." });

        var relative = Path.Combine(runId, "report.csv");
        var full = ResolveUnderRoot(_batch.OutputRoot, relative);

        if (!System.IO.File.Exists(full))
            return NotFound(new { error = "CSV-звіт не знайдено." });

        return PhysicalFile(full, "text/csv", "report.csv");
    }

    [HttpGet("batch/report.pdf")]
    public IActionResult DownloadBatchPdf([FromQuery] string runId, [FromQuery] bool inline = true)
    {
        if (string.IsNullOrWhiteSpace(runId))
            return BadRequest(new { error = "Параметр 'runId' є обов'язковим." });

        var relative = Path.Combine(runId, "report.pdf");
        var full = ResolveUnderRoot(_batch.OutputRoot, relative);

        if (!System.IO.File.Exists(full))
            return NotFound(new { error = "PDF-звіт не знайдено." });

        // inline перегляд у браузері
        Response.Headers["Content-Disposition"] = inline
            ? "inline; filename=report.pdf"
            : "attachment; filename=report.pdf";

        return PhysicalFile(full, "application/pdf");
    }
    
    private static async Task<byte[]> ReadAllBytes(IFormFile f)
    {
        await using var s = f.OpenReadStream();
        using var ms = new MemoryStream((int)Math.Min(f.Length, int.MaxValue));
        await s.CopyToAsync(ms);
        return ms.ToArray();
    }

    private static string ResolveUnderRoot(string root, string? relative)
    {
        relative ??= string.Empty;

        var rootFull = Path.GetFullPath(root);
        Directory.CreateDirectory(rootFull);

        var candidate = Path.GetFullPath(Path.Combine(rootFull, relative));

        if (!candidate.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Invalid folder path.");

        return candidate;
    }

    [HttpPost("batch/upload")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<BatchUploadResponse>> UploadBatch(
        [FromForm] string? runId,
        [FromForm] List<IFormFile> files,
        [FromForm] List<string>? paths)
    {
        if (files is null || files.Count == 0)
            return BadRequest(new { error = "Поле 'files' є обов'язковим." });

        // paths[i] = webkitRelativePath (якщо фронт передає)
        if (paths is not null && paths.Count != files.Count)
            return BadRequest(new { error = "Поле 'paths' повинно мати таку ж довжину як 'files'." });

        runId = string.IsNullOrWhiteSpace(runId)
            ? $"run_{DateTime.UtcNow:yyyyMMdd_HHmmss_ffff}"
            : runId;

        var inputRoot = ResolveUnderRoot(_batch.InputRoot, string.Empty);

        // InputRoot/<runId>/input/...
        var runInputDir = ResolveUnderRoot(_batch.InputRoot, Path.Combine(runId, "input"));
        Directory.CreateDirectory(runInputDir);

        for (int i = 0; i < files.Count; i++)
        {
            var f = files[i];
            if (f.Length == 0) continue;

            var rel = (paths is not null ? paths[i] : f.FileName) ?? f.FileName;

            // нормалізація: прибираємо \, ../ і т.п.
            rel = rel.Replace('\\', '/').TrimStart('/');
            while (rel.StartsWith("../", StringComparison.Ordinal)) rel = rel[3..];

            // кінцевий шлях всередині runInputDir
            var target = Path.GetFullPath(Path.Combine(runInputDir, rel));
            if (!target.StartsWith(runInputDir, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "Некоректний шлях у 'paths'." });

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            await using var src = f.OpenReadStream();
            await using var dst = System.IO.File.Create(target);
            await src.CopyToAsync(dst);
        }

        // фронт потім передає це в Batch()
        var inputFolder = Path.Combine(runId, "input").Replace('\\', '/');

        return Ok(new BatchUploadResponse(runId, inputFolder));
    }

    [HttpGet("batch/outputs")]
    public ActionResult<IReadOnlyList<OutputFileInfoDto>> ListBatchOutputs(
        [FromQuery] string runId,
        [FromQuery] string? kind = "anon")
    {
        if (string.IsNullOrWhiteSpace(runId))
            return BadRequest(new { error = "Параметр 'runId' є обов'язковим." });

        kind = string.IsNullOrWhiteSpace(kind) ? "anon" : kind.Trim().ToLowerInvariant();
        if (kind != "anon" && kind != "detect")
            return BadRequest(new { error = "Параметр 'kind' має бути 'anon' або 'detect'." });

        // OutputRoot/<runId>/images/<kind>
        var runDir = ResolveUnderRoot(_batch.OutputRoot, runId);
        var imagesDir = ResolveUnderRoot(_batch.OutputRoot, Path.Combine(runId, "images", kind));

        if (!Directory.Exists(imagesDir))
            return Ok(Array.Empty<OutputFileInfoDto>());

        var files = Directory.EnumerateFiles(imagesDir, "*.*", SearchOption.AllDirectories)
            .Where(f =>
                f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Select(f =>
            {
                // rel від runDir => "images/...."
                var rel = Path.GetRelativePath(runDir, f).Replace('\\', '/');
                var name = Path.GetFileName(f);
                var size = new FileInfo(f).Length;
                return new OutputFileInfoDto(name, rel, size);
            })
            .ToList();

        return Ok(files);
    }

    [HttpGet("batch/output-file")]
    public IActionResult GetBatchOutputFile([FromQuery] string runId, [FromQuery] string path)
    {
        if (string.IsNullOrWhiteSpace(runId) || string.IsNullOrWhiteSpace(path))
            return BadRequest(new { error = "Параметри runId та path є обов'язковими." });

        var safeRel = path.Replace('\\', '/').TrimStart('/');

        // простий захист
        if (safeRel.Contains("..", StringComparison.Ordinal))
            return BadRequest(new { error = "Некоректний шлях." });

        var fullPath = ResolveUnderRoot(_batch.OutputRoot, Path.Combine(runId, safeRel));

        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { error = "Вихідний файл не знайдено." });

        var contentType = "application/octet-stream";
        if (fullPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) contentType = "image/png";
        else if (fullPath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || fullPath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)) contentType = "image/jpeg";
        else if (fullPath.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)) contentType = "image/webp";
        else if (fullPath.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)) contentType = "image/bmp";

        return PhysicalFile(fullPath, contentType);
    }
    
    [HttpGet("batch/download.zip")]
    public IActionResult DownloadBatchZip([FromQuery] string runId)
    {
        if (string.IsNullOrWhiteSpace(runId))
            return BadRequest(new { error = "Параметр 'runId' є обов'язковим." });

        var runDir = ResolveUnderRoot(_batch.OutputRoot, runId);

        if (!Directory.Exists(runDir))
            return NotFound(new { error = "Папку результатів не знайдено." });

        // Створюємо ZIP в памʼяті (для диплома ок; якщо буде дуже багато файлів — можна стрімити)
        var ms = new MemoryStream();

        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Додаємо всі файли з runDir рекурсивно
            var files = Directory.EnumerateFiles(runDir, "*", SearchOption.AllDirectories);

            foreach (var fullPath in files)
            {
                // entry path всередині zip: відносно runDir, з "/"
                var entryName = Path.GetRelativePath(runDir, fullPath).Replace('\\', '/');

                // Захист від дивних імен
                if (string.IsNullOrWhiteSpace(entryName) || entryName.Contains("..", StringComparison.Ordinal))
                    continue;

                zip.CreateEntryFromFile(fullPath, entryName, CompressionLevel.Fastest);
            }
        }

        ms.Position = 0;

        var fileName = $"{runId}.zip";
        return File(ms, "application/zip", fileName);
    }
}

public sealed record DetectResponse(
    string Engine,
    double ElapsedMs,
    IReadOnlyList<FaceBox> Faces,
    string? ImageUrl,
    IReadOnlyCollection<string> SupportedEngines
);

public sealed record AnonymizeResponse(
    string Engine,
    AnonymizationMethod Method,
    float Strength,
    IReadOnlyList<FaceBox> Faces,
    string ImageUrl,
    EvaluationMetrics? Metrics,
    TimingInfo Timings
);

public sealed record BatchRequestDto(
    string? RunId,
    string? InputFolder,
    string? Engine,
    AnonymizationMethod Method = AnonymizationMethod.GaussianBlur,
    float Strength = 0.75f,
    bool Evaluate = true,
    double IoUThreshold = 0.5,
    string? ColorHex = null
);

public sealed record BatchResultResponse(
    string RunId,
    int TotalFiles,
    int ProcessedOk,
    int Failed,
    double ElapsedMs,
    string CsvReport,
    string PdfReport
);

public sealed record BatchUploadResponse(string RunId, string InputFolder);

public sealed record OutputFileInfoDto(string Name, string RelativePath, long SizeBytes);

