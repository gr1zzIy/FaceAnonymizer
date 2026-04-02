namespace FaceAnonymizer.Core.Models;

public sealed record TimingInfo(
    double DetectMs,
    double AnonymizeMs,
    double RedetectMs,
    double TotalMs,
    double AntiSpoofMs = 0,
    double EncodeMs = 0,
    double EvaluationMs = 0
);
