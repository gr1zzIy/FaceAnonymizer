namespace FaceAnonymizer.Core.Models;

public sealed record EvaluationMetrics(
    double IoUThreshold,
    int TruePositives,
    int FalsePositives,
    int FalseNegatives,
    double Precision,
    double Recall,
    double F1,
    double MeanIoU,
    double? Ssim = null,
    double? PsnrDb = null
);
