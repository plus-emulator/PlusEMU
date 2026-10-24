namespace Plus.HabboHotel.Items.Wired.Configuration;

// The insert path of java.util.HashMap with Long keys, kept only for its iteration order. A bin turns into a red-black tree at its ninth
// id once the table has 64 buckets (a smaller table doubles instead). The tree root moves to the front of the bin, a new tree node goes
// right after its parent, and tree order compares the Long hash, then the value. A save holds at most 20 ids, so a tree never splits.
internal static class JavaHashSet
{
    private const int TreeifyThreshold = 8, MinTreeifyCapacity = 64;

    private sealed class Node(int hash, long key)
    {
        public readonly int Hash = hash;
        public readonly long Key = key;
        public Node? Next, Prev, Parent, Left, Right;
        public bool Tree, Red;
    }

    public static int[] Order(IEnumerable<int> ids, int initialCapacity)
    {
        var capacity = 1;

        while (capacity < initialCapacity) {
            capacity <<= 1;
        }

        var table = new Node?[capacity];
        var threshold = (int)(capacity * 0.75f);
        var size = 0;

        foreach (var id in ids) {
            long key = id;
            var h = (int)(key ^ (long)((ulong)key >> 32));
            var hash = h ^ (int)((uint)h >> 16);
            var index = hash & (table.Length - 1);
            var p = table[index];

            if (p == null) {
                table[index] = new(hash, key);
            }
            else if (p.Hash == hash && p.Key == key) {
                continue;
            }
            else if (p.Tree) {
                if (!PutTreeVal(table, p, hash, key)) {
                    continue;
                }
            }
            else {
                var duplicate = false;

                for (var count = 0; ; count++) {
                    var e = p.Next;

                    if (e == null) {
                        p.Next = new(hash, key);

                        if (count >= TreeifyThreshold - 1) {
                            table = TreeifyBin(table, hash, ref threshold);
                        }

                        break;
                    }

                    if (e.Hash == hash && e.Key == key) {
                        duplicate = true;

                        break;
                    }

                    p = e;
                }

                if (duplicate) {
                    continue;
                }
            }

            if (++size > threshold) {
                table = Resize(table, ref threshold);
            }
        }

        var order = new List<int>(size);

        foreach (var head in table) {
            for (var e = head; e != null; e = e.Next) {
                order.Add((int)e.Key);
            }
        }

        return [.. order];
    }

    private static int Compare(Node x, Node p) => p.Hash > x.Hash ? -1 : p.Hash < x.Hash ? 1 : x.Key.CompareTo(p.Key);

    private static Node?[] TreeifyBin(Node?[] table, int hash, ref int threshold)
    {
        if (table.Length < MinTreeifyCapacity) {
            return Resize(table, ref threshold);
        }

        Node? previous = null;

        for (var e = table[hash & (table.Length - 1)]; e != null; e = e.Next) {
            e.Tree = true;
            e.Prev = previous;
            previous = e;
        }

        Treeify(table, table[hash & (table.Length - 1)]!);

        return table;
    }

    private static void Treeify(Node?[] table, Node head)
    {
        Node? root = null;

        for (Node? x = head, next; x != null; x = next) {
            next = x.Next;
            x.Left = x.Right = null;

            if (root == null) {
                x.Parent = null;
                x.Red = false;
                root = x;

                continue;
            }

            for (var p = root; ;) {
                var dir = Compare(x, p);
                var xp = p;

                if ((p = dir <= 0 ? p.Left : p.Right) == null) {
                    x.Parent = xp;

                    if (dir <= 0) {
                        xp.Left = x;
                    }
                    else {
                        xp.Right = x;
                    }

                    root = BalanceInsertion(root, x);

                    break;
                }
            }
        }

        MoveRootToFront(table, root!);
    }

    private static bool PutTreeVal(Node?[] table, Node first, int hash, long key)
    {
        var root = first;

        while (root.Parent != null) {
            root = root.Parent;
        }

        var probe = new Node(hash, key);

        for (var p = root; ;) {
            if (p.Hash == hash && p.Key == key) {
                return false;
            }

            var dir = Compare(probe, p);
            var xp = p;

            if ((p = dir <= 0 ? p.Left! : p.Right!) == null) {
                var xpn = xp.Next;
                var x = new Node(hash, key) { Tree = true, Next = xpn, Parent = xp, Prev = xp };

                if (dir <= 0) {
                    xp.Left = x;
                }
                else {
                    xp.Right = x;
                }

                xp.Next = x;

                if (xpn != null) {
                    xpn.Prev = x;
                }

                MoveRootToFront(table, BalanceInsertion(root, x));

                return true;
            }
        }
    }

