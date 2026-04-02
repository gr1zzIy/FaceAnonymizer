using Microsoft.ML.OnnxRuntime;

namespace FaceAnonymizer.Core.Abstractions;

public interface IAntiSpoofSessionProvider
{
    InferenceSession Get();
}