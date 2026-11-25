namespace Forestry.Serialization.Reading
{
    /// <summary>
    /// What one enumeration holds that its policies share (spec 002, research R20, R26): the telemetries open
    /// around the current one and the telemetries policies keep after their completion. Created when the
    /// enumeration starts, emptied when it ends, after the policies' <c>Clear</c>
    /// </summary>
    /// <remarks>
    /// <para>
    /// A telemetry is in the context from its construction until its completion (open), and afterwards while a
    /// policy keeps it. Telemetries that come out and are not kept leave before they are handed out, so the
    /// common case costs no more than a push and a pop. The number of kept telemetries is limited by
    /// <see cref="DeserializeOptions.MaxContextTelemetries"/>.
    /// </para>
    /// <para>
    /// A telemetry and its policies only have what has been read so far: an open telemetry's members that come
    /// later in the document are not set yet (<see cref="ContextTelemetry.IsSet(string)"/>).
    /// </para>
    /// </remarks>
    public sealed class DeserializeContext
    {
        internal DeserializeContext(int maxKeptTelemetries)
        {
            _maxKeptTelemetries = maxKeptTelemetries;
        }

        private readonly int _maxKeptTelemetries;

        /// <summary>
        /// Open telemetries, outermost first
        /// </summary>
        private readonly List<ContextTelemetry> _open = [];

        /// <summary>
        /// Closed telemetries a policy kept, in the order they were kept
        /// </summary>
        private readonly List<ContextTelemetry> _kept = [];

        private long _nextSequence;

        /// <summary>
        /// The telemetry being built (the innermost open telemetry) or, during its completion, the telemetry being completed
        /// </summary>
        public ContextTelemetry? Current => Completing ?? (_open.Count > 0 ? _open[^1] : null);

        /// <summary>
        /// The telemetry whose completion is running, if any
        /// </summary>
        internal ContextTelemetry? Completing { get; private set; }

        /// <summary>
        /// Number of open telemetries
        /// </summary>
        internal int OpenCount => _open.Count;

        /// <summary>
        /// The open and kept telemetries of type <typeparamref name="TTelemetry"/> (or deriving from it), oldest first,
        /// including the telemetry being completed; released telemetries are left out. A query is a snapshot: releasing
        /// telemetries while iterating it takes effect for later queries
        /// </summary>
        /// <typeparam name="TTelemetry"></typeparam>
        /// <returns>Empty when there are none, never null</returns>
        public IEnumerable<ContextTelemetry> Telemetries<TTelemetry>() where TTelemetry : Telemetry
        {
            List<ContextTelemetry> telemetries = [];

            AddTelemetries<TTelemetry>(_kept, telemetries);

            if (Completing is { } completing && completing.Telemetry is TTelemetry && !completing.IsReleased)
            {
                telemetries.Add(completing);
            }

            AddTelemetries<TTelemetry>(_open, telemetries);

            telemetries.Sort(static (x, y) => x.Sequence.CompareTo(y.Sequence));

            return telemetries;
        }

        /// <summary>
        /// The telemetries open around <paramref name="telemetry"/>, innermost first: all open telemetries for the telemetry being
        /// completed, the outer ones for an open telemetry, none for a telemetry whose completion is over
        /// </summary>
        /// <param name="telemetry"></param>
        /// <returns></returns>
        public IEnumerable<ContextTelemetry> Ancestors(ContextTelemetry telemetry)
        {
            ArgumentNullException.ThrowIfNull(telemetry);

            int start;

            if (ReferenceEquals(telemetry, Completing))
            {
                start = _open.Count - 1;
            }
            else if (telemetry.IsOpen)
            {
                start = _open.IndexOf(telemetry) - 1;
            }
            else
            {
                return [];
            }

            List<ContextTelemetry> ancestors = new(Math.Max(start + 1, 0));

            for (int i = start; i >= 0; i--)
            {
                ancestors.Add(_open[i]);
            }

            return ancestors;
        }

        /// <summary>
        /// The telemetry that was open around <paramref name="telemetry"/> when it was constructed, while it is still in
        /// the context (open or kept); null for a telemetry with no parent, or whose parent left (FR-015)
        /// </summary>
        /// <param name="telemetry"></param>
        /// <returns></returns>
        public ContextTelemetry? Parent(ContextTelemetry telemetry)
        {
            ArgumentNullException.ThrowIfNull(telemetry);

            if (telemetry.Container.ParentId is not long parentId)
            {
                return null;
            }

            foreach (ContextTelemetry candidate in InContext())
            {
                if (candidate.Sequence == parentId)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// The telemetries whose parent is <paramref name="telemetry"/>, still in the context, oldest first: at a telemetry's
        /// completion, its children that policies kept (FR-015)
        /// </summary>
        /// <param name="telemetry"></param>
        /// <returns></returns>
        public IEnumerable<ContextTelemetry> Children(ContextTelemetry telemetry)
        {
            ArgumentNullException.ThrowIfNull(telemetry);

            return WithParent(telemetry.Sequence, except: null);
        }

        /// <summary>
        /// The other telemetries with the same parent as <paramref name="telemetry"/>, still in the context, oldest first
        /// (FR-015); telemetries with no parent are siblings of each other
        /// </summary>
        /// <param name="telemetry"></param>
        /// <returns></returns>
        public IEnumerable<ContextTelemetry> Siblings(ContextTelemetry telemetry)
        {
            ArgumentNullException.ThrowIfNull(telemetry);

            return WithParent(telemetry.Container.ParentId, except: telemetry);
        }

        /// <summary>
        /// A kept, closed telemetry leaves the context; at most once, and not for a telemetry kept until the end
        /// </summary>
        /// <param name="telemetry"></param>
        /// <exception cref="InvalidOperationException">Open, not kept, released already, or kept until the end</exception>
        public void Release(ContextTelemetry telemetry)
        {
            ArgumentNullException.ThrowIfNull(telemetry);

            telemetry.Release();
            _kept.Remove(telemetry);
        }

        /// <summary>
        /// A telemetry was constructed: it is open and the innermost
        /// </summary>
        /// <param name="chains"></param>
        /// <param name="telemetry"></param>
        /// <returns></returns>
        internal ContextTelemetry Push(Policies.TypeChains chains, Telemetry telemetry)
        {
            ContextTelemetry contextTelemetry = new(this, chains, telemetry, _nextSequence++, _open.Count > 0 ? _open[^1].Sequence : null);
            _open.Add(contextTelemetry);
            return contextTelemetry;
        }

        /// <summary>
        /// The innermost open telemetry
        /// </summary>
        /// <param name="telemetry"></param>
        /// <returns></returns>
        internal bool TryPeek([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ContextTelemetry? telemetry)
        {
            telemetry = _open.Count > 0 ? _open[^1] : null;
            return telemetry is not null;
        }

        /// <summary>
        /// The innermost open telemetry closes and its completion starts
        /// </summary>
        /// <returns></returns>
        internal bool TryBeginCompletion([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ContextTelemetry? telemetry)
        {
            if (_open.Count == 0)
            {
                telemetry = null;
                return false;
            }

            telemetry = _open[^1];
            _open.RemoveAt(_open.Count - 1);
            telemetry.IsOpen = false;
            Completing = telemetry;
            return true;
        }

        /// <summary>
        /// The completion is over: a telemetry kept by its policies stays, checked against the limit;
        /// any other leaves
        /// </summary>
        /// <param name="telemetry"></param>
        internal void EndCompletion(ContextTelemetry telemetry)
        {
            Completing = null;

            if (telemetry.Lifetime == Lifetime.Leave)
            {
                return;
            }

            if (_kept.Count >= _maxKeptTelemetries)
            {
                Throwing.WhenContextLimitReached(_maxKeptTelemetries, [.. _kept, telemetry]);
            }

            _kept.Add(telemetry);
        }

        /// <summary>
        /// The enumeration ended: every telemetry leaves
        /// </summary>
        internal void Clear()
        {
            Completing = null;
            _open.Clear();
            _kept.Clear();
        }

        /// <summary>
        /// Every telemetry in the context that is not released: kept, being completed, open
        /// </summary>
        /// <returns></returns>
        private IEnumerable<ContextTelemetry> InContext()
        {
            foreach (ContextTelemetry telemetry in _kept)
            {
                if (!telemetry.IsReleased)
                {
                    yield return telemetry;
                }
            }

            if (Completing is { IsReleased: false } completing)
            {
                yield return completing;
            }

            foreach (ContextTelemetry telemetry in _open)
            {
                yield return telemetry;
            }
        }

        /// <summary>
        /// A snapshot of the telemetries in the context with this parent id, oldest first
        /// </summary>
        /// <param name="parentId"></param>
        /// <param name="except"></param>
        /// <returns></returns>
        private List<ContextTelemetry> WithParent(long? parentId, ContextTelemetry? except)
        {
            List<ContextTelemetry> telemetries = [];

            foreach (ContextTelemetry candidate in InContext())
            {
                if (candidate.Container.ParentId == parentId && !ReferenceEquals(candidate, except))
                {
                    telemetries.Add(candidate);
                }
            }

            telemetries.Sort(static (x, y) => x.Sequence.CompareTo(y.Sequence));
            return telemetries;
        }

        private static void AddTelemetries<TTelemetry>(List<ContextTelemetry> source, List<ContextTelemetry> telemetries) where TTelemetry : Telemetry
        {
            foreach (ContextTelemetry telemetry in source)
            {
                if (telemetry.Telemetry is TTelemetry && !telemetry.IsReleased)
                {
                    telemetries.Add(telemetry);
                }
            }
        }
    }
}
