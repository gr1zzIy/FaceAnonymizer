namespace FaceAnonymizer.Core.Models;

public sealed record BatchFileRow(
    string File,
    int FacesBefore,
    double? Precision,
    double? Recall,
    double? F1,
    double? MeanIoU,
    double? Ssim,
    double? PsnrDb,
    double DetectMs,
    double AnonymizeMs,
    double RedetectMs,
    double TotalMs,
    string OutputFile
);

public sealed record BatchRunSummary(
    string RunId,
    DateTime UtcStartedAt,
    DateTime UtcFinishedAt,
    string Engine,
    AnonymizationMethod Method,
    float Strength,
    bool EvaluateRedetection,
    double IoUThreshold,
    int TotalFiles,
    int ProcessedOk,
    int Failed,
    double ElapsedMs,
    double AvgDetectMs,
    double AvgAnonymizeMs,
    double AvgRedetectMs,
    double AvgTotalMs,
    double? AvgPrecision,
    double? AvgRecall,
    double? AvgF1,
    double? AvgMeanIoU,
    double? AvgSsim,
    double? AvgPsnrDb
);