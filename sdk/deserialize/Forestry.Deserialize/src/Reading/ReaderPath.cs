using Forestry.Deserialize.Definitions;

namespace Forestry.Deserialize.Reading
{
    /// <summary>
    /// A subsection of the Type Definition hierarchy currently active for a read: the chain of
    /// <see cref="TypeDefinition"/>s from the root down to wherever reading currently is, each
    /// with its properties already expanded and indexed (<see cref="ReaderPosition"/>).
    /// </summary>
    public struct ReaderPath
    {
        /// <summary>
        /// Current (last) position - where the deserialization is currently acting
        /// </summary>
        public ReaderPosition Position;

        /// <summary>
        /// Set the current position as the passed type definition
        /// </summary>
        /// <param name="typeDefintion"></param>
        /// <param name="useContinuation"></param>
        internal void SetPosition(TypeDefinition typeDefintion, bool useContinuation = false)
        {
            Position.TypeDefinition = typeDefintion;
        }
    }
}