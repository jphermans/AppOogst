namespace AppOogst.Services;

/// <summary>
/// Queue with the proven Python behavior: one action at a time; actions
/// clicked while another action is running are queued (max 3)
/// and run automatically once the current action finishes.
/// </summary>
public sealed class ActionQueue
{
    private readonly Queue<Func<Task>> _pending = new();
    private bool _busy;
    private readonly object _lock = new();

    public const int MaxPending = 3;
    public int PendingCount { get { lock (_lock) return _pending.Count; } }

    public event Action<bool>? BusyChanged;
    public event Action<string>? StatusChanged;

    /// <returns>true = accepted (direct or queued); false = queue full.</returns>
    public bool Enqueue(Func<Task> work, string busyStatus)
    {
        lock (_lock)
        {
            if (_busy && _pending.Count >= MaxPending)
            {
                StatusChanged?.Invoke("queue full; please wait");
                return false;
            }

            if (_busy)
                StatusChanged?.Invoke("action queued ...");

            _pending.Enqueue(async () =>
            {
                StatusChanged?.Invoke(busyStatus);
                await work();
            });
        }
        Pump();
        return true;
    }

    private void Pump()
    {
        Func<Task>? work = null;
        lock (_lock)
        {
            if (_busy)
                return;
            if (_pending.Count == 0)
            {
                // Deliberately no "ready" status here: it used to overwrite the
                // message the finished action had just set, so a failure like
                // "purchases failed: …" was replaced by "ready" a tick later.
                BusyChanged?.Invoke(false);
                return;
            }
            _busy = true;
            work = _pending.Dequeue();
        }

        BusyChanged?.Invoke(true);
        _ = Task.Run(async () =>
        {
            try
            {
                await work();
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke("fout: " + ex.Message);
            }
            finally
            {
                lock (_lock) _busy = false;
                Pump();
            }
        });
    }
}
