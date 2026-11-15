using Forestry.Deserialize;
using Forestry.Deserialize.Policies;
using Forestry.Deserialize.Reading;

namespace Forestry.StanForD.Policies {
    /// <summary>
    /// Sets the open <see cref="Stem"/>'s metric members from the wrapper rows below it (FR-024)
    /// </summary>
    internal static class StemMetrics
    {
        /// <summary>
        /// The <see cref="Stem"/> open around a wrapper row
        /// </summary>
        /// <exception cref="InvalidDataException">The row is not inside a stem</exception>
        public static ContextRow Stem(ContextRow row, DeserializationContext context, string element) =>
            Enclosing.Row<Stem>(row, context) ?? throw new InvalidDataException($"A {element} is not inside a Stem.");

        /// <summary>
        /// Sets <paramref name="member"/> on the stem, or handles an unmatched category
        /// </summary>
        /// <param name="stem">The open stem</param>
        /// <param name="member">The matching member, or null when the category matches none</param>
        /// <param name="value"></param>
        /// <param name="occurrence">The element and its category, as named in an error</param>
        /// <param name="unmatched">What happens when <paramref name="member"/> is null</param>
        /// <exception cref="InvalidDataException">No match and <see cref="UnmatchedOccurrence.Fail"/>
        /// (FR-012), or the member is set already</exception>
        public static void Set<TValue>(ContextRow stem, string? member, TValue value, string occurrence, UnmatchedOccurrence unmatched)
        {
            string what = $"Stem {stem.Row.AsType<Stem>().StemKey}";

            if (member is null)
            {
                if (unmatched == UnmatchedOccurrence.Fail)
                {
                    throw new InvalidDataException($"{occurrence} in {what} matches no {nameof(StanForD.Stem)} member.");
                }

                return;
            }

            if (stem.IsSet(member))
            {
                throw new InvalidDataException($"{occurrence} in {what} sets {member} a second time.");
            }

            stem.Set(member, value);
        }
    }

    /// <summary>
    /// Stem coordinates never come out; the altitude and coordinate reference system are copied onto the stem
    /// </summary>
    internal sealed class StemCoordinatesPolicy : EnumerationPolicy
    {
        /// <inheritdoc/>
        public override void Complete(ContextRow row, DeserializationContext context, Action<ContextRow> next)
        {
            row.Enumerate(false);

            ContextRow stem = StemMetrics.Stem(row, context, nameof(StemCoordinates));
            StemCoordinates coordinates = row.Row.AsType<StemCoordinates>();

            if (row.IsSet(nameof(StemCoordinates.Altitude)))
            {
                stem.Set(nameof(StanForD.Stem.Altitude), coordinates.Altitude);
            }

            if (row.IsSet(nameof(StemCoordinates.CoordinateReferenceSystem)))
            {
                stem.Set(nameof(StanForD.Stem.CoordinateReferenceSystem), coordinates.CoordinateReferenceSystem);
            }

            next(row);
        }
    }

    /// <summary>
    /// A latitude never comes out; a stem's is copied onto the stem, positive north of the equator, negative
    /// south. A latitude outside <see cref="StemCoordinates"/> (e.g. in a bark function) is ignored
    /// </summary>
    /// <param name="unmatched">What happens to a latitude category other than "North" or "South"</param>
    internal sealed class LatitudePolicy(UnmatchedOccurrence unmatched) : EnumerationPolicy
    {
        /// <inheritdoc/>
        public override void Complete(ContextRow row, DeserializationContext context, Action<ContextRow> next)
        {
            row.Enumerate(false);

            if (Enclosing.Row<StemCoordinates>(row, context) is null)
            {
                next(row);  // not a stem's coordinate, e.g. a species group's bark function (T088)
                return;
            }

            Latitude latitude = row.Row.AsType<Latitude>();
            decimal? signed = latitude.LatitudeCategory switch
            {
                "North" => latitude.Value,
                "South" => -latitude.Value,
                _ => null
            };

            StemMetrics.Set(
                StemMetrics.Stem(row, context, nameof(Latitude)),
                signed is null ? null : nameof(StanForD.Stem.Latitude),
                signed ?? 0m,
                $"{nameof(Latitude)} (latitudeCategory \"{latitude.LatitudeCategory}\")",
                unmatched);

            next(row);
        }
    }

