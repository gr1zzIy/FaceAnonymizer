using FaceAnonymizer.Core.Models;

namespace FaceAnonymizer.Core.Abstractions;

public interface IBatchReportGenerator
{
    byte[] GeneratePdf(BatchRunSummary summary, IReadOnlyList<BatchFileRow> rows);
}