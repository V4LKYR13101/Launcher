namespace LauncherApp.Services;

internal static class ActiveDownloads
{
    internal sealed record Status(long Id, string Name, long Bytes, long? Total)
    {
        internal int? Percent => Total is > 0 ? Math.Clamp((int)(Bytes * 100d / Total.Value), 0, 100) : null;
    }

    private static readonly object sync = new();
    private static readonly Dictionary<long, Transfer> active = new();
    private static long nextId;
    internal static event Action? Changed;

    internal static Transfer Start(string name, long? total = null)
    {
        Transfer transfer;
        lock (sync)
        {
            transfer = new Transfer(new(++nextId, name, 0, total));
            active.Add(transfer.Current.Id, transfer);
        }
        Changed?.Invoke();
        return transfer;
    }

    internal static Status[] Snapshot()
    {
        lock (sync)
            return active.Values.Select(transfer => transfer.Current).OrderBy(item => item.Id).ToArray();
    }

    internal sealed class Transfer(Status initial) : IDisposable
    {
        private Status current = initial;
        internal Status Current => Volatile.Read(ref current);

        internal void Name(string name)
        {
            lock (sync)
                current = current with { Name = name };
        }

        internal void Report(long bytes, long? total)
        {
            lock (sync)
                current = current with { Bytes = bytes == 0 ? 0 : Math.Max(current.Bytes, bytes), Total = total };
            Changed?.Invoke();
        }

        public void Dispose()
        {
            lock (sync)
                if (!active.Remove(current.Id)) return;
            Changed?.Invoke();
        }
    }
}
