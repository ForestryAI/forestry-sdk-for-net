namespace Forestry.StanForD {
    /// <summary>
    /// Why a log was cut, from StanForD <c>CuttingCategory/CuttingReason</c>.  Each member names the
    /// StanForD text it is read from; StanForD's texts contain spaces, so reading them needs a
    /// converter that maps the text to the member.
    /// </summary>
    public enum CuttingReason
    {
        /// <summary>
        /// "Automatic"
        /// </summary>
        Automatic,
        /// <summary>
        /// "Decay"
        /// </summary>
        Decay,
        /// <summary>
        /// "Damage"
        /// </summary>
        Damage,
        /// <summary>
        /// "Crook"
        /// </summary>
        Crook,
        /// <summary>
        /// "Sweep"
        /// </summary>
        Sweep,
        /// <summary>
        /// "Grade break"
        /// </summary>
        GradeBreak,
        /// <summary>
        /// "Pulpwood grade"
        /// </summary>
        PulpwoodGrade,
        /// <summary>
        /// "Top break"
        /// </summary>
        TopBreak,
        /// <summary>
        /// "Spinning"
        /// </summary>
        Spinning,
        /// <summary>
        /// "Other manual"
        /// </summary>
        OtherManual
    }
}
