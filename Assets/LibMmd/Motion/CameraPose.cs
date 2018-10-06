using UnityEngine;

namespace LibMMD.Motion
{
    public class CameraPose
    {
        public float Fov{get;set;}
        public float FocalLength{get;set;}
        public Vector3 Position{get;set;}
        public Vector3 Rotation{get;set;}
        public bool Orthographic{get;set;}
    }
}