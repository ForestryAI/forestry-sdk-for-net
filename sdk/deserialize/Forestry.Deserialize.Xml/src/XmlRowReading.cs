using System.Buffers;
using Forestry.Deserialize.Reading;
using Forestry.Xml;
using Forestry.Xml.Reading;

namespace Forestry.Deserialize.Xml
{
    /// <summary>
    /// The XML media's reading, shared by its sync and async row enumerators (research R2): rebuilds
    /// the reader from its saved state each call, turns elements, attributes and text into the core's
    /// row builder calls, and returns as soon as a row is completed
    /// </summary>
    /// <remarks>
    /// Association is by name (research R13 A, spec 002 R29, R30): an element named like a row type starts
    /// a row (wherever it is); a direct child element named like one of its child-element members sets that
    /// member; an attribute of the row's own element sets the member marked with that attribute name; text
    /// directly inside the row's element sets its text member (the last text wins; none by the row's end
    /// sets it empty); the end tag completes the row. Everything else is skipped, rows inside it still found.
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
        private readonly Stack<OpenRowElement> _openRows = new();

        /// <summary>
        /// Member whose element is open, with its depth and whether it had a value
        /// </summary>
        private XmlAssociation.MemberAssociation? _member;

        private int _memberDepth;

        private bool _memberHasValue;

        /// <summary>
        /// The row whose start tag is being read: its attributes set its members
        /// </summary>
        private OpenRowElement? _attributeTarget;

        /// <summary>
        /// The next Value token is an attribute's value, not text
        /// </summary>
        private bool _expectingAttributeValue;

        /// <summary>
        /// The member the pending attribute value sets, if any
        /// </summary>
        private XmlAssociation.MemberAssociation? _attributeMember;

        /// <summary>
        /// The pending attribute's name, for the field context
        /// </summary>
        private byte[] _attributeName = new byte[64];

        private int _attributeNameLength;

        /// <summary>
        /// Element path, e.g. <c>Root/Log/LogKey</c>, with the length before each element
        /// </summary>
        private byte[] _path = new byte[256];

        private int _pathLength;

        private readonly Stack<int> _pathMarks = new();

