namespace ARGOS.SharedKernel.Results;

/// <summary>
/// Represents the outcome of an operation that returns a value of type <typeparamref name="T"/>.
/// </summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value)
        : base(true, Error.None)
    {
        _value = value;
    }

    private Result(Error error)
        : base(false, error)
    {
        _value = default;
    }

    /// <summary>
    /// Gets the successful value.
    /// Accessing this property on a failed result throws an exception.
    /// </summary>
    public T Value =>
        IsSuccess
            ? _value!
            : throw new InvalidOperationException("No value exists for a failed result.");

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result<T> Success(T value) => new(value);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public new static Result<T> Failure(Error error) => new(error);

    /// <summary>
    /// Attempts to obtain the contained value.
    /// </summary>
    public bool TryGetValue(out T? value)
    {
        value = IsSuccess
            ? _value
            : default;

        return IsSuccess;
    }

    /// <summary>
    /// Transforms the contained value if the result is successful.
    /// </summary>
    public Result<TResult> Map<TResult>(Func<T, TResult> mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        return IsSuccess
            ? Result<TResult>.Success(mapping(_value!))
            : Result<TResult>.Failure(Error);
    }

    /// <summary>
    /// Chains another result-producing operation.
    /// </summary>
    public Result<TResult> Bind<TResult>(Func<T, Result<TResult>> binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        return IsSuccess
            ? binding(_value!)
            : Result<TResult>.Failure(Error);
    }

    /// <summary>
    /// Ensures the value satisfies a predicate.
    /// </summary>
    public Result<T> Ensure(Func<T, bool> predicate, Error error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);

        if (IsFailure)
            return this;

        return predicate(_value!)
            ? this
            : Failure(error);
    }

    /// <summary>
    /// Executes the appropriate function based on the result state.
    /// </summary>
    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<Error, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return IsSuccess
            ? onSuccess(_value!)
            : onFailure(Error);
    }

    /// <summary>
    /// Executes the appropriate action based on the result state.
    /// </summary>
    public void Match(
        Action<T> onSuccess,
        Action<Error> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        if (IsSuccess)
            onSuccess(_value!);
        else
            onFailure(Error);
    }

    /// <summary>
    /// Allows implicit conversion from a value to a successful result.
    /// </summary>
    public static implicit operator Result<T>(T value)
    {
        return Success(value);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return IsSuccess
            ? $"Success ({_value})"
            : $"Failure ({Error.Code})";
    }
}