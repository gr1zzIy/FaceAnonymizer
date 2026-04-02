namespace FaceAnonymizer.Infrastructure.Options;

public sealed class FaceDetectionOptions
{
    public string AssetsPath { get; init; } = "assets";
    public string HaarCascadeFile { get; init; } = "haarcascade_frontalface_default.xml";
    public string YuNetOnnxFile { get; init; } = "face_detection_yunet_2023mar.onnx";

    /// <summary>Нормалізований поріг оцінки для YuNet (sqrt(cls*obj)).</summary>
    public float ScoreThreshold { get; init; } = 0.6f;

    public float NmsThreshold { get; init; } = 0.3f;
    public int TopK { get; init; } = 5000;
}
