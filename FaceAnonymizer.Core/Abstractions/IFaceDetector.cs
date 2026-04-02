using FaceAnonymizer.Core.Models;
using OpenCvSharp;

namespace FaceAnonymizer.Core.Abstractions;

public interface IFaceDetector
{
    string Name { get; }

    /// <summary>Перевіряє доступність моделі/ресурсів без виключень.</summary>
    bool IsAvailable(out string reason);

    DetectResult Detect(ReadOnlySpan<byte> imageBytes);

    /// <summary>
    /// Виявлення облич безпосередньо з Mat (без кодування/декодування).
    /// Використовується для re-detect на етапі оцінки — уникає зайвого циклу encode→decode.
    /// </summary>
    DetectResult DetectMat(Mat bgr);
}
