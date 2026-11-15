using Forestry.Deserialize;
using Forestry.Deserialize.Policies;
using Forestry.Deserialize.Reading;

namespace Forestry.StanForD.Policies {
    /// <summary>
    /// Sets a member of the open <see cref="Log"/> from a measurement row whose categories matched (FR-010 to
    /// FR-013)
    /// </summary>
    internal static class ConditionalMapping
    {
        /// <summary>
        /// The measurement category every condition needs: the machine's measurement (M1)
        /// </summary>
        public const string Machine = "Machine";

        /// <summary>
        /// Measurement categories deliberately ignored (FR-011): the operator's own measurement (M2), seen
        /// beside the machine's in real Harvesting documents; to be looked into later (developer,
        /// 2026-11-14, #109). Quality-check measurements (M3) are not expected in Harvesting documents
        /// </summary>
        private static readonly HashSet<string> s_ignored = new(StringComparer.Ordinal) { "Operator" };

        /// <summary>
        /// Whether a measurement of <paramref name="measurementCategory"/> is deliberately ignored, neither
        /// setting a member nor counting as unmatched
        /// </summary>
        public static bool IsIgnored(string measurementCategory) => s_ignored.Contains(measurementCategory);

        /// <summary>
        /// The <see cref="Log"/> open around a measurement row
        /// </summary>
        /// <exception cref="InvalidDataException">The row is not inside a log</exception>
        public static ContextRow Log(ContextRow row, DeserializationContext context, string element) =>
            context.Ancestors(row).FirstOrDefault(ancestor => ancestor.Row is Log)
                ?? throw new InvalidDataException($"A {element} is not inside a Log.");

        /// <summary>
        /// The log, as named in an error, e.g. "Log 1 of Stem 619254"
        /// </summary>
        public static string Describe(ContextRow log, DeserializationContext context) =>
            $"Log {log.Row.AsType<Log>().LogKey} of Stem {Harvesting.Stem(log, context).StemKey}";

        /// <summary>
        /// Sets <paramref name="member"/> on the log, or handles an unmatched occurrence
        /// </summary>
        /// <param name="log">The open log</param>
        /// <param name="context"></param>
        /// <param name="member">The matching member, or null when the categories match none</param>
        /// <param name="value"></param>
        /// <param name="occurrence">The element and its categories, as named in an error</param>
        /// <param name="unmatched">What happens when <paramref name="member"/> is null</param>
        /// <exception cref="InvalidDataException">No match and <see cref="UnmatchedOccurrence.Fail"/> (FR-012),
        /// or the member is set already (FR-013)</exception>
        public static void Set<TValue>(
            ContextRow log,
            DeserializationContext context,
            string? member,
            TValue value,
            string occurrence,
            UnmatchedOccurrence unmatched)
        {
            if (member is null)
            {
                if (unmatched == UnmatchedOccurrence.Fail)
                {
                    throw new InvalidDataException($"{occurrence} in {Describe(log, context)} matches no {nameof(StanForD.Log)} member.");
                }

                return;
            }

            if (log.IsSet(member))
            {
                throw new InvalidDataException($"{occurrence} in {Describe(log, context)} sets {member} a second time.");
            }

            log.Set(member, value);
        }
    }

    /// <summary>
    /// A volume never comes out; its value lands in the log member its volume and measurement categories
    /// match
    /// </summary>
    /// <param name="unmatched">What happens to a volume matching no member</param>
    internal sealed class LogVolumePolicy(UnmatchedOccurrence unmatched) : EnumerationPolicy
    {
        private static readonly Dictionary<string, string> s_members = new(StringComparer.Ordinal)
        {
            ["m3sob"] = nameof(Log.MachineVolumeOverBark),
            ["m3sub"] = nameof(Log.MachineVolumeUnderBark),
            ["m3 (price)"] = nameof(Log.MachineVolumePrice),
        };

