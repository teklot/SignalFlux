using System;
using System.Collections;
using System.Collections.Generic;

namespace SignalFlux
{
    /// <summary>An immutable-style key-value store for attaching arbitrary metadata to domain objects.</summary>
    public sealed class Metadata : IReadOnlyDictionary<string, object>
    {
        private readonly Dictionary<string, object> _data;

        /// <summary>Creates an empty metadata collection.</summary>
        public Metadata()
        {
            _data = new Dictionary<string, object>();
        }

        /// <summary>Creates a metadata collection populated from the provided source.</summary>
        public Metadata(IReadOnlyDictionary<string, object> source)
        {
            _data = new Dictionary<string, object>();
            if (source != null)
            {
                foreach (var kvp in source)
                    _data[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>Gets the value associated with the specified key.</summary>
        /// <exception cref="KeyNotFoundException">Thrown if the key is not found.</exception>
        public object this[string key] => _data[key];

        /// <summary>The collection of keys.</summary>
        public IEnumerable<string> Keys => ((IReadOnlyDictionary<string, object>)_data).Keys;
        /// <summary>The collection of values.</summary>
        public IEnumerable<object> Values => ((IReadOnlyDictionary<string, object>)_data).Values;
        /// <summary>The number of entries.</summary>
        public int Count => _data.Count;

        /// <summary>Returns a new Metadata instance with the specified key-value pair added, leaving the current instance unchanged.</summary>
        public Metadata With(string key, object value)
        {
            var result = new Metadata(_data);
            result._data[key] = value;
            return result;
        }

        /// <summary>Returns true if the specified key exists.</summary>
        public bool ContainsKey(string key) => _data.ContainsKey(key);

        /// <summary>Attempts to retrieve the value for the specified key.</summary>
        public bool TryGetValue(string key, out object value) =>
            _data.TryGetValue(key, out value);

        /// <summary>Returns an enumerator that iterates through the metadata entries.</summary>
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() =>
            _data.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _data.GetEnumerator();

        /// <summary>Returns a string representation of this metadata collection.</summary>
        public override string ToString() =>
            $"Metadata ({_data.Count} entries)";
    }
}
