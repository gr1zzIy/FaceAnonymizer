namespace FaceAnonymizer.Infrastructure.Options;

public sealed class AntiSpoofOptions
{
    public bool Enabled { get; init; } = true;
    public string ModelPath { get; init; } = "assets/anti_spoof.onnx";
    public int InputSize { get; init; } = 80;          // 80 або 112 або 128
    public float RealThreshold { get; init; } = 0.80f; // поріг для "Real"
    public bool UseRgb { get; init; } = true;          // якщо модель очікує RGB
    public bool Normalize01 { get; init; } = true;     // /255
    public float[] Mean { get; init; } = new float[] { 0f, 0f, 0f };
    public float[] Std  { get; init; } = new float[] { 1f, 1f, 1f };
    public int RealClassIndex { get; init; } = 2;
}