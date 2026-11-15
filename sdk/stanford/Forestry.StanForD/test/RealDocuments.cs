using System.IO.Compression;
using System.Text;
using Xunit;

namespace Forestry.StanForD.Tests
{
    /// <summary>
    /// Real Harvesting documents from a local folder, for end-to-end tests only (A4, tasks.md T006)
    /// </summary>
    /// <remarks>
    /// Real documents hold personal data (operator names, e-mail) and are never committed. The folder is
    /// named by the environment variable <c>STANFORD_HPR_FOLDER</c>; its <c>*.hpr</c>, <c>*.hpr.xml</c> and
    /// <c>*.hpr.gz</c> files are read. Tests using them are marked <see cref="RealDocumentFactAttribute"/>,
    /// skipped when the variable is not set.
    /// </remarks>
    public static class RealDocuments
    {
        /// <summary>
        /// The environment variable naming the folder
        /// </summary>
        public const string FolderVariable = "STANFORD_HPR_FOLDER";

        /// <summary>
        /// The folder, or null when not set or missing
        /// </summary>
        public static string? Folder =>
            Environment.GetEnvironmentVariable(FolderVariable) is { Length: > 0 } folder && Directory.Exists(folder) ? folder : null;

        /// <summary>
        /// Every real Harvesting document in the folder, by file name, as text
        /// </summary>
        public static IEnumerable<(string Name, string Xml)> Read()
        {
            if (Folder is not { } folder)
            {
                yield break;
            }

            foreach (string path in Directory.EnumerateFiles(folder).Order(StringComparer.Ordinal))
            {
                string name = Path.GetFileName(path);

                if (name.EndsWith(".hpr.gz", StringComparison.OrdinalIgnoreCase))
                {
                    using GZipStream gzip = new(File.OpenRead(path), CompressionMode.Decompress);
                    using StreamReader reader = new(gzip, Encoding.UTF8);
                    yield return (name, reader.ReadToEnd());
                }
                else if (name.EndsWith(".hpr", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".hpr.xml", StringComparison.OrdinalIgnoreCase))
                {
                    yield return (name, File.ReadAllText(path, Encoding.UTF8));
                }
            }
        }
    }

    /// <summary>
    /// A test reading real documents: skipped when <c>STANFORD_HPR_FOLDER</c> is not set
    /// </summary>
    public sealed class RealDocumentFactAttribute : FactAttribute
    {
        public RealDocumentFactAttribute()
        {
            if (RealDocuments.Folder is null)
            {
                Skip = $"{RealDocuments.FolderVariable} is not set to a folder of real Harvesting documents.";
            }
        }
    }
}
