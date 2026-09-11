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
        /// Line number when the exception was thrown
        /// </summary>
        public long? LineNumber { get; internal set; }

        /// <summary>
        /// Line position i.e. byte count when the exception was thrown
        /// </summary>
        public long? LinePosition { get; internal set; }

        /// <summary>
        /// XML path where the exception was thrown
        /// </summary>
        public string? Path { get; internal set; }

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
        /// <param name="path"></param>
        /// <param name="lineNumber"></param>
        /// <param name="linePosition"></param>
        /// <param name="innerException"></param>
        public XmlException(
            string? message, 
            string? path, 
            long? lineNumber, 
            long? linePosition, 
            Exception? innerException
        ) : base(message, innerException)
        {
            _message = message;
            LineNumber = lineNumber;
            LinePosition = linePosition;
            Path = path;
        }

        /// <summary>
        /// All optional properties starting the stack trace from the throw
        /// </summary>
        /// <param name="message"></param>
        /// <param name="path"></param>
        /// <param name="lineNumber"></param>
        /// <param name="linePosition"></param>
        public XmlException(
            string? message, 
            string? path,
            long? lineNumber, 
            long? linePosition
        ) : base(message)
        {
            _message = message;
            LineNumber = lineNumber;
            LinePosition = linePosition;
            Path = path;
        }

        /// <summary>
        /// All optional properties except path starting the stack trace from the throw
        /// </summary>
        /// <param name="message"></param>
        /// <param name="lineNumber"></param>
        /// <param name="linePosition"></param>
        public XmlException(
            string? message, 
            long? lineNumber, 
            long? linePosition
        ) : base(message)
        {
            _message = message;
            LineNumber = lineNumber;
            LinePosition = linePosition;
        }
    }
}