using System;
using System.Runtime.Serialization;
using JetBrains.Annotations;

namespace LibMMD.Unity3D.BonePose
{
    public class BonePoseFileFormatException:Exception
    {
        public BonePoseFileFormatException()
        {
        }

        public BonePoseFileFormatException(string message) : base(message)
        {
        }

        protected BonePoseFileFormatException([NotNull] SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public BonePoseFileFormatException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}