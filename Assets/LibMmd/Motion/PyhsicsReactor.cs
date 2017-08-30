using UnityEngine;
using Vector3 = BulletSharp.Math.Vector3;

namespace LibMMD.Motion
{
    public abstract class PyhsicsReactor
    {
        public abstract void AddPoser(Poser poser);
        public abstract void RemovePoser(Poser poser);
        public abstract void Reset();
        public abstract void React(float step, int maxSubSteps = 10 , float fixStepTime = 1.0f / 60.0f);

        public abstract void SetGravityStrength(float strength);
        public abstract void SetGravityDirection(Vector3 direction);

        public abstract float GetGravityStrength();
        public abstract Vector3 GetGravityDirection();

        public abstract void SetFloor(bool hasFloor);
        public abstract bool IsHasFloor();

        protected static BoneImage GetPoserImage(Poser poser, int index)
        {
            return poser.BoneImages[index];
        }
    }
}