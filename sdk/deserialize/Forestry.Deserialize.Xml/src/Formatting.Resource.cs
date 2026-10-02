namespace Forestry.Deserialize.Xml
{
    internal static partial class Formatting
    {
        #region xml exception
        internal static string WhenDocumentHasNoRootElement => Deserialize.Formatting.GetResourceString(nameof(WhenDocumentHasNoRootElement), "Malformed markup: reading is completed without a root element");

        internal static string WhenDocumentHasElementNotEnded => Deserialize.Formatting.GetResourceString(nameof(WhenDocumentHasElementNotEnded), "Malformed markup: reading is completed without an ending terminal or end tag for an element non-terminal");

        internal static string WhenDocumentHasNoNextToken => Deserialize.Formatting.GetResourceString(nameof(WhenDocumentHasNoNextToken), "Malformed markup: reading is completed without advancing to the next token");

        internal static string WhenNoNameAfterElementStartTerminal => Deserialize.Formatting.GetResourceString(nameof(WhenNoNameAfterElementStartTerminal), @"Malformed markup: characters {0} are an invalid name non-terminal");

        internal static string WhenEndingTerminalMissing => Deserialize.Formatting.GetResourceString(nameof(WhenEndingTerminalMissing), @"Malformed markup: no ending terminal {0} before reading completed");

        internal static string WhenDocumentTypeRepeated => Deserialize.Formatting.GetResourceString(nameof(WhenDocumentTypeRepeated), @"Malformed markup: more than one document type in the prolog");

        internal static string WhenDeclarationNotFirst => Deserialize.Formatting.GetResourceString(nameof(WhenDeclarationNotFirst), @"Malformed markup: the declaration is not the first characters in the document");

        internal static string WhenProcessingInstructionTargetMalformed => Deserialize.Formatting.GetResourceString(nameof(WhenProcessingInstructionTargetMalformed), @"Malformed markup: the processing instruction target is not a name, or is 'xml' in any case");
        #endregion

        #region invalid operation exception
        internal static string WhenNotPositive => Deserialize.Formatting.GetResourceString(nameof(WhenNotPositive), @"Value {0} named {1} is not positive");
        #endregion
    }
}