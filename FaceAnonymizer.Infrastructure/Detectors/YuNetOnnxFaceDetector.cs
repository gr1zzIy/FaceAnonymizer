using System.Runtime.InteropServices;
using System.Buffers;
using FaceAnonymizer.Core.Abstractions;
using FaceAnonymizer.Core.Models;
using FaceAnonymizer.Infrastructure.Options;
using FaceAnonymizer.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace FaceAnonymizer.Infrastructure.Detectors;

/// <summary>
/// YuNet ONNX (face_detection_yunet_2023mar.onnx) detector.
/// Notes:
/// - Uses a shared <see cref="InferenceSession"/> (singleton) via <see cref="IOnnxSessionProvider"/>.
/// - Performs letterboxing to 640x640 and decodes model heads similar to OpenCV YuNet.
/// </summary>
public sealed class YuNetOnnxFaceDetector : IFaceDetector
{
    private readonly IOnnxSessionProvider _sessions;
    private readonly IImageCodec _codec;
    private readonly FaceDetectionOptions _o;

    private static readonly int[] Strides = [8, 16, 32];

    public string Name => "yunet";

    public YuNetOnnxFaceDetector(
        IOnnxSessionProvider sessions,
        IImageCodec codec,
        IOptions<FaceDetectionOptions> options)
    {
        _sessions = sessions;
        _codec = codec;
        _o = options.Value;
    }

    public bool IsAvailable(out string reason)
    {
        try
        {
            _ = _sessions.GetYuNetSession();
            reason = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            reason = ex is FileNotFoundException fnf
                ? $"YuNet model not found: {fnf.FileName}"
                : $"Failed to initialize YuNet session: {ex.Message}";
            return false;
        }
    }

    public DetectResult Detect(ReadOnlySpan<byte> imageBytes)
    {
        using var bgr = _codec.DecodeToBgr(imageBytes);
        return DetectMat(bgr);
    }

    public DetectResult DetectMat(Mat bgr)
    {
        var faces = DetectInternal(bgr);
        return new DetectResult(faces);
    }

    private IReadOnlyList<FaceBox> DetectInternal(Mat bgrImage)
    {
        const int NetW = 640;
        const int NetH = 640;

        var session = _sessions.GetYuNetSession();

        int inputW = bgrImage.Width;
        int inputH = bgrImage.Height;

        float scale = Math.Min(NetW / (float)inputW, NetH / (float)inputH);
        int newW = (int)MathF.Round(inputW * scale);
        int newH = (int)MathF.Round(inputH * scale);
        int dx = (NetW - newW) / 2;
        int dy = (NetH - newH) / 2;

        using var resized = new Mat();
        Cv2.Resize(bgrImage, resized, new Size(newW, newH), 0, 0, InterpolationFlags.Linear);

        using var netImg = new Mat(new Size(NetW, NetH), MatType.CV_8UC3, Scalar.All(0));
        resized.CopyTo(new Mat(netImg, new Rect(dx, dy, newW, newH)));

        var inputName = session.InputMetadata.Keys.First();
        var input = NamedOnnxValue.CreateFromTensor(inputName, ToBgrCHWFloatTensor(netImg));
        using var outs = session.Run([input]);

        float[] Get(string name)
        {
            var v = outs.FirstOrDefault(x => x.Name == name)
                    ?? throw new InvalidOperationException(
                        $"Model output '{name}' not found. Outputs: {string.Join(", ", outs.Select(o => o.Name))}");
            return v.AsTensor<float>().ToArray();
        }

        var clsArr = new[] { Get("cls_8"), Get("cls_16"), Get("cls_32") };
        var objArr = new[] { Get("obj_8"), Get("obj_16"), Get("obj_32") };
        var bboxArr = new[] { Get("bbox_8"), Get("bbox_16"), Get("bbox_32") };

        var dets = new List<RawDet>(256);

        for (int i = 0; i < Strides.Length; i++)
        {
            int stride = Strides[i];
            int cols = NetW / stride;
            int rows = NetH / stride;

            var cls = clsArr[i];
            var obj = objArr[i];
            var bbox = bboxArr[i];

            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                int idx = r * cols + c;

                float clsScore = Clamp01(cls[idx]);
                float objScore = Clamp01(obj[idx]);
                float score = MathF.Sqrt(clsScore * objScore);
                if (score < _o.ScoreThreshold) continue;

                float cx = (c + bbox[idx * 4 + 0]) * stride;
                float cy = (r + bbox[idx * 4 + 1]) * stride;
                float w = MathF.Exp(bbox[idx * 4 + 2]) * stride;
                float h = MathF.Exp(bbox[idx * 4 + 3]) * stride;

                float x1 = cx - w / 2f;
                float y1 = cy - h / 2f;

                dets.Add(new RawDet(x1, y1, w, h, score));
            }
        }

