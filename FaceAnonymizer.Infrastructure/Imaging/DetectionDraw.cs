using FaceAnonymizer.Core.Abstractions;
using FaceAnonymizer.Core.Models;
using OpenCvSharp;

namespace FaceAnonymizer.Infrastructure.Imaging;

public static class DetectionDraw
{
    /// <summary>Малює рамки облич та кодує в PNG (зворотна сумісність).</summary>
    public static byte[] DrawPng(ReadOnlySpan<byte> imageBytes, IReadOnlyList<FaceBox> faces)
        => Draw(imageBytes, faces, ImageOutputFormat.Png);

    /// <summary>Малює рамки облич та кодує у вказаному форматі.</summary>
    public static byte[] Draw(ReadOnlySpan<byte> imageBytes, IReadOnlyList<FaceBox> faces, ImageOutputFormat format)
    {
        var arr = imageBytes.ToArray();
        using var mat = Cv2.ImDecode(arr, ImreadModes.Color);
        if (mat.Empty()) throw new ArgumentException("Не вдалося декодувати зображення.");

        foreach (var f in faces)
        {
            var rect = new Rect(f.X, f.Y, f.Width, f.Height);
            Cv2.Rectangle(mat, rect, Scalar.LimeGreen, 2);
            if (f.Confidence is > 0 and <= 1.0f)
            {
                Cv2.PutText(mat, f.Confidence.ToString("0.00"), new Point(rect.X, Math.Max(0, rect.Y - 5)),
                    HersheyFonts.HersheySimplex, 0.6, Scalar.LimeGreen, 2);
            }
        }

        return format switch
        {
            ImageOutputFormat.Jpeg => mat.ImEncode(".jpg", new ImageEncodingParam(ImwriteFlags.JpegQuality, 95)),
            _ => mat.ImEncode(".png")
        };
    }
}
