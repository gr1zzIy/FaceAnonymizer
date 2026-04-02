using FaceAnonymizer.Core.Abstractions;
using FaceAnonymizer.Core.Models;
using OpenCvSharp;

namespace FaceAnonymizer.Infrastructure.Anonymizers;

public sealed class GaussianBlurAnonymizer : IAnonymizer
{
    public string Name => "gaussian-blur";

    public void Apply(Mat bgrImage, IReadOnlyList<FaceBox> faces, float strength)
    {
        if (faces.Count == 0) return;

        // Strength → розмір ядра. Завжди непарний.
        // 0.0 → мінімальне розмиття, 1.0 → сильне розмиття.
        int k = (int)MathF.Round(5 + strength * 55); // 5..60
        if ((k & 1) == 0) k++;

        foreach (var face in faces)
        {
            if (face.IsEmpty) continue;
            var roi = RoiHelper.Clip(face, bgrImage.Width, bgrImage.Height);
            if (roi.Width <= 0 || roi.Height <= 0) continue;

            using var sub = new Mat(bgrImage, roi);
            Cv2.GaussianBlur(sub, sub, new Size(k, k), 0);
        }
    }
}
