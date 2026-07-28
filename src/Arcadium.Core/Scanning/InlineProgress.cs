namespace Arcadium.Core.Scanning;

/// <summary>
/// An <see cref="IProgress{T}"/> that invokes the handler synchronously on the reporting thread.
/// The BCL <see cref="Progress{T}"/> posts to the captured SynchronizationContext, which can
/// reorder events; scan hosts should use this class instead.
/// </summary>
public sealed class InlineProgress<T> : IProgress<T>
{
    private readonly Action<T> _handler;

    public InlineProgress(Action<T> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        _handler = handler;
    }

    public void Report(T value) => _handler(value);
}
