using System.Threading;
using System.Threading.Tasks;

namespace SignalFlux.Storage
{
    /// <summary>Provides read/write access to persisted signal data.</summary>
    public interface ISignalStore
    {
        /// <summary>Writes a signal to the store.</summary>
        /// <param name="signal">The signal to persist.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task WriteSignalAsync<T>(Signal<T> signal, CancellationToken cancellationToken = default);
        /// <summary>Reads a signal from the store by its source identifier.</summary>
        /// <param name="source">The source identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<Signal<T>> ReadSignalAsync<T>(string source, CancellationToken cancellationToken = default);
        /// <summary>Returns true if a signal with the given source identifier exists.</summary>
        Task<bool> ExistsAsync(string source, CancellationToken cancellationToken = default);
        /// <summary>Deletes a signal from the store.</summary>
        Task DeleteAsync(string source, CancellationToken cancellationToken = default);
    }
}
