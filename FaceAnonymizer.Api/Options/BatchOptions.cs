namespace FaceAnonymizer.Api.Options;

public sealed class BatchOptions
{
    public string InputRoot { get; set; } = "batch-input";
    public string OutputRoot { get; set; } = "batch-output";
}
