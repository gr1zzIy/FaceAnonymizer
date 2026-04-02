using FaceAnonymizer.Core.Models;
using OpenCvSharp;

namespace FaceAnonymizer.Infrastructure.Anonymizers;

/// <summary>
/// Спільний хелпер для обрізки FaceBox до безпечного Rect у межах зображення.
/// </summary>
public static class RoiHelper
{
    public static Rect Clip(in FaceBox b, int imgW, int imgH)
    {
        int x1 = Math.Clamp(b.X, 0, imgW);
        int y1 = Math.Clamp(b.Y, 0, imgH);
        int x2 = Math.Clamp(b.Right, 0, imgW);
        int y2 = Math.Clamp(b.Bottom, 0, imgH);
        return new Rect(x1, y1, Math.Max(0, x2 - x1), Math.Max(0, y2 - y1));
    }
}
