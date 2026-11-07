namespace Forestry.Deserialize.Xml.Tests.Fixtures
{
    /// <summary>
    /// What recording policies saw, scoped to the running test (tasks.md T049, research R19)
    /// </summary>
    /// <remarks>
    /// The current recording flows through <see cref="AsyncLocal{T}"/>, so tests running in parallel,
    /// sync or async, never mix records, and a policy the core created from an attribute finds the
    /// test's recording without a static log shared between tests. Outside a recording, policies
    /// record nothing.
    /// </remarks>
    public sealed class Recording : IDisposable
    {
        private static readonly AsyncLocal<Recording?> s_current = new();

        private readonly Recording? _previous;

        private readonly List<string> _records = [];

        private readonly List<object> _clearedPolicies = [];

        private Recording()
        {
            _previous = s_current.Value;
            s_current.Value = this;
        }

        /// <summary>
        /// Start recording for the running test; dispose to stop
        /// </summary>
        /// <returns></returns>
        public static Recording Start() => new();

        /// <summary>
        /// What was recorded, in order
        /// </summary>
        public IReadOnlyList<string> Records
        {
            get
            {
                lock (_records)
                {
                    return [.. _records];
                }
            }
        }

        /// <summary>
        /// The policy instances cleared, in order, one entry per <c>Clear()</c> call
        /// </summary>
        public IReadOnlyList<object> ClearedPolicies
        {
            get
            {
                lock (_records)
                {
                    return [.. _clearedPolicies];
                }
            }
        }

        /// <summary>
        /// Record into the running test's recording, if any
        /// </summary>
        /// <param name="record"></param>
        public static void Record(string record)
        {
            Recording? current = s_current.Value;

            if (current is null)
            {
                return;
            }

            lock (current._records)
            {
                current._records.Add(record);
            }
        }

        /// <summary>
        /// Record that <paramref name="policy"/> was cleared
        /// </summary>
        /// <param name="policy"></param>
        /// <param name="record"></param>
        public static void RecordCleared(object policy, string record)
        {
            Recording? current = s_current.Value;

            if (current is null)
            {
                return;
            }

            lock (current._records)
            {
                current._records.Add(record);
                current._clearedPolicies.Add(policy);
            }
        }

        public void Dispose() => s_current.Value = _previous;
    }
}