        if (dets.Count == 0) return Array.Empty<FaceBox>();

        var keep = Nms(dets, _o.NmsThreshold, _o.TopK);
        var final = new List<FaceBox>(keep.Count);

        foreach (var k in keep)
        {
            var d = dets[k];

            // unletterbox
            float x = (d.X - dx) / scale;
            float y = (d.Y - dy) / scale;
            float w = d.W / scale;
            float h = d.H / scale;

            var rect = ClipRect(x, y, w, h, inputW, inputH);
            if (rect.width <= 0 || rect.height <= 0) continue;

            final.Add(new FaceBox(rect.x, rect.y, rect.width, rect.height, d.Score));
        }

        return final;
    }

    private static DenseTensor<float> ToBgrCHWFloatTensor(Mat bgr)
    {
        int h = bgr.Rows;
        int w = bgr.Cols;

        // Ensure continuous for predictable layout.
        if (!bgr.IsContinuous())
            bgr = bgr.Clone();

        int bytesLen = checked(h * w * 3);
        var pooled = ArrayPool<byte>.Shared.Rent(bytesLen);
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(bgr.Data, pooled, 0, bytesLen);

            var t = new DenseTensor<float>(new[] { 1, 3, h, w });

            // Convert HWC BGR byte -> CHW float.
            // Layout pooled: [B,G,R, B,G,R, ...]
            int idx = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    byte b = pooled[idx++];
                    byte g = pooled[idx++];
                    byte r = pooled[idx++];

                    t[0, 0, y, x] = b;
                    t[0, 1, y, x] = g;
                    t[0, 2, y, x] = r;
                }
            }

            return t;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pooled);
        }
    }

    private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

    private static (int x, int y, int width, int height) ClipRect(float x, float y, float w, float h, int maxW, int maxH)
    {
        int x1 = (int)MathF.Floor(x);
        int y1 = (int)MathF.Floor(y);
        int x2 = (int)MathF.Ceiling(x + w);
        int y2 = (int)MathF.Ceiling(y + h);

        x1 = Math.Clamp(x1, 0, maxW);
        y1 = Math.Clamp(y1, 0, maxH);
        x2 = Math.Clamp(x2, 0, maxW);
        y2 = Math.Clamp(y2, 0, maxH);

        return (x1, y1, x2 - x1, y2 - y1);
    }

    private static List<int> Nms(List<RawDet> dets, float nmsThreshold, int topK)
    {
        var order = dets
            .Select((d, i) => (d.Score, i))
            .OrderByDescending(x => x.Score)
            .Select(x => x.i)
            .ToArray();

        var keep = new List<int>(Math.Min(order.Length, topK));
        var suppressed = new bool[dets.Count];

        for (int oi = 0; oi < order.Length; oi++)
        {
            int i = order[oi];
            if (suppressed[i]) continue;

            keep.Add(i);
            if (keep.Count >= topK) break;

            var di = dets[i];
            for (int oj = oi + 1; oj < order.Length; oj++)
            {
                int k = order[oj];
                if (suppressed[k]) continue;
                if (IoU(di, dets[k]) >= nmsThreshold)
                    suppressed[k] = true;
            }
        }

        return keep;
    }

    private static float IoU(RawDet a, RawDet b)
    {
        float ax2 = a.X + a.W;
        float ay2 = a.Y + a.H;
        float bx2 = b.X + b.W;
        float by2 = b.Y + b.H;

        float ix1 = MathF.Max(a.X, b.X);
        float iy1 = MathF.Max(a.Y, b.Y);
        float ix2 = MathF.Min(ax2, bx2);
        float iy2 = MathF.Min(ay2, by2);

        float iw = MathF.Max(0f, ix2 - ix1);
        float ih = MathF.Max(0f, iy2 - iy1);
        float inter = iw * ih;

        float union = a.W * a.H + b.W * b.H - inter;
        return union <= 0f ? 0f : inter / union;
    }

    private readonly record struct RawDet(float X, float Y, float W, float H, float Score);
}
