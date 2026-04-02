using FaceAnonymizer.Core.Evaluation;
using FaceAnonymizer.Core.Models;
using OpenCvSharp;

namespace FaceAnonymizer.Application.Evaluation;

public static class MetricsCalculator
{
    /// <summary>
    /// Порівнює <paramref name="baseline"/> (еталон) з <paramref name="candidate"/> (прогноз).
    /// Жадібне зіставлення за IoU.
    /// </summary>
    public static EvaluationMetrics CompareDetections(
        IReadOnlyList<FaceBox> baseline,
        IReadOnlyList<FaceBox> candidate,
        double iouThreshold)
    {
        if (baseline.Count == 0 && candidate.Count == 0)
            return new EvaluationMetrics(iouThreshold, 0, 0, 0, 1, 1, 1, 1);

        if (baseline.Count == 0)
            return new EvaluationMetrics(iouThreshold, 0, candidate.Count, 0, 0, 1, 0, 0);

        if (candidate.Count == 0)
            return new EvaluationMetrics(iouThreshold, 0, 0, baseline.Count, 1, 0, 0, 0);

        var usedCandidate = new bool[candidate.Count];
        int tp = 0;
        double iouSum = 0;

        // For each GT box find best unused prediction.
        for (int i = 0; i < baseline.Count; i++)
        {
            double bestIou = 0;
            int bestJ = -1;

            for (int j = 0; j < candidate.Count; j++)
            {
                if (usedCandidate[j]) continue;
                double iou = BoxMath.IoU(baseline[i], candidate[j]);
                if (iou > bestIou)
                {
                    bestIou = iou;
                    bestJ = j;
                }
            }

            if (bestJ >= 0 && bestIou >= iouThreshold)
            {
                usedCandidate[bestJ] = true;
                tp++;
                iouSum += bestIou;
            }
        }

        int fp = candidate.Count - tp;
        int fn = baseline.Count - tp;

        double precision = tp + fp == 0 ? 0 : tp / (double)(tp + fp);
        double recall = tp + fn == 0 ? 0 : tp / (double)(tp + fn);
        double f1 = precision + recall == 0 ? 0 : 2 * precision * recall / (precision + recall);
        double meanIou = tp == 0 ? 0 : iouSum / tp;

        return new EvaluationMetrics(iouThreshold, tp, fp, fn, precision, recall, f1, meanIou);
    }

    /// <summary>
    /// Пікове співвідношення сигнал/шум між оригінальним та анонімізованим зображеннями.
    /// Вищий PSNR → менше спотворень. Типовий діапазон: 20–50 дБ.
    /// </summary>
    public static double CalculatePsnr(Mat original, Mat anonymized)
    {
        if (original.Empty() || anonymized.Empty())
            return 0;

        double psnr = Cv2.PSNR(original, anonymized);
        return double.IsInfinity(psnr) ? 100.0 : psnr;
    }

    /// <summary>
    /// Індекс структурної подібності (SSIM) між оригінальним та анонімізованим зображеннями.
    /// Повертає значення [0, 1]. Вище → більше подібність (менше анонімізації видно).
    /// Wang et al., 2004 — «Image Quality Assessment: From Error Visibility to Structural Similarity».
    /// </summary>
    public static double CalculateSsim(Mat original, Mat anonymized)
    {
        if (original.Empty() || anonymized.Empty())
            return 0;

        const double C1 = 6.5025;   // (0.01 * 255)^2
        const double C2 = 58.5225;  // (0.03 * 255)^2

        using var img1 = new Mat();
        using var img2 = new Mat();
        original.ConvertTo(img1, MatType.CV_32F);
        anonymized.ConvertTo(img2, MatType.CV_32F);

        using var mu1 = new Mat();
        using var mu2 = new Mat();
        Cv2.GaussianBlur(img1, mu1, new Size(11, 11), 1.5);
        Cv2.GaussianBlur(img2, mu2, new Size(11, 11), 1.5);

        using var mu1Sq = new Mat();
        using var mu2Sq = new Mat();
        using var mu1Mu2 = new Mat();
        Cv2.Multiply(mu1, mu1, mu1Sq);
        Cv2.Multiply(mu2, mu2, mu2Sq);
        Cv2.Multiply(mu1, mu2, mu1Mu2);

        using var sigma1Sq = new Mat();
        using var sigma2Sq = new Mat();
        using var sigma12 = new Mat();

        using var img1Sq = new Mat();
        using var img2Sq = new Mat();
        using var img1Img2 = new Mat();
        Cv2.Multiply(img1, img1, img1Sq);
        Cv2.Multiply(img2, img2, img2Sq);
        Cv2.Multiply(img1, img2, img1Img2);

        Cv2.GaussianBlur(img1Sq, sigma1Sq, new Size(11, 11), 1.5);
        Cv2.GaussianBlur(img2Sq, sigma2Sq, new Size(11, 11), 1.5);
        Cv2.GaussianBlur(img1Img2, sigma12, new Size(11, 11), 1.5);

        Cv2.Subtract(sigma1Sq, mu1Sq, sigma1Sq);
        Cv2.Subtract(sigma2Sq, mu2Sq, sigma2Sq);
        Cv2.Subtract(sigma12, mu1Mu2, sigma12);

        // SSIM formula: ((2*mu1*mu2 + C1) * (2*sigma12 + C2)) / ((mu1^2 + mu2^2 + C1) * (sigma1^2 + sigma2^2 + C2))
        using var t1 = new Mat();
        using var t2 = new Mat();
        using var t3 = new Mat();

        Cv2.Multiply(mu1Mu2, new Scalar(2, 2, 2), t1);
        Cv2.Add(t1, new Scalar(C1, C1, C1), t1);

        Cv2.Multiply(sigma12, new Scalar(2, 2, 2), t2);
        Cv2.Add(t2, new Scalar(C2, C2, C2), t2);

        Cv2.Multiply(t1, t2, t1); // numerator

        Cv2.Add(mu1Sq, mu2Sq, t3);
        Cv2.Add(t3, new Scalar(C1, C1, C1), t3);

        using var t4 = new Mat();
        Cv2.Add(sigma1Sq, sigma2Sq, t4);
        Cv2.Add(t4, new Scalar(C2, C2, C2), t4);

        Cv2.Multiply(t3, t4, t3); // denominator

        using var ssimMap = new Mat();
        Cv2.Divide(t1, t3, ssimMap);

        Scalar mssim = Cv2.Mean(ssimMap);
        return (mssim.Val0 + mssim.Val1 + mssim.Val2) / 3.0;
    }
}
