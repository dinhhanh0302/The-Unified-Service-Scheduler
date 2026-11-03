namespace Scheduler.Domain.Results;

/// <summary>Outcome of an operation that returns no value (SPEC §11: expected failures are results, not exceptions).</summary>
public class Result
{
    protected Result(DomainError? error) => Error = error;

    public DomainError? Error { get; }

    public bool IsSuccess => Error is null;

    public bool IsFailure => !IsSuccess;

    public static Result Success() => new(null);

    public static Result Failure(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(error);
    }

    public static implicit operator Result(DomainError error) => Failure(error);
}

/// <summary>Outcome of an operation that returns a value on success.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(null) => _value = value;

    private Result(DomainError error) : base(error)
    {
    }

    /// <summary>The value; only valid when <see cref="Result.IsSuccess"/> is true.</summary>
    public T Value => IsSuccess
        // Null-forgiving is safe: a success result is only created through the constructor taking a T.
        ? _value!
        : throw new InvalidOperationException($"Cannot read the value of a failed result ({Error?.Code}).");

    public static Result<T> Success(T value) => new(value);

    public static new Result<T> Failure(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(error);
    }

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(DomainError error) => Failure(error);
}
