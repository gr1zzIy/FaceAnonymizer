namespace FaceAnonymizer.Core.Models;

public sealed record AuthenticityResult(
    FaceAuthenticity Label,
    float RealScore
);