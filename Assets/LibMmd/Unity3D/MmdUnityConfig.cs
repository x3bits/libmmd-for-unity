using System;

namespace LibMMD.Unity3D
{
    public class MmdUnityConfig
    {
        public MmdConfigSwitch EnableDrawSelfShadow;
        public MmdConfigSwitch EnableCastShadow;
        public MmdConfigSwitch EnableEdge;
        
        public static bool DealSwitch(MmdConfigSwitch switchVal, bool configVal)
        {
            switch (switchVal)
            {
                case MmdConfigSwitch.AsConfig:
                    return configVal;
                case MmdConfigSwitch.ForceTrue:
                    return true;
                case MmdConfigSwitch.ForceFalse:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException("switchVal", switchVal, null);
            }
        }
    }
}