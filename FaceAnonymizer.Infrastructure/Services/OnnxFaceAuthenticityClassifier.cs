using System.Buffers;
using FaceAnonymizer.Core.Abstractions;
using FaceAnonymizer.Core.Models;
using FaceAnonymizer.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using Microsoft.Extensions.Logging;

namespace FaceAnonymizer.Infrastructure.Services;

public sealed class OnnxFaceAuthenticityClassifier : IFaceAuthenticityClassifier
{
    public string Name => "anti-spoof-onnx";

    private readonly IAntiSpoofSessionProvider _sessions;
    private readonly AntiSpoofOptions _opt;

    private readonly string _inputName;
    private readonly string _outputName;

    private readonly ILogger<OnnxFaceAuthenticityClassifier> _log;
    
    public OnnxFaceAuthenticityClassifier(
        IAntiSpoofSessionProvider sessions,
        IOptions<AntiSpoofOptions> opt,
        ILogger<OnnxFaceAuthenticityClassifier> log)
    {
        _sessions = sessions;
        _opt = opt.Value;
        _log = log;

        var s = _sessions.Get();
        _inputName = s.InputMetadata.Keys.First();
        _outputName = s.OutputMetadata.Keys.First();
    }

    public AuthenticityResult Classify(Mat faceBgr)
    {
        var session = _sessions.Get();

        // 1) normalize input to 80x80 RGB float32
        using var resized = new Mat();
        Cv2.Resize(faceBgr, resized, new Size(80, 80), 0, 0, InterpolationFlags.Area);

        using var rgb = new Mat();
        Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

        using var f32 = new Mat();
        rgb.ConvertTo(f32, MatType.CV_32FC3, 1.0 / 255.0);

        // 2) CHW packing: [1,3,80,80]
        const int H = 80, W = 80, C = 3;
        var inputData = new float[C * H * W]; // 19200
        int hw = H * W;

        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                Vec3f v = f32.At<Vec3f>(y, x); // RGB
                int idx = y * W + x;

                inputData[0 * hw + idx] = v.Item0; // R
                inputData[1 * hw + idx] = v.Item1; // G
                inputData[2 * hw + idx] = v.Item2; // B
            }
        }

        var inputTensor = new DenseTensor<float>(inputData, new[] { 1, 3, 80, 80 });

        // 3) run ONNX
        var inputs = new List<NamedOnnxValue>(1)
        {
            NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)
        };

        using var outputs = session.Run(inputs);

        // якщо хочеш саме по імені output:
        var outTensor = outputs.First(o => o.Name == _outputName).AsTensor<float>(); // [1,3]

        // 4) softmax -> probabilities
        float p0, p1, p2;
        Softmax3(outTensor[0, 0], outTensor[0, 1], outTensor[0, 2], out p0, out p1, out p2);

        // Поки вважаємо "real" = p0, але ти це підтвердиш логами на real vs mask
        var probs = new[] { p0, p1, p2 };
        int realIdx = Math.Clamp(_opt.RealClassIndex, 0, 2);
        float realScore = probs[realIdx];

        _log.LogInformation("[AntiSpoof] probs: p0={P0:0.000} p1={P1:0.000} p2={P2:0.000} => real={Real:0.000}", p0, p1, p2, realScore);

        
        var label = realScore >= _opt.RealThreshold ? FaceAuthenticity.Real : FaceAuthenticity.Spoof;
        return new AuthenticityResult(label, realScore);
    }


    private static void Softmax3(float a, float b, float c, out float p0, out float p1, out float p2)
    {
        float m = MathF.Max(a, MathF.Max(b, c));
        float ea = MathF.Exp(a - m);
        float eb = MathF.Exp(b - m);
        float ec = MathF.Exp(c - m);
        float s = ea + eb + ec;
        p0 = ea / s;
        p1 = eb / s;
        p2 = ec / s;
    }

    private static float ExtractRealScore(Tensor<float> output)
    {
        var dims = output.Dimensions.ToArray();

        // [N, C]
        if (dims.Length == 2 && dims[1] >= 2)
        {
            // беремо перший елемент батча
            int c = dims[1];

            // softmax по C
            float max = float.NegativeInfinity;
            for (int i = 0; i < c; i++)
                max = MathF.Max(max, output[0, i]);

            float sum = 0f;
            Span<float> exps = c <= 16 ? stackalloc float[c] : new float[c];

            for (int i = 0; i < c; i++)
            {
                float e = MathF.Exp(output[0, i] - max);
                exps[i] = e;
                sum += e;
            }

            // у більшості таких моделей "real" = індекс 0
            return exps[0] / sum;
        }

        // [N] або [1] -> sigmoid
        if ((dims.Length == 1 && dims[0] == 1) || (dims.Length == 2 && dims[1] == 1))
        {
            float x = dims.Length == 2 ? output[0, 0] : output[0];
            return 1f / (1f + MathF.Exp(-x));
        }

        // fallback
        float first = output.ToArray()[0];
        return 1f / (1f + MathF.Exp(-first));
    }
}
