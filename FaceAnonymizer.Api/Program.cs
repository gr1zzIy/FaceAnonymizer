using System.Text.Json.Serialization;
using FaceAnonymizer.Api.Middleware;
using FaceAnonymizer.Api.Options;
using FaceAnonymizer.Api.Services;
using FaceAnonymizer.Application.Batch;
using FaceAnonymizer.Application.Services;
using FaceAnonymizer.Core.Abstractions;
using FaceAnonymizer.Infrastructure.Anonymizers;
using FaceAnonymizer.Infrastructure.Detectors;
using FaceAnonymizer.Infrastructure.Options;
using FaceAnonymizer.Infrastructure.Reporting;
using FaceAnonymizer.Infrastructure.Services;
using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRouting(o => o.LowercaseUrls = true);

builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Face Anonymizer API",
        Version = "v1",
        Description = "API для автоматичного виявлення та анонімізації облич із використанням моделей глибинного навчання (Haar / YuNet ONNX)."
    });

    c.OperationFilter<FaceAnonymizer.Api.Swagger.FileUploadOperationFilter>();
});

// Налаштування детекції
// Налаштування детекції
builder.Services.Configure<FaceDetectionOptions>(builder.Configuration.GetSection("FaceDetection"));

// Налаштування пакетної обробки (нормалізація до storage/)
builder.Services.Configure<BatchOptions>(opt =>
{
    builder.Configuration.GetSection("Batch").Bind(opt);

    var storageRoot = Path.Combine(builder.Environment.ContentRootPath, "storage");
    Directory.CreateDirectory(storageRoot);

    opt.InputRoot = MakeAbsoluteUnderStorage(opt.InputRoot, storageRoot, "batch-input");
    opt.OutputRoot = MakeAbsoluteUnderStorage(opt.OutputRoot, storageRoot, "batch-output");

    Directory.CreateDirectory(opt.InputRoot);
    Directory.CreateDirectory(opt.OutputRoot);
});

static string MakeAbsoluteUnderStorage(string? configuredPath, string storageRoot, string defaultFolderName)
{
    if (string.IsNullOrWhiteSpace(configuredPath))
        return Path.GetFullPath(Path.Combine(storageRoot, defaultFolderName));

    if (Path.IsPathRooted(configuredPath))
        return Path.GetFullPath(configuredPath);

    return Path.GetFullPath(Path.Combine(storageRoot, configuredPath));
}

// Основні сервіси
builder.Services.AddSingleton<IImageCodec, OpenCvImageCodec>();
builder.Services.AddSingleton<IOnnxSessionProvider, OnnxSessionProvider>();

// Детектори
builder.Services.AddTransient<HaarCascadeFaceDetector>();
builder.Services.AddTransient<YuNetOnnxFaceDetector>();
builder.Services.AddSingleton<IFaceDetectorFactory, FaceDetectorFactory>();

// Анонімізатори
builder.Services.AddSingleton<IAnonymizer, GaussianBlurAnonymizer>();
builder.Services.AddSingleton<IAnonymizer, PixelationAnonymizer>();
builder.Services.AddSingleton<IAnonymizer, SolidColorAnonymizer>();

// Прикладні сервіси
builder.Services.AddSingleton<FaceDetectionService>();
builder.Services.AddSingleton<FaceAnonymizationService>();
builder.Services.AddSingleton<BatchProcessor>();
builder.Services.AddSingleton<ResultStore>();

builder.Services.AddSingleton<ProblemDetailsExceptionMiddleware>();

// Anti-Spoof
builder.Services.Configure<FaceAnonymizer.Infrastructure.Options.AntiSpoofOptions>(
    builder.Configuration.GetSection("AntiSpoof"));

builder.Services.AddSingleton<IAntiSpoofSessionProvider, AntiSpoofSessionProvider>();
builder.Services.AddSingleton<IFaceAuthenticityClassifier, OnnxFaceAuthenticityClassifier>();

// Звітність
builder.Services.AddSingleton<IBatchReportGenerator, QuestPdfBatchReportGenerator>();

QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();

app.UseMiddleware<ProblemDetailsExceptionMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

app.Run();