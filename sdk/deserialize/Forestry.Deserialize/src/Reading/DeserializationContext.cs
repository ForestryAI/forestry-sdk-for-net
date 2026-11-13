namespace Forestry.Deserialize.Reading
{
    /// <summary>
    /// What one enumeration holds that its policies share (spec 002, research R20, R26): the rows open
    /// around the current one and the rows policies keep after their completion. Created when the
    /// enumeration starts, emptied when it ends, after the policies' <c>Clear</c>
    /// </summary>
    /// <remarks>
    /// <para>
    /// A row is in the context from its construction until its completion (open), and afterwards while a
    /// policy keeps it. Rows that come out and are not kept leave before they are handed out, so the
    /// common case costs no more than a push and a pop. The number of kept rows is limited by
    /// <see cref="DeserializeOptions.MaxContextRows"/>.
    /// </para>
    /// <para>
    /// A row and its policies only have what has been read so far: an open row's members that come
    /// later in the document are not set yet (<see cref="ContextRow.IsSet(string)"/>).
    /// </para>
    /// </remarks>
    public sealed class DeserializationContext
    {
        internal DeserializationContext(int maxKeptRows)
        {
            _maxKeptRows = maxKeptRows;
        }

        private readonly int _maxKeptRows;

        /// <summary>
        /// Open rows, outermost first
        /// </summary>
        private readonly List<ContextRow> _open = [];

        /// <summary>
        /// Closed rows a policy kept, in the order they were kept
        /// </summary>
        private readonly List<ContextRow> _kept = [];

        private long _nextSequence;

        /// <summary>
        /// The row being built (the innermost open row) or, during its completion, the row being completed
        /// </summary>
        public ContextRow? Current => Completing ?? (_open.Count > 0 ? _open[^1] : null);

        /// <summary>
        /// The row whose completion is running, if any
        /// </summary>
        internal ContextRow? Completing { get; private set; }

        /// <summary>
        /// Number of open rows
        /// </summary>
        internal int OpenCount => _open.Count;

        /// <summary>
        /// The open and kept rows of type <typeparamref name="TRow"/> (or deriving from it), oldest first,
        /// including the row being completed; released rows are left out. A query is a snapshot: releasing
        /// rows while iterating it takes effect for later queries
        /// </summary>
        /// <typeparam name="TRow"></typeparam>
        /// <returns>Empty when there are none, never null</returns>
        public IEnumerable<ContextRow> Rows<TRow>() where TRow : Row
        {
            List<ContextRow> rows = [];

            AddRows<TRow>(_kept, rows);

            if (Completing is { } completing && completing.Row is TRow && !completing.IsReleased)
            {
                rows.Add(completing);
            }

            AddRows<TRow>(_open, rows);

            rows.Sort(static (x, y) => x.Sequence.CompareTo(y.Sequence));

            return rows;
        }

        /// <summary>
        /// The rows open around <paramref name="row"/>, innermost first: all open rows for the row being
        /// completed, the outer ones for an open row, none for a row whose completion is over
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        public IEnumerable<ContextRow> Ancestors(ContextRow row)
        {
            ArgumentNullException.ThrowIfNull(row);

            int start;

            if (ReferenceEquals(row, Completing))
            {
                start = _open.Count - 1;
            }
            else if (row.IsOpen)
            {
                start = _open.IndexOf(row) - 1;
            }
            else
            {
                return [];
            }

            List<ContextRow> ancestors = new(Math.Max(start + 1, 0));

            for (int i = start; i >= 0; i--)
            {
                ancestors.Add(_open[i]);
            }

            return ancestors;
        }

        /// <summary>
        /// The row that was open around <paramref name="row"/> when it was constructed, while it is still in
        /// the context (open or kept); null for a row with no parent, or whose parent left (FR-015)
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        public ContextRow? Parent(ContextRow row)
        {
            ArgumentNullException.ThrowIfNull(row);

            if (row.Container.ParentId is not long parentId)
            {
                return null;
            }

            foreach (ContextRow candidate in InContext())
            {
                if (candidate.Sequence == parentId)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// The rows whose parent is <paramref name="row"/>, still in the context, oldest first: at a row's
        /// completion, its children that policies kept (FR-015)
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        public IEnumerable<ContextRow> Children(ContextRow row)
        {
            ArgumentNullException.ThrowIfNull(row);

            return WithParent(row.Sequence, except: null);
        }

        /// <summary>
        /// The other rows with the same parent as <paramref name="row"/>, still in the context, oldest first
        /// (FR-015); rows with no parent are siblings of each other
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        public IEnumerable<ContextRow> Siblings(ContextRow row)
        {
            ArgumentNullException.ThrowIfNull(row);

            return WithParent(row.Container.ParentId, except: row);
        }

        /// <summary>
        /// A kept, closed row leaves the context; at most once, and not for a row kept until the end
        /// </summary>
        /// <param name="row"></param>
        /// <exception cref="InvalidOperationException">Open, not kept, released already, or kept until the end</exception>
        public void Release(ContextRow row)
        {
            ArgumentNullException.ThrowIfNull(row);

            row.Release();
            _kept.Remove(row);
        }

        /// <summary>
        /// A row was constructed: it is open and the innermost
        /// </summary>
        /// <param name="chains"></param>
        /// <param name="row"></param>
        /// <returns></returns>
        internal ContextRow Push(Policies.TypeChains chains, Row row)
        {
            ContextRow contextRow = new(this, chains, row, _nextSequence++, _open.Count > 0 ? _open[^1].Sequence : null);
            _open.Add(contextRow);
            return contextRow;
        }

        /// <summary>
        /// The innermost open row
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        internal bool TryPeek([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ContextRow? row)
        {
            row = _open.Count > 0 ? _open[^1] : null;
            return row is not null;
        }

        /// <summary>
        /// The innermost open row closes and its completion starts
        /// </summary>
        /// <returns></returns>
        internal bool TryBeginCompletion([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ContextRow? row)
        {
            if (_open.Count == 0)
            {
                row = null;
                return false;
            }

            row = _open[^1];
            _open.RemoveAt(_open.Count - 1);
            row.IsOpen = false;
            Completing = row;
            return true;
        }

        /// <summary>
        /// The completion is over: a row kept by its policies stays, checked against the limit;
        /// any other leaves
        /// </summary>
        /// <param name="row"></param>
        internal void EndCompletion(ContextRow row)
        {
            Completing = null;

            if (row.Lifetime == Lifetime.Leave)
            {
                return;
            }

            if (_kept.Count >= _maxKeptRows)
            {
                Throwing.WhenContextLimitReached(_maxKeptRows, [.. _kept, row]);
            }

            _kept.Add(row);
        }

        /// <summary>
        /// The enumeration ended: every row leaves
        /// </summary>
        internal void Clear()
        {
            Completing = null;
            _open.Clear();
            _kept.Clear();
        }

        /// <summary>
        /// Every row in the context that is not released: kept, being completed, open
        /// </summary>
        /// <returns></returns>
        private IEnumerable<ContextRow> InContext()
        {
            foreach (ContextRow row in _kept)
            {
                if (!row.IsReleased)
                {
                    yield return row;
                }
            }

            if (Completing is { IsReleased: false } completing)
            {
                yield return completing;
            }

            foreach (ContextRow row in _open)
            {
                yield return row;
            }
        }

        /// <summary>
        /// A snapshot of the rows in the context with this parent id, oldest first
        /// </summary>
        /// <param name="parentId"></param>
        /// <param name="except"></param>
        /// <returns></returns>
        private List<ContextRow> WithParent(long? parentId, ContextRow? except)
        {
            List<ContextRow> rows = [];

            foreach (ContextRow candidate in InContext())
            {
                if (candidate.Container.ParentId == parentId && !ReferenceEquals(candidate, except))
                {
                    rows.Add(candidate);
                }
            }

            rows.Sort(static (x, y) => x.Sequence.CompareTo(y.Sequence));
            return rows;
        }

        private static void AddRows<TRow>(List<ContextRow> source, List<ContextRow> rows) where TRow : Row
        {
            foreach (ContextRow row in source)
            {
                if (row.Row is TRow && !row.IsReleased)
                {
                    rows.Add(row);
                }
            }
        }
    }
}
