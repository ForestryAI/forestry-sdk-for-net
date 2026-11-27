# Contract: Public API added or changed

**Plan**: [../plan.md](../plan.md) | Decisions: [../research.md](../research.md)

Signatures are the contract; XML documentation follows D0. Nothing is released, so changes to the
core's public API are accepted.

## Forestry.Serialization

```csharp
namespace Forestry.Serialization
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class EnumMemberNameAttribute : Attribute               // new (R62)
    {
        public EnumMemberNameAttribute(string name);
        public string Name { get; }
    }
}

namespace Forestry.Serialization.Converters
{
    public enum EnumForm { Name, Number }                                   // new (R59)

    public abstract class EnumConverter<TEnum> : Converter<TEnum> where TEnum : struct, Enum   // new (R59)
    {
        protected EnumConverter(EnumForm form);
        public EnumForm Form { get; }
        public override TEnum Read(ReadOnlySpan<byte> utf8);                // the form's text; FormatException otherwise
        public override void Write(TEnum value, TelemetryWriter writer);    // WriteEnumName or WriteValue(long)
        protected static IReadOnlyList<EnumMember<TEnum>> Members { get; }
        protected static bool TryGetValue(ReadOnlySpan<char> name, out TEnum value);         // ignoring case
        protected static bool TryGetUtf8Name(TEnum value, out ReadOnlySpan<byte> utf8Name);
    }

    public readonly record struct EnumMember<TEnum>(TEnum Value, string MemberName, string Name)
        where TEnum : struct, Enum;                                         // new: one resolved field

    public abstract class ConverterFactory : Converter                     // changed (R58)
    {
        public virtual bool UsesMember => false;
        public abstract Converter CreateConverter(Type type, TelemetrySerializationOptions options);
        public virtual Converter CreateConverter(Type type, MemberInfo member, TelemetrySerializationOptions options);
                                                                            // default: CreateConverter(type, options)
    }
}

namespace Forestry.Serialization.Writing
{
    public abstract class TelemetryWriter : IBufferWriter<byte>             // changed (R60, R66)
    {
        public virtual Span<byte> GetSpan(int sizeHint = 0);                // now virtual
        public virtual Memory<byte> GetMemory(int sizeHint = 0);            // now virtual
        public virtual void Advance(int count);                             // now virtual

        public virtual void WriteValue(DateOnly value);                     // new: O
        public virtual void WriteValue(TimeOnly value);                     // new: O
        public virtual void WriteValue(TimeSpan value);                     // new: c
        public virtual void WriteEnumName(ReadOnlySpan<byte> utf8Name);     // new: the bytes
    }
}
```

Built in, not public: converters for enums (names), `DateOnly`, `TimeOnly`, `TimeSpan`.

## Forestry.Serialization.Xml

```csharp
namespace Forestry.Serialization.Xml
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class XmlEnumFormAttribute : Attribute                   // new (R64)
    {
        public XmlEnumFormAttribute(EnumForm form);
        public EnumForm Form { get; }
    }
}
```

Registered, not public: the XML enum converter (by `XmlEnumForm`, names by default) and the
`xs:duration` `TimeSpan` converter.

## Forestry.Serialization.JsonLines (new package)

```csharp
namespace Forestry.Serialization.JsonLines
{
    public sealed class SerializeJsonLinesOptions : SerializeOptions        // new (R65)
    {
        public SerializeJsonLinesOptions();
        protected override IEnumerable<Converter> FormatConverters { get; }
        protected override TelemetryWriter CreateWriter();
        protected override void OnMakingReadOnly();
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class JsonLinesEnumFormAttribute : Attribute             // new (R65)
    {
        public JsonLinesEnumFormAttribute(EnumForm form);
        public EnumForm Form { get; }
    }
}
```

Use:

```csharp
var options = new SerializeJsonLinesOptions().AddHarvesting();
foreach (Telemetry telemetry in stems)
{
    TelemetrySerialization.Serialize(telemetry, stream, options);   // one line per telemetry
}
```

Exceptions: a NaN or infinite `double`, and a converter writing through the writer's
`IBufferWriter<byte>` members, throw `NotSupportedException` naming the member (FR-011a, FR-011b).

## Forestry.StanForD

```csharp
namespace Forestry.StanForD
{
    public static class HarvestingOptionsExtensions
    {
        public static DeserializeXmlOptions AddHarvesting(this DeserializeXmlOptions options, Action<HarvestingOptions>? configure = null);  // unchanged
        public static TOptions AddHarvesting<TOptions>(this TOptions options) where TOptions : SerializeOptions;   // new (R63): Stem, Log, enum converters
    }

    public enum CuttingReason { /* [EnumMemberName("Grade break")] GradeBreak, ... */ }   // texts declared
}
```
