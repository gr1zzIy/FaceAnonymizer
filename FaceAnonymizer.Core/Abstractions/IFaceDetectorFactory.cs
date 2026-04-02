namespace FaceAnonymizer.Core.Abstractions;

public interface IFaceDetectorFactory
{
    IFaceDetector Create(string engine);
    IReadOnlyCollection<string> GetSupportedEngines();
}
