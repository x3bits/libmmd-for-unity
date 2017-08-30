using UnityEngine;

namespace LibMMD.Motion
{
    public class BoneKeyframe
    {
        public Vector3 Translation { get; set; }
        public Quaternion Rotation { get; set; }

        public Interpolator XInterpolator { get; set; }
        public Interpolator YInterpolator { get; set; }
        public Interpolator ZInterpolator { get; set; }
        public Interpolator RInterpolator { get; set; }

    }
}