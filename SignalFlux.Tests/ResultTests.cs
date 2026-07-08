using System;
using Xunit;

namespace SignalFlux.Tests
{
    public class ResultTests
    {
        [Fact]
        public void Ok_CreatesSuccessfulResult()
        {
            var result = Result<int>.Ok(42);

            Assert.True(result.IsSuccess);
            Assert.Equal(42, result.Value);
        }

        [Fact]
        public void Fail_CreatesFailedResult()
        {
            var result = Result<int>.Fail("Something went wrong");

            Assert.False(result.IsSuccess);
            Assert.Equal("Something went wrong", result.Error);
        }

        [Fact]
        public void GetValueOrThrow_OnSuccess_ReturnsValue()
        {
            var result = Result<int>.Ok(42);

            Assert.Equal(42, result.GetValueOrThrow());
        }

        [Fact]
        public void GetValueOrThrow_OnFailure_Throws()
        {
            var result = Result<int>.Fail("error");

            Assert.Throws<InvalidOperationException>(() => result.GetValueOrThrow());
        }

        [Fact]
        public void GetValueOrDefault_OnFailure_ReturnsDefault()
        {
            var result = Result<int>.Fail("error");

            Assert.Equal(0, result.GetValueOrDefault());
            Assert.Equal(-1, result.GetValueOrDefault(-1));
        }
    }
}
