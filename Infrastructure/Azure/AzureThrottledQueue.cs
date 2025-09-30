
using System.Threading.Channels;

namespace Infrastructure.Azure;

public sealed class AzureThrottledQueue<T> : IAsyncDisposable
{
    private readonly Channel<T> _channel = Channel.CreateUnbounded<T>();
    private readonly TimeSpan _minInterval;
    private readonly Func<T, CancellationToken, Task> _handler;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _runner;

    public AzureThrottledQueue(TimeSpan minInterval, Func<T, CancellationToken, Task> handler)
    {
        _minInterval = minInterval;
        _handler = handler;
        _runner = Task.Run(RunAsync);
    }

    public async Task EnqueueAsync(T item) => await _channel.Writer.WriteAsync(item);

    private async Task RunAsync()
    {
        var reader = _channel.Reader;
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var hasItem = await reader.WaitToReadAsync(_cts.Token);
                if (!hasItem) { await Task.Delay(50, _cts.Token); continue; }
                if (reader.TryRead(out var item))
                {
                    await _handler(item, _cts.Token);
                    await Task.Delay(_minInterval, _cts.Token);
                }
            }
            catch (OperationCanceledException) { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { await _runner; } catch {}
        _cts.Dispose();
    }
}
