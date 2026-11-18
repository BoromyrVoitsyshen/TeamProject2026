using System.Collections.Concurrent;

namespace AiChat.Api.Scheduling;

/// <summary>
/// Один замок на майстра: «перевірити, що час вільний» і «створити запис» ідуть по черзі,
/// тож двоє клієнтів не запишуться на той самий час. Різні майстри одне одному не заважають.
/// Працює в межах одного процесу сервера — для кількох копій сервера знадобиться перевірка в самій базі.
/// </summary>
public class BookingLocks
{
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _locks = new();

    public async Task<IDisposable> AcquireAsync(int masterId, CancellationToken ct)
    {
        var gate = _locks.GetOrAdd(masterId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        return new Releaser(gate);
    }

    private sealed class Releaser(SemaphoreSlim gate) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0) gate.Release();
        }
    }
}
