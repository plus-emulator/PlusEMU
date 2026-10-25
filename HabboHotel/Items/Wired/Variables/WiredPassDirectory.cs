namespace Plus.HabboHotel.Items.Wired.Variables;

/// <summary>While a Wired pass is open, each variable definition and room owner is read from the inner directory once.</summary>
public sealed class WiredPassDirectory(IWiredVariableDirectory inner) : IWiredVariableDirectory
{
    private readonly object _gate = new();
    private Dictionary<uint, WiredVariableDefinition?>? _definitions;
    private Dictionary<uint, uint?>? _owners;
    private int _depth;

    public WiredVariableDefinition? Find(uint itemId) => Remembered(ref _definitions, itemId, inner.Find);
    public uint? GetRoomOwner(uint roomId) => Remembered(ref _owners, roomId, inner.GetRoomOwner);
    public IReadOnlyList<WiredSharedVariable> ListShared(uint ownerId, uint exceptRoomId) => inner.ListShared(ownerId, exceptRoomId);
    public IReadOnlyList<WiredVariableDefinition> ListRoom(uint roomId) => inner.ListRoom(roomId);
    public IReadOnlyList<WiredVariableDefinition> ListReferences(uint sourceRoomId, uint sourceItemId) => inner.ListReferences(sourceRoomId, sourceItemId);

    public IDisposable OpenPass()
    {
        lock (_gate) {
            if (_depth++ == 0) {
                _definitions = [];
                _owners = [];
            }
        }

        return new Pass(this);
    }

    private void ClosePass()
    {
        lock (_gate) {
            if (--_depth == 0) {
                _definitions = null;
                _owners = null;
            }
        }
    }

    // The database is read outside the lock, so one slow query does not hold up other callers.
    private T Remembered<T>(ref Dictionary<uint, T>? memo, uint id, Func<uint, T> read)
    {
        Dictionary<uint, T>? pass;

        lock (_gate) {
            pass = memo;

            if (pass is not null && pass.TryGetValue(id, out var remembered)) {
                return remembered;
            }
        }

        var value = read(id);

        if (pass is not null) {
            lock (_gate) {
                pass.TryAdd(id, value);
            }
        }

        return value;
    }

    private sealed class Pass(WiredPassDirectory owner) : IDisposable
    {
        private int _closed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _closed, 1) == 0) {
                owner.ClosePass();
            }
        }
    }
}
