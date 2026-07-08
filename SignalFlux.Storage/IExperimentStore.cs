using System.Threading;
using System.Threading.Tasks;

namespace SignalFlux.Storage
{
    /// <summary>Provides read/write access to persisted experiment data.</summary>
    public interface IExperimentStore
    {
        /// <summary>Writes an experiment to the store.</summary>
        /// <param name="experiment">The experiment to persist.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task WriteExperimentAsync(Experiment experiment, CancellationToken cancellationToken = default);
        /// <summary>Reads an experiment from the store by its identifier.</summary>
        /// <param name="id">The experiment identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<Experiment> ReadExperimentAsync(string id, CancellationToken cancellationToken = default);
        /// <summary>Returns true if an experiment with the given identifier exists.</summary>
        Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default);
        /// <summary>Deletes an experiment from the store.</summary>
        Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    }
}
