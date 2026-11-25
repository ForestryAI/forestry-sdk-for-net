using System.Buffers;
using Forestry.Serialization.Converters;
using Xunit;

namespace Forestry.Serialization.Tests.Fixtures
{
    /// <summary>
    /// Writes a value through a converter and reads it back (spec 004 tasks.md T001, research R45)
    /// </summary>
    internal static class ConverterRoundTrip
    {
        /// <summary>
        /// The built-in converter the options resolve for <typeparamref name="T"/>, as a property definition
        /// would (research R33)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Converter<T> BuiltIn<T>() =>
            Assert.IsAssignableFrom<Converter<T>>(new TestingDeserializeOptions().ResolveConverter(typeof(T)));

        /// <summary>
        /// The UTF-8 bytes <paramref name="converter"/> writes for <paramref name="value"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="converter"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static byte[] Write<T>(Converter<T> converter, T value)
        {
            ArrayBufferWriter<byte> utf8 = new();
            converter.Write(value, utf8);
            return utf8.WrittenSpan.ToArray();
        }

        /// <summary>
        /// Write <paramref name="value"/>, then read the written bytes with the same converter
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="converter"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static T RoundTrip<T>(Converter<T> converter, T value) => converter.Read(Write(converter, value));
    }

    /// <summary>
    /// A buffer writer that offers only <see cref="FirstOffer"/> bytes when asked without a size hint,
    /// and exactly the hint otherwise, recording every hint (spec 004 US1-2, FR-002): a converter has to
    /// ask for the room it needs rather than assume it
    /// </summary>
    internal sealed class SmallOfferBufferWriter : IBufferWriter<byte>
    {
        public const int FirstOffer = 16;

        private readonly ArrayBufferWriter<byte> _written = new();

        /// <summary>
        /// Every size hint asked for, in order
        /// </summary>
        public List<int> Hints { get; } = [];

        public ReadOnlySpan<byte> WrittenSpan => _written.WrittenSpan;

        public void Advance(int count) => _written.Advance(count);

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            Hints.Add(sizeHint);
            return _written.GetMemory(Math.Max(sizeHint, 1))[..Offer(sizeHint)];
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            Hints.Add(sizeHint);
            return _written.GetSpan(Math.Max(sizeHint, 1))[..Offer(sizeHint)];
        }

        private static int Offer(int sizeHint) => sizeHint > 0 ? sizeHint : FirstOffer;
    }
}
