using System.Diagnostics;
using Forestry.Deserialize.Definitions;

namespace Forestry.Deserialize.Reading
{
    /// <summary>
    /// Positions with depth in the type definition hierarchy defining a deserialization path
    /// </summary>
    public struct ReaderPath
    {
        /// <summary>
        /// Current position where the deserialization path is active
        /// </summary>
        public ReaderPosition Position;

        /// <summary>
        /// Previous position
        /// </summary>
        public readonly ref ReaderPosition PreviousPosition
        {
            get
            {
                Debug.Assert(_depth > 1);
                Debug.Assert(_positions is not null);

                return ref _positions[_depth - 2];
            }
        }

        /// <summary>
        /// When the path has paused positions ready for re-entry
        /// </summary>
        public readonly bool HasPausedPositions => _pausedPositionsCount != 0;

        /// <summary>
        /// When not zero then paused positions are ready for re-entry
        /// </summary>
        private int _pausedPositionsCount;

        /// <summary>
        /// Path depth
        /// </summary>
        private int _depth;

        /// <summary>
        /// Positions constituting the path
        /// </summary>
        private ReaderPosition[] _positions;

        /// <summary>
        /// Open 
        /// </summary>
        public void Open()
        {
            if (_pausedPositionsCount == 0)
            {
                if (_depth == 0) // just use current position
                {
                    _depth = 1;
                } else
                {
                    TypeDefinition typeDefinition = Position.PropertyDefinition.TypeDefinition;

                    GrowPathSize();
                    _positions[_depth - 1] = Position;
                    Position = default;
                    _depth++;

                    Position.TypeDefinition = typeDefinition;
                    Position.PropertyDefinition = typeDefinition.SelfReferencingPropertyDefinition;
                }
            } else
            {
                if (_depth++ > 0)
                {
                    _positions[_depth - 2] = Position;
                    Position = _positions[_depth - 1];
                }

                if (_pausedPositionsCount == _depth)
                {
                    _pausedPositionsCount = 0;
                }
            }
        }

        /// <summary>
        /// Move the path back
        /// </summary>
        /// <param name="done"></param>
        public void Close(bool done)
        {
            Debug.Assert(_depth > 0);

            if (!done)
            {
                if (_pausedPositionsCount == 0) // next position at the same depth
                {
                    if (_depth == 1)
                    {
                        _pausedPositionsCount = 1;
                        _depth = 0;
                        return;
                    }

                    GrowPathSize();
                    _pausedPositionsCount = _depth--;
                }
                else if (--_depth == 0)  // no more positions e.g. root
                {
                    return;
                }

                _positions[_depth] = Position;
                Position = _positions[_depth - 1];
            }
            else
            {
                Debug.Assert(_pausedPositionsCount == 0);

                if (--_depth > 0)
                {
                    Position = _positions[_depth - 1];
                }
            }
        }

        private void GrowPathSize()
        {
            if (_positions is null)
            {
                _positions = new ReaderPosition[4];
            }
            else if (_depth - 1 == _positions.Length)
            {
                Array.Resize(ref _positions, 2 * _positions.Length);
            }
        }

        /// <summary>
        /// Set type and property (as self-referencing) definition of the current position
        /// </summary>
        /// <param name="typeDefintion"></param>
        /// <param name="useContinuation"></param>
        internal void SetPosition(TypeDefinition typeDefintion, bool useContinuation = false)
        {
            Position.TypeDefinition = typeDefintion;
            Position.PropertyDefinition = typeDefintion.SelfReferencingPropertyDefinition;

            // TODO: use continuation flag
        }
    }
}