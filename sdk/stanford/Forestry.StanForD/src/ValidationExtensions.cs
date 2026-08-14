using System.ComponentModel.DataAnnotations;

namespace Forestry.StanForD
{
    /// <summary>
    /// Validates a deserialized StanForD type against its <see cref="ValidationAttribute"/>s
    /// (e.g. <see cref="RequiredAttribute"/> on <see cref="MachineType.MachineKey"/>),
    /// independent of whatever actually produced the instance.
    /// </summary>
    /// <remarks>
    /// Neither <see cref="System.Xml.Serialization.XmlSerializer"/> nor the C# <c>required</c>
    /// modifier enforce this on their own: <c>XmlSerializer</c> constructs via reflection and
    /// sets properties through their public setters, which never runs the compiler's "were all
    /// required members set" check - <c>required</c> only guards direct C# construction
    /// (<c>new MachineType { ... }</c>). <c>Forestry.Deserialize</c> never constructs an instance
    /// at all, so neither check ever applies to it either. This is the explicit step that
    /// actually validates a deserialized object, called after deserialization completes.
    /// </remarks>
    public static class ValidationExtensions
    {
        /// <summary>
        /// Validates every <see cref="ValidationAttribute"/> on <paramref name="instance"/>,
        /// throwing a single <see cref="ValidationException"/> listing every failure if any
        /// fail - not just the first, so all problems are visible at once.
        /// </summary>
        /// <exception cref="ValidationException">One or more validations failed.</exception>
        public static void Validate(this object instance)
        {
            ValidationContext context = new(instance);
            List<ValidationResult> results = [];

            if (!Validator.TryValidateObject(instance, context, results, validateAllProperties: true))
            {
                string message = string.Join("; ", results.Select(result => result.ErrorMessage));
                throw new ValidationException(message);
            }
        }
    }
}
