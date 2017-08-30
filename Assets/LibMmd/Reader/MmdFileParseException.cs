using System;

namespace LibMMD.Reader
{
    public class MmdFileParseException : Exception
    {
        public MmdFileParseException(string message) : base(message)
        {
        }
    }
}