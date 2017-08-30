using BulletSharp;
using BulletSharp.Math;
using LibMMD.Model;
using LibMMD.Motion;
using UnityEngine;
using MathUtil = LibMMD.Util.MathUtil;

namespace LibMMD.Pyhsics
{
    public class PoserMotionState : MotionState
    {
        private static readonly BoneImage NullBoneImage = new BoneImage();
        private readonly Poser _poser;
        private readonly bool _passive;
        private readonly bool _strict;
        private readonly bool _ghost;
        private readonly BoneImage _target;
        private Matrix _transform;
        private readonly Matrix _bodyTransform;
        private readonly Matrix _bodyTransformInv;


        public PoserMotionState(Poser poser, MmdRigidBody body, Matrix bodyTransform)
        {
            _poser = poser;
            _passive = body.Type == MmdRigidBody.RigidBodyType.RigidTypeKinematic;
            _strict = body.Type == MmdRigidBody.RigidBodyType.RigidTypePhysicsStrict;
            _ghost = body.Type == MmdRigidBody.RigidBodyType.RigidTypePhysicsGhost;
            _target = GetPoserBoneImage(poser, body.AssociatedBoneIndex);
            _bodyTransform = bodyTransform;
            _bodyTransformInv = Matrix.Invert(bodyTransform);

            Reset();
        }


        public override void GetWorldTransform(out Matrix worldTrans)
        {
            if (_passive)
            {
                Reset();
            }
            worldTrans = _transform;
        }

        public override void SetWorldTransform(ref Matrix worldTrans)
        {
            if (!_passive)
            {
                _transform = worldTrans;
            }
        }

        public void Synchronize()
        {
            if (_passive || _ghost) return;
            var btTransform = _bodyTransformInv * _transform;
            MathUtil.BulletMatrixToUnityMatrix(btTransform, ref _target.SkinningMatrix);
        }


        public void Fix()
        {
            if (!_strict) return;
            var parentLocalMatrix = new Matrix4x4();
            _target.LocalMatrix = _target.SkinningMatrix * _target.GlobalOffsetMatrixInv;
            if (_target.HasParent)
            {
                parentLocalMatrix = BulletPyhsicsReactor.GetPoserBoneImage(_poser, _target.Parent).LocalMatrix;
                _target.LocalMatrix = parentLocalMatrix.inverse * _target.LocalMatrix;
            }
            MathUtil.SetTransToMatrix4X4(_target.TotalTranslation + _target.LocalOffset, ref _target.LocalMatrix);
            if (_target.HasParent)
            {
                _target.LocalMatrix = parentLocalMatrix * _target.LocalMatrix;
            }
            _target.SkinningMatrix = _target.LocalMatrix * _target.GlobalOffsetMatrix;
        }

        public void Reset()
        {
            MathUtil.UnityMatrixToBulletMatrix(_target.SkinningMatrix, ref _transform);
            _transform = _bodyTransform * _transform;
            //Debug.Log("bodyTransform = " + _bodyTransform + ", SkinningMatrix = " + _target.SkinningMatrix + ", _transform = " + _transform );
        }

        protected static BoneImage GetPoserBoneImage(Poser poser, int index)
        {
            return index >= poser.BoneImages.Length ? NullBoneImage : poser.BoneImages[index];
        }
    }
}