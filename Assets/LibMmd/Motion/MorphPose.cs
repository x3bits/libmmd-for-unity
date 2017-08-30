namespace LibMMD.Motion
{
    public class MorphPose
    {
        public MorphPose()
        {
        }

        public MorphPose(float weight)
        {
            Weight = weight;
        }

        public float Weight { get; set; }
    }
}