using Forestry.Serialization;

namespace Forestry.StanForD {
    /// <summary>
    /// Why a log was cut, from StanForD <c>CuttingCategory/CuttingReason</c>.  Each member names the
    /// StanForD text it is read from and written as; texts with spaces are declared with
    /// <see cref="EnumMemberNameAttribute"/> (serialization spec 005, research R63).
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
        [EnumMemberName("Grade break")]
        GradeBreak,
        /// <summary>
        /// "Pulpwood grade"
        /// </summary>
        [EnumMemberName("Pulpwood grade")]
        PulpwoodGrade,
        /// <summary>
        /// "Top break"
        /// </summary>
        [EnumMemberName("Top break")]
        TopBreak,
        /// <summary>
        /// "Spinning"
        /// </summary>
        Spinning,
        /// <summary>
        /// "Other manual"
        /// </summary>
        [EnumMemberName("Other manual")]
        OtherManual
    }
}
