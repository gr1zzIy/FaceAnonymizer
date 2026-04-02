namespace FaceAnonymizer.Core.Models;

public sealed record DetectResult(IReadOnlyList<FaceBox> Faces);
