using Xunit;

namespace SignalFlux.Tests
{
    public class MetadataTests
    {
        [Fact]
        public void EmptyMetadata_HasNoEntries()
        {
            var m = new Metadata();
            Assert.Empty(m);
        }

        [Fact]
        public void With_AddsEntry()
        {
            var m = new Metadata();
            var updated = m.With("key", "value");

            Assert.Empty(m);
            Assert.Single(updated);
            Assert.Equal("value", updated["key"]);
        }

        [Fact]
        public void ContainsKey_Works()
        {
            var m = new Metadata().With("name", "test");
            Assert.True(m.ContainsKey("name"));
            Assert.False(m.ContainsKey("missing"));
        }

        [Fact]
        public void TryGetValue_Works()
        {
            var m = new Metadata().With("x", 42);
            Assert.True(m.TryGetValue("x", out var val));
            Assert.Equal(42, val);
        }
    }
}
