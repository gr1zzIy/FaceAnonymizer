using FaceAnonymizer.Core.Abstractions;
using FaceAnonymizer.Core.Models;
using OpenCvSharp;

namespace FaceAnonymizer.Infrastructure.Anonymizers;

public sealed class PixelationAnonymizer : IAnonymizer
{
    public string Name => "pixelation";

    public void Apply(Mat bgrImage, IReadOnlyList<FaceBox> faces, float strength)
    {
        if (faces.Count == 0) return;

        // Strength → коефіцієнт зменшення.
        // 0.0 → слабка піксельність (більший розмір), 1.0 → сильна (менший розмір).
        float scale = Lerp(0.35f, 0.06f, strength);

        foreach (var face in faces)
        {
            if (face.IsEmpty) continue;
            var roi = RoiHelper.Clip(face, bgrImage.Width, bgrImage.Height);
            if (roi.Width <= 0 || roi.Height <= 0) continue;

            using var sub = new Mat(bgrImage, roi);

            int smallW = Math.Max(1, (int)MathF.Round(sub.Width * scale));
            int smallH = Math.Max(1, (int)MathF.Round(sub.Height * scale));

            using var small = new Mat();
            Cv2.Resize(sub, small, new Size(smallW, smallH), 0, 0, InterpolationFlags.Area);
            Cv2.Resize(small, sub, new Size(sub.Width, sub.Height), 0, 0, InterpolationFlags.Nearest);
        }
    }

    private static float Lerp(float a, float b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return a + (b - a) * t;
    }

}
