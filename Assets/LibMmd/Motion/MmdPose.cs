using System.Collections.Generic;

namespace LibMMD.Motion
{
    public class MmdPose
    {
        private Dictionary<string, BonePose> _bonePoses = new Dictionary<string, BonePose>();

        public string ModelName { get; set; }

        public Dictionary<string, BonePose> BonePoses
        {
            get { return _bonePoses; }
            set { _bonePoses = value; }
        }
        

    }
}