    /// <summary>
    /// A longitude never comes out; a stem's is copied onto the stem, positive east of the prime meridian,
    /// negative west. A longitude outside <see cref="StemCoordinates"/> is ignored
    /// </summary>
    /// <param name="unmatched">What happens to a longitude category other than "East" or "West"</param>
    internal sealed class LongitudePolicy(UnmatchedOccurrence unmatched) : EnumerationPolicy
    {
        /// <inheritdoc/>
        public override void Complete(ContextRow row, DeserializationContext context, Action<ContextRow> next)
        {
            row.Enumerate(false);

            if (Enclosing.Row<StemCoordinates>(row, context) is null)
            {
                next(row);  // not a stem's coordinate, e.g. a species group's bark function (T088)
                return;
            }

            Longitude longitude = row.Row.AsType<Longitude>();
            decimal? signed = longitude.LongitudeCategory switch
            {
                "East" => longitude.Value,
                "West" => -longitude.Value,
                _ => null
            };

            StemMetrics.Set(
                StemMetrics.Stem(row, context, nameof(Longitude)),
                signed is null ? null : nameof(StanForD.Stem.Longitude),
                signed ?? 0m,
                $"{nameof(Longitude)} (longitudeCategory \"{longitude.LongitudeCategory}\")",
                unmatched);

            next(row);
        }
    }

    /// <summary>
    /// A boom positioning never comes out; its angle and extension are copied onto the stem members of its
    /// category
    /// </summary>
    /// <param name="unmatched">What happens to a category other than "Felling" or "Last log cut"</param>
    internal sealed class BoomPositioningPolicy(UnmatchedOccurrence unmatched) : EnumerationPolicy
    {
        private static readonly Dictionary<string, (string Angle, string Extension)> s_members = new(StringComparer.Ordinal)
        {
            ["Felling"] = (nameof(StanForD.Stem.BoomAngleFelling), nameof(StanForD.Stem.BoomExtensionFelling)),
            ["Last log cut"] = (nameof(StanForD.Stem.BoomAngleLastLogCut), nameof(StanForD.Stem.BoomExtensionLastLogCut)),
        };

        /// <inheritdoc/>
        public override void Complete(ContextRow row, DeserializationContext context, Action<ContextRow> next)
        {
            row.Enumerate(false);

            BoomPositioning positioning = row.Row.AsType<BoomPositioning>();
            ContextRow stem = StemMetrics.Stem(row, context, nameof(BoomPositioning));
            bool found = s_members.TryGetValue(positioning.BoomPositioningCategory, out (string Angle, string Extension) members);
            string occurrence = $"{nameof(BoomPositioning)} (boomPositioningCategory \"{positioning.BoomPositioningCategory}\")";

            if (!found)
            {
                StemMetrics.Set<int>(stem, null, 0, occurrence, unmatched);
            }
            else
            {
                if (row.IsSet(nameof(BoomPositioning.BoomAngle)))
                {
                    StemMetrics.Set(stem, members.Angle, positioning.BoomAngle, occurrence, unmatched);
                }

                if (row.IsSet(nameof(BoomPositioning.BoomExtension)))
                {
                    StemMetrics.Set(stem, members.Extension, positioning.BoomExtension, occurrence, unmatched);
                }
            }

            next(row);
        }
    }

    /// <summary>
    /// A single-tree processed stem's wrapper never comes out; its DBH is copied onto the stem
    /// </summary>
    internal sealed class SingleTreeProcessedStemPolicy : EnumerationPolicy
    {
        /// <inheritdoc/>
        public override void Complete(ContextRow row, DeserializationContext context, Action<ContextRow> next)
        {
            row.Enumerate(false);

            if (row.IsSet(nameof(SingleTreeProcessedStem.DBH)))
            {
                StemMetrics.Stem(row, context, nameof(SingleTreeProcessedStem))
                    .Set(nameof(StanForD.Stem.DBH), row.Row.AsType<SingleTreeProcessedStem>().DBH);
            }

            next(row);
        }
    }
}
