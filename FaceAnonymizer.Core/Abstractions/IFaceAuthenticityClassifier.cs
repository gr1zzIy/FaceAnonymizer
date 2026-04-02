using FaceAnonymizer.Core.Models;
using OpenCvSharp;

namespace FaceAnonymizer.Core.Abstractions;

public interface IFaceAuthenticityClassifier
{
    string Name { get; }
    AuthenticityResult Classify(Mat faceBgr);
}