        /// <inheritdoc/>
        public override void Complete(ContextRow row, DeserializationContext context, Action<ContextRow> next)
        {
            row.Enumerate(false);

            LogVolume volume = row.Row.AsType<LogVolume>();

            if (ConditionalMapping.IsIgnored(volume.LogMeasurementCategory))
            {
                next(row);
                return;
            }

            ContextRow log = ConditionalMapping.Log(row, context, nameof(LogVolume));
            string? member = volume.LogMeasurementCategory == ConditionalMapping.Machine
                && s_members.TryGetValue(volume.LogVolumeCategory, out string? found) ? found : null;
            string occurrence = $"{nameof(LogVolume)} (logVolumeCategory \"{volume.LogVolumeCategory}\", logMeasurementCategory \"{volume.LogMeasurementCategory}\")";

            ConditionalMapping.Set(log, context, member, volume.Value, occurrence, unmatched);

            next(row);
        }
    }

    /// <summary>
    /// A measurement never comes out; a machine measurement's length lands in the log's length
    /// </summary>
    /// <param name="unmatched">What happens to a length under a measurement that is not the machine's</param>
    internal sealed class LogMeasurementPolicy(UnmatchedOccurrence unmatched) : EnumerationPolicy
    {
        /// <inheritdoc/>
        public override void Complete(ContextRow row, DeserializationContext context, Action<ContextRow> next)
        {
            row.Enumerate(false);

            if (row.IsSet(nameof(LogMeasurement.LogLength))
                && !ConditionalMapping.IsIgnored(row.Row.AsType<LogMeasurement>().LogMeasurementCategory))
            {
                LogMeasurement measurement = row.Row.AsType<LogMeasurement>();
                ContextRow log = ConditionalMapping.Log(row, context, nameof(LogMeasurement));
                string? member = measurement.LogMeasurementCategory == ConditionalMapping.Machine ? nameof(Log.MachineLength) : null;
                string occurrence = $"{nameof(LogMeasurement)}/{nameof(LogMeasurement.LogLength)} (logMeasurementCategory \"{measurement.LogMeasurementCategory}\")";

                ConditionalMapping.Set(log, context, member, measurement.LogLength, occurrence, unmatched);
            }

            next(row);
        }
    }

    /// <summary>
    /// A diameter never comes out; its value lands in the log member its diameter category and its
    /// measurement's category match
    /// </summary>
    /// <param name="unmatched">What happens to a diameter matching no member</param>
    internal sealed class LogDiameterPolicy(UnmatchedOccurrence unmatched) : EnumerationPolicy
    {
        private static readonly Dictionary<string, string> s_members = new(StringComparer.Ordinal)
        {
            ["Butt ob"] = nameof(Log.MachineDiameterButtOverBark),
            ["Butt ub"] = nameof(Log.MachineDiameterButtUnderBark),
            ["Mid ob"] = nameof(Log.MachineDiameterMidOverBark),
            ["Mid ub"] = nameof(Log.MachineDiameterMidUnderBark),
            ["Mid HKS ob"] = nameof(Log.MachineDiameterMidHKSOverBark),
            ["Mid HKS ub"] = nameof(Log.MachineDiameterMidHKSUnderBark),
            ["Top ob"] = nameof(Log.MachineDiameterTopOverBark),
            ["Top ub"] = nameof(Log.MachineDiameterTopUnderBark),
        };

        /// <inheritdoc/>
        public override void Complete(ContextRow row, DeserializationContext context, Action<ContextRow> next)
        {
            row.Enumerate(false);

            LogDiameter diameter = row.Row.AsType<LogDiameter>();
            ContextRow log = ConditionalMapping.Log(row, context, nameof(LogDiameter));
            string measurementCategory = context.Ancestors(row)
                .FirstOrDefault(ancestor => ancestor.Row is LogMeasurement)?.Row.AsType<LogMeasurement>().LogMeasurementCategory ?? string.Empty;

            if (ConditionalMapping.IsIgnored(measurementCategory))
            {
                next(row);
                return;
            }

            string? member = measurementCategory == ConditionalMapping.Machine
                && s_members.TryGetValue(diameter.LogDiameterCategory, out string? found) ? found : null;
            string occurrence = $"{nameof(LogDiameter)} (logDiameterCategory \"{diameter.LogDiameterCategory}\", logMeasurementCategory \"{measurementCategory}\")";

            ConditionalMapping.Set(log, context, member, diameter.Value, occurrence, unmatched);

            next(row);
        }
    }
}
