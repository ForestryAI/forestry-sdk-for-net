namespace Forestry.Deserialize.Xml {

    /// <summary>
    /// EBNF XML grammar
    /// </summary>
    /// <seealso href="https://en.wikipedia.org/wiki/Extended_Backus%E2%80%93Naur_form"/>
    public static partial class EBNF
    {
        /// <summary>
        /// document ::= prolog element miscellaneous*
        /// </summary>
        public enum Document: byte
        {
            None = (byte)0,

            Prolog = (byte)1,

            Element = (byte)2,

            Miscellaneous  = (byte)3,
        }
    }
}