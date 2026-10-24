using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;

namespace RealFenixFailures.Application.Session;

public sealed record PreparedFailureItem(
    PresetFailureDefinition Definition,
    string FenixFailureId,
    string Description,
    string PresetName,
    FailureSeverity Severity,
    bool IsGround,
    ComplexFlightPhaseEnum PresetPhase,
    DateTime PreparedAtUtc);

public sealed class PreparedFailureQueue {
    private readonly int _limit;
    private readonly object _sync = new();
    private readonly Queue<PreparedFailureItem> _items = new();
    private int _count;

    public PreparedFailureQueue(int limit) {
        _limit = Math.Max(1, limit);
    }

    public event EventHandler? Changed;

    public int Count {
        get {
            lock (_sync) return _count;
        }
    }

    public bool HasGround {
        get {
            lock (_sync) return _items.Any(x => x.IsGround);
        }
    }

    public bool TryEnqueue(PreparedFailureItem item) {
        lock (_sync) {
            if (_count >= _limit) return false;
            _items.Enqueue(item);
            _count++;
        }
        OnChanged();
        return true;
    }

    public IReadOnlyList<PreparedFailureItem> TakeGround() {
        List<PreparedFailureItem> taken;
        lock (_sync) {
            taken = _items.Where(x => x.IsGround).ToList();
            if (taken.Count > 0) {
                var remaining = _items.Where(x => !x.IsGround).ToList();
                _items.Clear();
                foreach (var item in remaining) _items.Enqueue(item);
                _count = remaining.Count;
            }
        }
        if (taken.Count > 0) OnChanged();
        return taken;
    }

    public PreparedFailureItem? TryTakeFirstGround() {
        PreparedFailureItem? taken = null;
        lock (_sync) {
            var list = _items.ToList();
            var index = list.FindIndex(x => x.IsGround);
            if (index >= 0) {
                taken = list[index];
                list.RemoveAt(index);
                _items.Clear();
                foreach (var item in list) _items.Enqueue(item);
                _count = list.Count;
            }
        }
        if (taken is not null) OnChanged();
        return taken;
    }

    public PreparedFailureItem? TryTakeFirstAirborne() {
        PreparedFailureItem? taken = null;
        lock (_sync) {
            var list = _items.ToList();
            var index = list.FindIndex(x => !x.IsGround);
            if (index >= 0) {
                taken = list[index];
                list.RemoveAt(index);
                _items.Clear();
                foreach (var item in list) _items.Enqueue(item);
                _count = list.Count;
            }
        }
        if (taken is not null) OnChanged();
        return taken;
    }

    public void RequeueFront(PreparedFailureItem item) {
        lock (_sync) {
            if (_count >= _limit) return;
            var list = _items.ToList();
            list.Insert(0, item);
            _items.Clear();
            foreach (var i in list) _items.Enqueue(i);
            _count = list.Count;
        }
        OnChanged();
    }

    public int DiscardAll() {
        int cleared;
        lock (_sync) {
            cleared = _count;
            _items.Clear();
            _count = 0;
        }
        if (cleared > 0) OnChanged();
        return cleared;
    }

    public int DiscardGround() {
        int cleared;
        lock (_sync) {
            cleared = _items.Count(x => x.IsGround);
            if (cleared > 0) {
                var remaining = _items.Where(x => !x.IsGround).ToList();
                _items.Clear();
                foreach (var item in remaining) _items.Enqueue(item);
                _count = remaining.Count;
            }
        }
        if (cleared > 0) OnChanged();
        return cleared;
    }

    public IReadOnlyList<PreparedFailureItem> Snapshot() {
        lock (_sync) return _items.ToList();
    }

    private void OnChanged() {
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
