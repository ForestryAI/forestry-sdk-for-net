namespace Forestry.StanForD {
    /// <summary>
    /// Interpretations are based on standard forestry terminology and harvesting practices 
    /// because StanForD does not define the terms.
    /// 
    /// Processing and Felling describe what operation the machine is doing while single and 
    /// multi describe how many trees are handled together.
    /// </summary>
    public enum ProcessingCategory
    {
        /// <summary>
        /// Processing one previously felled tree at a time.
        /// </summary>
        SingleTreeProcessing,
        /// <summary>
        /// Processing multiple previously felled trees together i.e. accumulated stems.
        /// </summary>
        MultiTreeProcessing,
        /// <summary>
        /// Felling one standing tree at a time.
        /// </summary>
        SingleTreeFelling,
        /// <summary>
        /// Felling multiple standing trees during the same operation
        /// </summary>
        MultiTreeFelling,
    }
}