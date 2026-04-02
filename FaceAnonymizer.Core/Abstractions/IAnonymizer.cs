using FaceAnonymizer.Core.Models;
using OpenCvSharp;

namespace FaceAnonymizer.Core.Abstractions;

public interface IAnonymizer
{
    string Name { get; }

    /// <summary>
    /// Застосовує анонімізацію in-place до кожної області обличчя.
    /// Інтенсивність нормалізована [0..1].
    /// </summary>
    void Apply(Mat bgrImage, IReadOnlyList<FaceBox> faces, float strength);
}
