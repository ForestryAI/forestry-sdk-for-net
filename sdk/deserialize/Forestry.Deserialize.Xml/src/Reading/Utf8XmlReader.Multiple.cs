using System.Buffers;
using System.Diagnostics;

namespace Forestry.Deserialize.Xml.Reading
{
    /// <summary>
    /// Multiple segment reading
    /// </summary>
    public ref partial struct Utf8XmlReader
    {
        #region sequence
        public readonly SequencePosition SequencePosition
        {
            get
            {
                if (_isSequence)
                {
                    Debug.Assert(_currentSequencePosition.GetObject() is not null);
                    return _sequence.GetPosition(_segmentPosition, _currentSequencePosition);
                }
                return default;
            }
        }

        /// <summary>
        /// External access to the internal sequence
        /// </summary>
        internal readonly ReadOnlySequence<byte> Sequence => _sequence;
        #endregion

        public partial Utf8XmlReader(
            ReadOnlySequence<byte> segments,
            bool isFinalSegment,
            ReaderState readerState
        ): this(segments.FirstSpan, isFinalSegment, readerState)
        {
            _isSequence = true;
            _sequence = segments;

            _currentSequencePosition = segments.Start;

            // remaining fields depend on if the sequence has a single segment or multiple ignoring empty segments
            if (segments.IsSingleSegment)
            {
                _isMultipleSegments = false;
                _nextSequencePosition = default;
            } else
            {
                _nextSequencePosition = _currentSequencePosition;

                bool emptyFirstSegment = _segment.Length == 0;
                if (emptyFirstSegment)
                {
                    SequencePosition referenceSequencePosition = _nextSequencePosition;
                    while (segments.TryGet(ref _nextSequencePosition, out ReadOnlyMemory<byte> memory, advance: true))
                    {
                        _currentSequencePosition = referenceSequencePosition;
                        if (memory.Length != 0)
                        {
                            _segment = memory.Span;
                            break;
                        }
                        referenceSequencePosition = _nextSequencePosition;
                    }
                }

                _isFinalSegment = !segments.TryGet(ref _nextSequencePosition, out _, advance: !emptyFirstSegment) && isFinalSegment; 

                Debug.Assert(!_nextSequencePosition.Equals(_currentSequencePosition));
                _isMultipleSegments = true;
            }
        }

        /// <summary>
        /// Access the next non-empty segment of the underlying byte sequence, replacing
        /// <see cref="_segment"/> and resetting <see cref="_segmentPosition"/> - raw buffer
        /// plumbing, not a grammar-level operation, so it sits outside the Read/Skip/Peek
        /// vocabulary (see <see cref="IsSegmentFetchable"/>).
        /// </summary>
        /// <returns></returns>
        private bool FetchNextSegment()
        {
            ReadOnlyMemory<byte> memory;

            while (true)
            {
                Debug.Assert(!_isMultipleSegments || _currentSequencePosition.GetObject() is not null);

                SequencePosition referenceSequencePosition = _currentSequencePosition;
                _currentSequencePosition = _nextSequencePosition;

                if (!_sequence.TryGet(ref _nextSequencePosition, out memory, advance: true))
                {
                    _currentSequencePosition = referenceSequencePosition;
                    _isFinalSegment = true;

                    return false;
                }

                if (memory.Length != 0)
                {
                    break;
                }

                _currentSequencePosition = referenceSequencePosition;
                Debug.Assert(!_isMultipleSegments || _currentSequencePosition.GetObject() is not null);
            }

            if (_isExternalFinalSegment)
            {
                _isFinalSegment = !_sequence.TryGet(ref _nextSequencePosition, out _, advance: false);
            }

            _segment = memory.Span;

            _documentPosition += _segmentPosition;
            _segmentPosition = 0;

            return true;
        }

        /// <summary>
        /// Read opaque value from a multiple segments
        /// </summary>
        /// <param name="startingTerminal"></param>
        /// <param name="endingTermianl"></param>
        /// <returns></returns>
        internal bool ReadMultipleSegmentOpaqueValue(
            ReadOnlySpan<byte> startingTerminal,
            ReadOnlySpan<byte> endingTermianl,
            TokenType tokenType
        )
        {
            return false;
        }

        /// <summary>
        /// Read a Name across multiple segments - not yet built (#24: a Name split across a
        /// segment boundary needs the same kind of stitching/rollback
        /// <see cref="ReadMultipleSegmentOpaqueValue"/> still owes, not a naive retry). Returning
        /// <see langword="false"/> here is indistinguishable from a genuinely malformed Name to
        /// the caller today - accepted POC debt, same shape as the opaque-value gap above.
        /// </summary>
        /// <returns></returns>
        internal bool ReadMultipleSegmentName()
        {
            return false;
        }
    }
}