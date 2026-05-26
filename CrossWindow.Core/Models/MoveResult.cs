namespace CrossWindow.Core.Models;

public sealed record MoveResult
{
    public bool IsSuccess { get; init; }
    public CwRect? NewBounds { get; init; }
    public string? ErrorMessage { get; init; }

    public static MoveResult Ok(CwRect bounds) =>
        new() { IsSuccess = true, NewBounds = bounds };

    public static MoveResult Fail(string reason) =>
        new() { IsSuccess = false, ErrorMessage = reason };

    public override string ToString() =>
        IsSuccess ? $"OK -> {NewBounds}" : $"FAIL: {ErrorMessage}";
}
