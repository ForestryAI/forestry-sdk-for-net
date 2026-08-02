namespace Forestry.Deserialize.Reading
{
    /// <summary>
    /// Continuation state to recreate a reader. Responsible for establishing the reader's state,
    /// which is typed in the concrete media Deserializer classes.
    /// </summary>
    public interface IReaderState<TState> where TState : struct, IReaderState<TState>
    {

    }
}