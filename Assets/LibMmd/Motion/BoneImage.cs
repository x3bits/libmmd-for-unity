using System.Collections.Generic;
using LibMMD.Model;
using UnityEngine;

namespace LibMMD.Motion
{
    public class BoneImage
    {
        public BoneImage()
        {
            Rotation = Quaternion.identity;
            Translation = Vector3.zero;
            MorphRotation = Quaternion.identity;
            MorphTranslation = Vector3.zero;
            GlobalOffsetMatrix = Matrix4x4.identity;
            GlobalOffsetMatrixInv = Matrix4x4.identity;
        }

        public Quaternion Rotation { get; set; }
        public Vector3 Translation { get; set; }

        public Quaternion MorphRotation { get; set; }
        public Vector3 MorphTranslation { get; set; }

        public bool HasParent { get; set; }
        public int Parent { get; set; }

        public bool HasAppend { get; set; }
        public bool AppendRotate { get; set; }
        public bool AppendTranslate { get; set; }

        public int AppendParent { get; set; }
        public float AppendRatio { get; set; }

        public bool HasIk { get; set; }
        public bool IkLink { get; set; }

        public float CcdAngleLimit { get; set; }
        public int CcdIterateLimit { get; set; }

        public int[] IkLinks { get; set; }

        public enum AxisFixType
        {
            FixNone,
            FixX,
            FixY,
            FixZ,
            FixAll
        }

        public enum AxisTransformOrder
        {
            OrderZxy,
            OrderXyz,
            OrderYzx
        }

        public AxisFixType[] IkFixTypes { get; set; }
        public AxisTransformOrder[] IkTransformOrders { get; set; }

        public bool[] IkLinkLimited { get; set; }
        public Vector3[] IkLinkLimitsMin { get; set; }
        public Vector3[] IkLinkLimitsMax { get; set; }

        public int IkTarget { get; set; }

        public Quaternion PreIkRotation { get; set; }
        public Quaternion IkRotation { get; set; }

        public Quaternion TotalRotation { get; set; }
        public Vector3 TotalTranslation { get; set; }

        public Vector3 LocalOffset { get; set; }

        public Matrix4x4 GlobalOffsetMatrix;
        public Matrix4x4 GlobalOffsetMatrixInv;
        public Matrix4x4 LocalMatrix;

        public Matrix4x4 SkinningMatrix;

        public class TransformOrder : IComparer<int>
        {
            public TransformOrder(MmdModel model)
            {
                _model = model;
            }

            public int Compare(int a, int b)
            {
                if (_model.Bones[a].TransformLevel == _model.Bones[b].TransformLevel)
                {
                    return a.CompareTo(b);
                }
                return _model.Bones[a].TransformLevel.CompareTo(_model.Bones[b].TransformLevel);
            }

            private readonly MmdModel _model;
        };
    }
}