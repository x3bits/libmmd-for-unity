using System;
using System.Runtime.Serialization;
using JetBrains.Annotations;

namespace LibMMD.Unity3D.BonePose
{
    public class BonePoseNotSuitableException : Exception
    {
        public BonePoseNotSuitableException()
        {
        }

        public BonePoseNotSuitableException(string message) : base(message)
        {
        }

        protected BonePoseNotSuitableException([NotNull] SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public BonePoseNotSuitableException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}