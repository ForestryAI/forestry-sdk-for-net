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
                Debug.Assert(_positionsCount > 1);
                Debug.Assert(_positions is not null);

                return ref _positions[_positionsCount - 2];
            }
        }

        /// <summary>
        /// When the path has paused positions waiting for continuation
        /// </summary>
        public readonly bool HasPausedPositions => _pausedPositionsCount != 0;

        /// <summary>
        /// Paused positions count
        /// </summary>
        private int _pausedPositionsCount;

        /// <summary>
        /// Positions count
        /// </summary>
        private int _positionsCount;

        /// <summary>
        /// Positions constituting the path
        /// </summary>
        private ReaderPosition[] _positions;

        /// <summary>
        /// When non-root not waiting for continuation then shift the current position to 
        /// the <see cref="TypeDefinition"/> of active property otherwise unshift to the previous position
        /// </summary>
        public void Shift()
        {
            if (_pausedPositionsCount == 0)
            {
                if (_positionsCount == 0) // root waiting for continuation
                {
                    _positionsCount = 1;
                } else
                {
                    TypeDefinition typeDefinition = Position.PropertyDefinition.TypeDefinition;

                    GrowPathSize();
                    _positions[_positionsCount - 1] = Position;
                    Position = default;
                    _positionsCount++;

                    // remarks: property definition overwritten to a non-self referencing in object deserializers
                    Position.TypeDefinition = typeDefinition;
                    Position.PropertyDefinition = typeDefinition.SelfReferencingPropertyDefinition;
                }
            } else
            {
                if (_positionsCount++ > 0)
                {
                    _positions[_positionsCount - 2] = Position;
                    Position = _positions[_positionsCount - 1];
                }

                if (_pausedPositionsCount == _positionsCount)
                {
                    _pausedPositionsCount = 0;
                }
            }
        }

        /// <summary>
        /// When done there are no paused positions before unshifting to the previous position 
        /// otherwise
        /// </summary>
        /// <param name="done"></param>
        public void Close(bool done)
        {
            Debug.Assert(_positionsCount > 0);

            if (!done)
            {
                if (_pausedPositionsCount == 0)
                {
                    if (_positionsCount == 1)
                    {
                        _pausedPositionsCount = 1;
                        _positionsCount = 0;
                        return;
                    }

                    GrowPathSize();
                    _pausedPositionsCount = _positionsCount--;
                }
                else if (--_positionsCount == 0)
                {
                    return;
                }

                _positions[_positionsCount] = Position;
                Position = _positions[_positionsCount - 1];
            }
            else
            {
                Debug.Assert(_pausedPositionsCount == 0);

                if (--_positionsCount > 0)
                {
                    Position = _positions[_positionsCount - 1];
                }
            }
        }

        private void GrowPathSize()
        {
            if (_positions is null)
            {
                _positions = new ReaderPosition[4];
            }
            else if (_positionsCount - 1 == _positions.Length)
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