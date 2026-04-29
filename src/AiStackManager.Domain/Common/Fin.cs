namespace AiStackManager.Domain.Common;

public sealed record AiStackError(string Code, string Message)
{
    public static AiStackError Validation(string message) => new("validation", message);
    public static AiStackError Conflict(string message) => new("conflict", message);
    public static AiStackError ExternalTool(string message) => new("external_tool", message);
}

public readonly record struct Fin<T>
{
    private readonly T? _value;
    public bool IsSucc { get; }
    public bool IsFail => !IsSucc;
    public T Value => IsSucc ? _value! : throw new InvalidOperationException("No success value is available.");
    public AiStackError Error { get; }

    private Fin(T value) { IsSucc = true; _value = value; Error = default!; }
    private Fin(AiStackError error) { IsSucc = false; _value = default; Error = error; }

    public static Fin<T> Succ(T value) => new(value);
    public static Fin<T> Fail(AiStackError error) => new(error);
}

public readonly record struct Fin
{
    public bool IsSucc { get; }
    public bool IsFail => !IsSucc;
    public AiStackError Error { get; }

    private Fin(bool success, AiStackError error) { IsSucc = success; Error = error; }
    public static Fin Succ() => new(true, default!);
    public static Fin Fail(AiStackError error) => new(false, error);
}