        public ReadStatus Read(RowBuilder builder, ReadOnlySequence<byte> bytes, bool isFinalBlock, out Row? row, out long consumed)
        {
            row = null;
            Utf8XmlReader reader = new(bytes, isFinalBlock, _readerState);

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case TokenType.Element:
                        StartElement(builder, ref reader);
                        break;
                    case TokenType.Attribute:
                        StartAttribute(ref reader);
                        break;
                    case TokenType.Value:
                        ReadValue(builder, ref reader);
                        break;
                    case TokenType.ElementEnd:
                        _attributeTarget = null;
                        row = EndElement(builder, ref reader, out bool completed);
                        if (completed && row is not null)
                        {
                            _readerState = reader.ReaderState;
                            consumed = reader.Position;
                            return ReadStatus.Complete;
                        }
                        break;
                }
            }

            _readerState = reader.ReaderState;
            consumed = reader.Position;

            if (!isFinalBlock)
            {
                return ReadStatus.Partial;
            }

            // A document cut off inside an element throws from the reader itself on the final block,
            // so open rows are never handed out
            return ReadStatus.None;
        }

        private void StartElement(RowBuilder builder, ref Utf8XmlReader reader)
        {
            ReadOnlySpan<byte> name = PushPath(ref reader);
            int depth = reader.Depth;
            _attributeTarget = null;

            if (_member is not null)
            {
                return;  // inside a member's element: not a row or member of this slice
            }

            if (_openRows.TryPeek(out OpenRowElement? open) &&
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
                row.Start(builder);
                OpenRowElement started = new(depth, row);
                _openRows.Push(started);
                _attributeTarget = started;
            }
        }

        private void StartAttribute(ref Utf8XmlReader reader)
        {
            _expectingAttributeValue = true;
            _attributeMember = null;

            if (_attributeTarget is null)
            {
                return;  // an attribute of a member, a wrapper or an unknown element: not read
            }

            ReadOnlySpan<byte> name = Value(ref reader);

            if (_attributeTarget.Row.TryGetAttribute(name, out XmlAssociation.MemberAssociation? member))
            {
                if (name.Length > _attributeName.Length)
                {
                    _attributeName = new byte[name.Length];
                }

                name.CopyTo(_attributeName);
                _attributeNameLength = name.Length;
                _attributeMember = member;
            }
        }

        private void ReadValue(RowBuilder builder, ref Utf8XmlReader reader)
        {
            if (_expectingAttributeValue)
            {
                _expectingAttributeValue = false;

                if (_attributeMember is not null)
                {
                    SetMember(builder, _attributeMember, reader.Depth, _attributeName.AsSpan(0, _attributeNameLength), Value(ref reader));
                    _attributeMember = null;
                }

                return;
            }

            _attributeTarget = null;

            if (_member is not null)
            {
                if (reader.Depth == _memberDepth)
                {
                    SetMember(builder, _member, reader.Depth, ElementName(), Value(ref reader));
                    _memberHasValue = true;
                }

                return;
            }

            if (_openRows.TryPeek(out OpenRowElement? open) && reader.Depth == open.Depth && open.Row.Text is { } text)
            {
                SetMember(builder, text, reader.Depth, ElementName(), Value(ref reader));  // the last text wins
                open.HasText = true;
            }
        }

        private Row? EndElement(RowBuilder builder, ref Utf8XmlReader reader, out bool completed)
        {
            completed = false;
            int depth = reader.Depth;  // after the end tag
            Row? row = null;

            if (_member is not null && depth == _memberDepth - 1)
            {
                if (!_memberHasValue)
                {
                    SetMember(builder, _member, _memberDepth, ElementName(), []);  // empty element
                }

                _member = null;
            }
            else if (_openRows.TryPeek(out OpenRowElement? open) && depth == open.Depth - 1)
            {
                if (open.Row.Text is { } text && !open.HasText)
                {
                    SetMember(builder, text, open.Depth, ElementName(), []);  // no text: empty, and set
                }

                _openRows.Pop();
                row = builder.Complete();
                completed = true;
            }

            PopPath();
            return row;
        }

        private void SetMember(RowBuilder builder, XmlAssociation.MemberAssociation member, int depth, ReadOnlySpan<byte> name, ReadOnlySpan<byte> utf8)
        {
            FieldContext context = new(depth, name, _path.AsSpan(0, _pathLength));
            member.Set(builder, utf8, in context);
        }

        /// <summary>
        /// The innermost element's name, the last segment of the path
        /// </summary>
        /// <returns></returns>
        private ReadOnlySpan<byte> ElementName()
        {
            ReadOnlySpan<byte> path = _path.AsSpan(0, _pathLength);
            return path[(path.LastIndexOf(PathSeparator) + 1)..];
        }

        /// <summary>
        /// The current token's value as one contiguous span; a value split across segments is copied
        /// </summary>
        /// <remarks>
        /// As <c>Utf8JsonReader</c>'s callers do (research R2, T068): it allocates only when a value
        /// straddles a buffer boundary, never per row. A value whose bytes are not all buffered yet is
        /// never a token: the reader halts before it and the read returns <see cref="ReadStatus.Partial"/>
        /// without consuming it. A scratch buffer instead of the copy is a later optimisation.
        /// </remarks>
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

        /// <summary>
        /// An open row's element: its depth, its association, and whether its text arrived
        /// </summary>
        /// <param name="depth"></param>
        /// <param name="row"></param>
        private sealed class OpenRowElement(int depth, XmlAssociation.RowAssociation row)
        {
            public int Depth { get; } = depth;

            public XmlAssociation.RowAssociation Row { get; } = row;

            public bool HasText { get; set; }
        }
    }
}
