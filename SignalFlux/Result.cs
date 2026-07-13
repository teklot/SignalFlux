using System;
using System.Collections.Generic;

namespace SignalFlux
{
    /// <summary>A discriminated union that represents either a successful value or a failure with an error message.</summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    public readonly struct Result<T> : IEquatable<Result<T>>
    {
        /// <summary>True if the operation succeeded.</summary>
        public bool IsSuccess { get; }
        /// <summary>The success value (only valid when <see cref="IsSuccess"/> is true).</summary>
        public T Value { get; }
        /// <summary>A description of the failure (only valid when <see cref="IsSuccess"/> is false).</summary>
        public string Error { get; }
        /// <summary>The optional exception associated with the failure.</summary>
        public Exception Exception { get; }

        private Result(T value)
        {
            IsSuccess = true;
            Value = value;
            Error = null;
            Exception = null;
        }

        private Result(string error, Exception exception = null)
        {
            IsSuccess = false;
            Value = default;
            Error = error ?? throw new ArgumentNullException(nameof(error));
            Exception = exception;
        }

        /// <summary>Creates a successful result wrapping the given value.</summary>
        public static Result<T> Ok(T value) => new Result<T>(value);

        /// <summary>Creates a failed result with an error message and optional exception.</summary>
        public static Result<T> Fail(string error, Exception exception = null) =>
            new Result<T>(error, exception);

        /// <summary>Returns the value if successful; otherwise throws <see cref="InvalidOperationException"/>.</summary>
        public T GetValueOrThrow()
        {
            if (!IsSuccess)
                throw new InvalidOperationException(Error, Exception);
            return Value;
        }

        /// <summary>Returns the value if successful; otherwise returns <paramref name="defaultValue"/>.</summary>
        public T GetValueOrDefault(T defaultValue = default) =>
            IsSuccess ? Value : defaultValue;

        /// <summary>Returns true if this result is equal to another by comparing success, value, and error.</summary>
        /// <param name="other">The other result to compare against.</param>
        public bool Equals(Result<T> other) =>
            IsSuccess == other.IsSuccess &&
            EqualityComparer<T>.Default.Equals(Value, other.Value) &&
            Error == other.Error;

        /// <summary>Returns true if this result is equal to another object.</summary>
        public override bool Equals(object obj) =>
            obj is Result<T> other && Equals(other);

        /// <summary>Returns a hash code for this result.</summary>
        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + IsSuccess.GetHashCode();
            hash = hash * 31 + (Value?.GetHashCode() ?? 0);
            hash = hash * 31 + (Error?.GetHashCode() ?? 0);
            return hash;
        }

        /// <summary>Returns true if two results are equal.</summary>
        public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);
        /// <summary>Returns true if two results are not equal.</summary>
        public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);

        /// <summary>Returns a string representation of this result.</summary>
        public override string ToString() =>
            IsSuccess ? $"Ok({Value})" : $"Fail({Error})";
    }
}
