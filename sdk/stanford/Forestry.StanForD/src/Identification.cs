namespace Forestry.StanForD {
    /// <summary>
    /// The global identification shared by every StanForD telemetry: the machine key, a GUID without
    /// dashes (format "N"), then the telemetry's keys, separated by dashes
    /// </summary>
    /// <remarks>
    /// The machine key is globally unique; stem keys are running numbers per machine, never reset;
    /// log keys restart for every stem; common definition keys are running numbers per machine. So
    /// the machine key and the keys together are globally unique, e.g.
    /// "6f1d2c3b4a5e4f608a7192b3c4d5e6f7-619254-1".
    /// </remarks>
    public static class Identification
    {
        /// <summary>
        /// "{machine key}-{key}", e.g. a stem or a common definition
        /// </summary>
        public static string Format(Guid machineKey, int key) =>
            $"{machineKey:N}-{key}";

        /// <summary>
        /// "{machine key}-{key}-{subkey}", e.g. a log within its stem
        /// </summary>
        public static string Format(Guid machineKey, int key, int subkey) =>
            $"{machineKey:N}-{key}-{subkey}";
    }
}
