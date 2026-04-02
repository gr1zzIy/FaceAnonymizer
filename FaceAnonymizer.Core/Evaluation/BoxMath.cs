using FaceAnonymizer.Core.Models;

namespace FaceAnonymizer.Core.Evaluation;

public static class BoxMath
{
    public static double IoU(in FaceBox a, in FaceBox b)
    {
        int ax2 = a.Right;
        int ay2 = a.Bottom;
        int bx2 = b.Right;
        int by2 = b.Bottom;

        int ix1 = Math.Max(a.X, b.X);
        int iy1 = Math.Max(a.Y, b.Y);
        int ix2 = Math.Min(ax2, bx2);
        int iy2 = Math.Min(ay2, by2);

        int iw = Math.Max(0, ix2 - ix1);
        int ih = Math.Max(0, iy2 - iy1);
        long inter = (long)iw * ih;

        long union = (long)a.Width * a.Height + (long)b.Width * b.Height - inter;
        if (union <= 0) return 0;
        return inter / (double)union;
    }

    public static FaceBox ClipToImage(in FaceBox box, int imgW, int imgH)
    {
        int x1 = Math.Clamp(box.X, 0, imgW);
        int y1 = Math.Clamp(box.Y, 0, imgH);
        int x2 = Math.Clamp(box.Right, 0, imgW);
        int y2 = Math.Clamp(box.Bottom, 0, imgH);
        return new FaceBox(x1, y1, Math.Max(0, x2 - x1), Math.Max(0, y2 - y1), box.Confidence);
    }
}
