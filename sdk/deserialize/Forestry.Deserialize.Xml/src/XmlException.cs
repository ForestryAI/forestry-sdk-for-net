namespace Forestry.Deserialize.Xml
{
    /// <summary>
    /// Thrown when an expectation on the next terminal or non-terminal fails
    /// or reader optioons fail e.g. the current depth exceeds the maximum 
    /// element stack size
    /// </summary>
    [Serializable]
    public class XmlException: Exception
    {
        /// <summary>
        /// XML exception category
        /// </summary>
        public string? Category { get; internal set; }

        /// <summary>
        /// XML exception markup
        /// </summary>
        public string? Markup { get; internal set; }

        /// <summary>
        /// Line number when the exception was thrown
        /// </summary>
        public long? LineNumber { get; internal set; }

        /// <summary>
        /// Line position i.e. byte count when the exception was thrown
        /// </summary>
        public long? LinePosition { get; internal set; }

        /// <summary>
        /// Overrides getting the message from the constructor or if not set from 
        /// the base exception
        /// </summary>
        public override string Message
        {
            get
            {
                return _message ?? base.Message;
            }
        }

        /// <summary>
        /// Avoid re-throwing by mutating the message i.e. keep stack trace without loosing inner exception
        /// </summary>
        /// <param name="message"></param>
        internal void SetMessage(string? message)
        {
            _message = message;
        }

        internal string? _message;

        /// <summary>
        /// All properties are optional but the message can be mutated interally
        /// </summary>
        public XmlException() : base() { }

        /// <summary>
        /// Message and preserving the stack-trace with the inner exception
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public XmlException(
            string? message, 
            Exception? innerException
        ) : base(message, innerException)
        {
            _message = message;
        }

        /// <summary>
        /// All optional properties and preserving the stack-trace with the inner exception
        /// </summary>
        /// <param name="message"></param>
        /// <param name="category"></param>
        /// <param name="markup"></param>
        /// <param name="lineNumber"></param>
        /// <param name="linePosition"></param>
        /// <param name="innerException"></param>
        public XmlException(
            string? message, 
            string? category, 
            string? markup,
            long? lineNumber, 
            long? linePosition, 
            Exception? innerException
        ) : base(message, innerException)
        {
            _message = message;
            Category = category;
            Markup = markup;
            LineNumber = lineNumber;
            LinePosition = linePosition;            
        }

        /// <summary>
        /// All optional properties except path starting the stack trace from the throw
        /// </summary>
        /// <param name="message"></param>
        /// <param name="category"></param>
        /// <param name="markup"></param>
        /// <param name="lineNumber"></param>
        /// <param name="linePosition"></param>
        public XmlException(
            string? message, 
            string? category,
            string? markup,
            long? lineNumber, 
            long? linePosition
        ) : base(message)
        {
            _message = message;
            Category = category;
            Markup = markup;
            LineNumber = lineNumber;
            LinePosition = linePosition;
        }
    }
}