using System.Buffers;
using Forestry.Deserialize.Reading;
using Forestry.Deserialize.Xml.Reading;

namespace Forestry.Deserialize.Xml
{
    /// <summary>
    /// The XML media's reading, shared by its sync and async row enumerators (research R2): rebuilds
    /// the reader from its saved state each call, turns elements and text into the core's pipeline
    /// calls, and returns as soon as a row is completed
    /// </summary>
    /// <remarks>
    /// Association is by element name (research R13 A): an element named like a row type starts a row
    /// (wherever it is); a direct child element named like one of its members sets that member; its end
    /// tag completes the row. Everything else is skipped.
    /// </remarks>
    internal sealed class XmlRowReading(XmlAssociation association)
    {
        private const byte PathSeparator = (byte)'/';

        /// <summary>
        /// Where the reader stopped in its grammar
        /// </summary>
        private ReaderState _readerState = new();

        /// <summary>
        /// Open rows with the depth of their element
        /// </summary>
        private readonly Stack<(int Depth, XmlAssociation.RowAssociation Row)> _openRows = new();

        /// <summary>
        /// Member whose element is open, with its depth and whether it had a value
        /// </summary>
        private XmlAssociation.MemberAssociation? _member;

        private int _memberDepth;

        private bool _memberHasValue;

        /// <summary>
        /// Element path, e.g. <c>Root/Log/LogKey</c>, with the length before each element
        /// </summary>
        private byte[] _path = new byte[256];

        private int _pathLength;

        private readonly Stack<int> _pathMarks = new();

        public RowState Read(RowPipelines pipelines, ReadOnlySequence<byte> bytes, bool isFinalBlock, out Row? row, out long consumed)
        {
            row = null;
            Utf8XmlReader reader = new(bytes, isFinalBlock, _readerState);

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case TokenType.Element:
                        StartElement(pipelines, ref reader);
                        break;
                    case TokenType.Value:
                        if (_member is not null && reader.Depth == _memberDepth)
                        {
                            SetMember(pipelines, reader.Depth, Value(ref reader));
                            _memberHasValue = true;
                        }
                        break;
                    case TokenType.ElementEnd:
                        row = EndElement(pipelines, ref reader, out bool completed);
                        if (completed && row is not null)
                        {
                            _readerState = reader.ReaderState;
                            consumed = reader.Position;
                            return RowState.Complete;
                        }
                        break;
                }
            }

            _readerState = reader.ReaderState;
            consumed = reader.Position;

            if (!isFinalBlock)
            {
                return RowState.Partial;
            }

            if (reader.Depth > 0)
            {
                // Cut off inside an element: open rows are never handed out
                Throwing.ThrowXmlException(ref reader, Throwing.Category.Incomplete, Throwing.Markup.Element, offset: reader.RemainingLength);
            }

            return RowState.None;
        }

        private void StartElement(RowPipelines pipelines, ref Utf8XmlReader reader)
        {
            ReadOnlySpan<byte> name = PushPath(ref reader);
            int depth = reader.Depth;

            if (_member is not null)
            {
                return;  // inside a member's element: not a row or member of this slice
            }

            if (_openRows.TryPeek(out (int Depth, XmlAssociation.RowAssociation Row) open) &&
                depth == open.Depth + 1 &&
                open.Row.TryGetMember(name, out XmlAssociation.MemberAssociation? member))
            {
                _member = member;
                _memberDepth = depth;
                _memberHasValue = false;
                return;
            }

            if (association.TryGetRow(name, out XmlAssociation.RowAssociation? row))
            {
                row.Start(pipelines);
                _openRows.Push((depth, row));
            }
        }

        private Row? EndElement(RowPipelines pipelines, ref Utf8XmlReader reader, out bool completed)
        {
            completed = false;
            int depth = reader.Depth;  // after the end tag
            Row? row = null;

            if (_member is not null && depth == _memberDepth - 1)
            {
                if (!_memberHasValue)
                {
                    SetMember(pipelines, _memberDepth, []);  // empty element
                }

                _member = null;
            }
            else if (_openRows.TryPeek(out (int Depth, XmlAssociation.RowAssociation Row) open) && depth == open.Depth - 1)
            {
                _openRows.Pop();
                row = pipelines.Pipeline();
                completed = true;
            }

            PopPath();
            return row;
        }

        private void SetMember(RowPipelines pipelines, int depth, ReadOnlySpan<byte> utf8)
        {
            ReadOnlySpan<byte> path = _path.AsSpan(0, _pathLength);
            ReadOnlySpan<byte> name = path[(path.LastIndexOf(PathSeparator) + 1)..];
            FieldContext context = new(depth, name, path);

            _member!.Set(pipelines, utf8, in context);
        }

        /// <summary>
        /// The current token's value as one contiguous span; a value split across segments is copied
        /// </summary>
        /// <remarks>Joining split values without allocating is the multi-segment task (T068).</remarks>
        private static ReadOnlySpan<byte> Value(scoped ref Utf8XmlReader reader) =>
            reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.Value;

        private ReadOnlySpan<byte> PushPath(ref Utf8XmlReader reader)
        {
            ReadOnlySpan<byte> name = Value(ref reader);
            int required = _pathLength + 1 + name.Length;

            if (required > _path.Length)
            {
                Array.Resize(ref _path, Math.Max(required, _path.Length * 2));
            }

            _pathMarks.Push(_pathLength);

            if (_pathLength > 0)
            {
                _path[_pathLength++] = PathSeparator;
            }

            name.CopyTo(_path.AsSpan(_pathLength));
            _pathLength += name.Length;

            return _path.AsSpan(_pathLength - name.Length, name.Length);
        }

        private void PopPath()
        {
            if (_pathMarks.TryPop(out int length))
            {
                _pathLength = length;
            }
        }
    }
}
