namespace Forestry.Deserialize.Policies
{
    /// <summary>
    /// Base of every policy (research R19): a <see cref="DefinitionPolicy"/> shapes the definitions once,
    /// when the options lock; an <see cref="EnumerationPolicy"/> acts while rows are built and enumerated
    /// </summary>
    /// <remarks>
    /// A policy takes part in exactly the virtual methods it overrides. Each method gets the next step
    /// as its last argument, <c>next</c>: the next policy added through the options for the same target,
    /// or the built-in step; a policy may wrap it, replace it or change its result.
    /// </remarks>
    public abstract class Policy
    {
        private protected Policy()
        {
        }
    }
}
