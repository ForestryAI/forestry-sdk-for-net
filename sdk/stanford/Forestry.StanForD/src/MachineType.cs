using System.ComponentModel.DataAnnotations;
using System.Xml.Serialization;

namespace Forestry.StanForD
{
    [XmlType("Machine")]
    public class MachineType
    {
        /// <summary>
        /// Machine specific globally unique identity (GUID). Must be updated if memory of
        /// previously used Keys are lost. Possible for manufacturers to use this in order to
        /// identify individual machines. Other users of data should use MachineUserId or
        /// MachineIdOwner.
        /// </summary>
        [Required]
        public required string MachineKey { get; set; }
    }
}
