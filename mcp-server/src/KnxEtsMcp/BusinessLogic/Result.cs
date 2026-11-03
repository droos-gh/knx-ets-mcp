namespace KnxEtsMcp.BusinessLogic;

/// <summary>
/// Error returned by the ETS AddIn through the IPC protocol (or raised by the bridge
/// client itself, with code <c>internal</c>). The code is part of the protocol
/// contract (docs/protocol.md), so it is kept as a structured value.
/// </summary>
public sealed record BridgeError(string Code, string Message)
{
    public const string Internal = "internal";

    /// <summary>"[code] message" -- the format the LLM sees in a failed tool call.</summary>
    public override string ToString() => $"[{Code}] {Message}";
}

/// <summary>Railway-style result: a value, or a <see cref="BridgeError"/>.</summary>
public sealed class Result<T>
{
    private Result(bool isSuccess, T value, BridgeError? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public bool IsSuccess { get; }

    /// <summary>Valid when <see cref="IsSuccess"/> is true.</summary>
    public T Value { get; }

    /// <summary>Valid when <see cref="IsSuccess"/> is false.</summary>
    public BridgeError? Error { get; }

    public static Result<T> Ok(T value) => new(true, value, null);

    public static Result<T> Fail(BridgeError error) => new(false, default!, error);

    public static Result<T> Fail(string code, string message) => Fail(new BridgeError(code, message));

    public Result<TOut> Map<TOut>(Func<T, TOut> fn)
        => IsSuccess ? Result<TOut>.Ok(fn(Value)) : Result<TOut>.Fail(Error!);

    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> fn)
        => IsSuccess ? fn(Value) : Result<TOut>.Fail(Error!);
}
