using FaceAnonymizer.Core.Abstractions;
using OpenCvSharp;

namespace FaceAnonymizer.Infrastructure.Services;

public sealed class OpenCvImageCodec : IImageCodec
{
    public Mat DecodeToBgr(ReadOnlySpan<byte> imageBytes)
    {
        var arr = imageBytes.ToArray();
        var mat = Cv2.ImDecode(arr, ImreadModes.Color);
        if (mat.Empty())
            throw new ArgumentException("Не вдалося декодувати байти зображення.");
        return mat;
    }

    public byte[] EncodePng(Mat bgr)
    {
        if (bgr.Empty()) throw new ArgumentException("Порожній Mat.");
        return bgr.ImEncode(".png");
    }

    /// <inheritdoc />
    public byte[] Encode(Mat bgr, ImageOutputFormat format)
    {
        if (bgr.Empty()) throw new ArgumentException("Порожній Mat.");

        return format switch
        {
            ImageOutputFormat.Jpeg => bgr.ImEncode(".jpg", new ImageEncodingParam(ImwriteFlags.JpegQuality, 95)),
            _ => bgr.ImEncode(".png")
        };
    }
}
