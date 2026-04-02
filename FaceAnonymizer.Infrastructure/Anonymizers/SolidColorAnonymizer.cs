using FaceAnonymizer.Core.Abstractions;
using FaceAnonymizer.Core.Models;
using OpenCvSharp;

namespace FaceAnonymizer.Infrastructure.Anonymizers;

/// <summary>
/// Заповнює область обличчя суцільним кольором з альфа-змішуванням.
/// </summary>
public sealed class SolidColorAnonymizer : IAnonymizer
{
    public string Name => "solid-color";

    public void Apply(Mat bgrImage, IReadOnlyList<FaceBox> faces, float strength)
    {
        ApplyWithColor(bgrImage, faces, strength, "#000000");
    }

    /// <summary>
    /// Застосовує анонімізацію з вказаним кольором у форматі hex (#RRGGBB).
    /// </summary>
    public void ApplyWithColor(Mat bgrImage, IReadOnlyList<FaceBox> faces, float strength, string hexColor)
    {
        if (faces.Count == 0) return;

        strength = Math.Clamp(strength, 0f, 1f);
        if (strength <= 0f) return;

        var fill = ParseHexColorToBgr(hexColor);

        foreach (var face in faces)
        {
            if (face.IsEmpty) continue;
            var roi = RoiHelper.Clip(face, bgrImage.Width, bgrImage.Height);
            if (roi.Width <= 0 || roi.Height <= 0) continue;

            using var sub = new Mat(bgrImage, roi);

            if (strength >= 0.999f)
            {
                sub.SetTo(fill);
                continue;
            }

            // Змішування: dst = (1-a)*src + a*fill
            using var overlay = new Mat(sub.Size(), sub.Type(), fill);
            Cv2.AddWeighted(overlay, strength, sub, 1.0 - strength, 0, sub);
        }
    }

    /// <summary>Парсинг #RRGGBB у BGR Scalar для OpenCV.</summary>
    private static Scalar ParseHexColorToBgr(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return new Scalar(0, 0, 0);

        hex = hex.Trim();
        if (hex.StartsWith("#")) hex = hex[1..];
        if (hex.Length != 6) return new Scalar(0, 0, 0);

        try
        {
            int r = Convert.ToInt32(hex[0..2], 16);
            int g = Convert.ToInt32(hex[2..4], 16);
            int b = Convert.ToInt32(hex[4..6], 16);
            return new Scalar(b, g, r); // BGR порядок для OpenCV
        }
        catch
        {
            return new Scalar(0, 0, 0);
        }
    }
}