    private static void MoveRootToFront(Node?[] table, Node root)
    {
        var index = root.Hash & (table.Length - 1);
        var first = table[index];

        if (ReferenceEquals(root, first)) {
            return;
        }

        table[index] = root;
        var rp = root.Prev;
        var rn = root.Next;

        if (rn != null) {
            rn.Prev = rp;
        }

        if (rp != null) {
            rp.Next = rn;
        }

        if (first != null) {
            first.Prev = root;
        }

        root.Next = first;
        root.Prev = null;
    }

    private static Node?[] Resize(Node?[] old, ref int threshold)
    {
        var bit = old.Length;
        var table = new Node?[bit * 2];
        threshold = (int)(table.Length * 0.75f);

        for (var j = 0; j < old.Length; j++) {
            var e = old[j];

            if (e == null) {
                continue;
            }

            if (e.Next == null) {
                table[e.Hash & (table.Length - 1)] = e;

                continue;
            }

            Node? lo = null, loTail = null, hi = null, hiTail = null;

            for (Node? next; e != null; e = next) {
                next = e.Next;
                e.Next = null;

                if ((e.Hash & bit) == 0) {
                    if (loTail == null) {
                        lo = e;
                    }
                    else {
                        loTail.Next = e;
                    }

                    loTail = e;
                }
                else {
                    if (hiTail == null) {
                        hi = e;
                    }
                    else {
                        hiTail.Next = e;
                    }

                    hiTail = e;
                }
            }

            table[j] = lo;
            table[j + bit] = hi;
        }

        return table;
    }

    private static Node BalanceInsertion(Node root, Node x)
    {
        x.Red = true;

        while (true) {
            var xp = x.Parent;

            if (xp == null) {
                x.Red = false;

                return x;
            }

            var xpp = xp.Parent;

            if (!xp.Red || xpp == null) {
                return root;
            }

            var xppl = xpp.Left;

            if (ReferenceEquals(xp, xppl)) {
                var xppr = xpp.Right;

                if (xppr is { Red: true }) {
                    xppr.Red = false;
                    xp.Red = false;
                    xpp.Red = true;
                    x = xpp;

                    continue;
                }

                if (ReferenceEquals(x, xp.Right)) {
                    root = RotateLeft(root, x = xp);
                    xp = x.Parent;
                    xpp = xp?.Parent;
                }

                if (xp != null) {
                    xp.Red = false;

                    if (xpp != null) {
                        xpp.Red = true;
                        root = RotateRight(root, xpp);
                    }
                }
            }
            else {
                if (xppl is { Red: true }) {
                    xppl.Red = false;
                    xp.Red = false;
                    xpp.Red = true;
                    x = xpp;

                    continue;
                }

                if (ReferenceEquals(x, xp.Left)) {
                    root = RotateRight(root, x = xp);
                    xp = x.Parent;
                    xpp = xp?.Parent;
                }

                if (xp != null) {
                    xp.Red = false;

                    if (xpp != null) {
                        xpp.Red = true;
                        root = RotateLeft(root, xpp);
                    }
                }
            }
        }
    }

    private static Node RotateLeft(Node root, Node p)
    {
        var r = p.Right;

        if (r == null) {
            return root;
        }

        var rl = p.Right = r.Left;

        if (rl != null) {
            rl.Parent = p;
        }

        var pp = r.Parent = p.Parent;

        if (pp == null) {
            root = r;
            r.Red = false;
        }
        else if (ReferenceEquals(pp.Left, p)) {
            pp.Left = r;
        }
        else {
            pp.Right = r;
        }

        r.Left = p;
        p.Parent = r;

        return root;
    }

    private static Node RotateRight(Node root, Node p)
    {
        var l = p.Left;

        if (l == null) {
            return root;
        }

        var lr = p.Left = l.Right;

        if (lr != null) {
            lr.Parent = p;
        }

        var pp = l.Parent = p.Parent;

        if (pp == null) {
            root = l;
            l.Red = false;
        }
        else if (ReferenceEquals(pp.Right, p)) {
            pp.Right = l;
        }
        else {
            pp.Left = l;
        }

        l.Right = p;
        p.Parent = l;

        return root;
    }
}
