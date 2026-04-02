namespace FaceAnonymizer.Core.Models;

/// <summary>
/// Обмежувальна рамка обличчя в піксельних координатах зображення.
/// </summary>
public readonly record struct FaceBox(int X, int Y, int Width, int Height, float Confidence = 1.0f)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;
}
