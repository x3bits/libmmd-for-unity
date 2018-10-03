using System;
using System.Collections.Generic;
using System.Linq;
using LibMMD.Model;
using LibMMD.Util;
using UnityEngine;
using Tools = LibMMD.Util.Tools;

namespace LibMMD.Motion
{
    public class Poser
    {
        public Poser(MmdModel model)
        {
            Model = model;
            /***** Create Pose Image *****/
            var vertexNum = model.Vertices.Length;
            //VertexImages = new Vector3[vertexNum];
            var boneNum = model.Bones.Length;
            BoneImages = new BoneImage[boneNum];
            for (var i = 0; i < boneNum; ++i)
            {
                BoneImages[i] = new BoneImage();
            }


            for (var i = 0; i < boneNum; ++i)
            {
                var bone = model.Bones[i];
                _boneNameMap[bone.Name] = i;
                var image = BoneImages[i];
                image.GlobalOffsetMatrix.m03 = -bone.Position[0];
                image.GlobalOffsetMatrix.m13 = -bone.Position[1];
                image.GlobalOffsetMatrix.m23 = -bone.Position[2];
                image.GlobalOffsetMatrixInv.m03 = bone.Position[0];
                image.GlobalOffsetMatrixInv.m13 = bone.Position[1];
                image.GlobalOffsetMatrixInv.m23 = bone.Position[2];
                image.Parent = bone.ParentIndex;
                if (image.Parent < boneNum && image.Parent >= 0)
                {
                    image.HasParent = true;
                    image.LocalOffset = bone.Position - model.Bones[image.Parent].Position;
                }
                else
                {
                    image.HasParent = false;
                    image.LocalOffset = bone.Position;
                }

                image.AppendRotate = bone.AppendRotate;
                image.AppendTranslate = bone.AppendTranslate;
                image.HasAppend = false;

                if (image.AppendRotate || image.AppendTranslate)
                {
                    image.AppendParent = bone.AppendBoneVal.Index;
                    if (image.AppendParent < boneNum)
                    {
                        image.HasAppend = true;
                        image.AppendRatio = bone.AppendBoneVal.Ratio;
                    }
                }


                image.HasIk = bone.HasIk;
                if (image.HasIk)
                {
                    var ikLinkNum = bone.IkInfoVal.IkLinks.Length;
                    image.IkLinks = new int[ikLinkNum];
                    image.IkFixTypes = new BoneImage.AxisFixType[ikLinkNum];
                    image.IkTransformOrders = Enumerable.Repeat(BoneImage.AxisTransformOrder.OrderYzx, ikLinkNum)
                        .ToArray();
                    image.IkLinkLimited = new bool [ikLinkNum];
                    image.IkLinkLimitsMin = new Vector3[ikLinkNum];
                    image.IkLinkLimitsMax = new Vector3[ikLinkNum];

                    for (var j = 0; j < ikLinkNum; ++j)
                    {
                        var ikLink = bone.IkInfoVal.IkLinks[j];
                        image.IkLinks[j] = ikLink.LinkIndex;
                        image.IkLinkLimited[j] = ikLink.HasLimit;
                        if (image.IkLinkLimited[j])
                        {
                            for (var k = 0; k < 3; ++k)
                            {
                                var lo = ikLink.LoLimit[k];
                                var hi = ikLink.HiLimit[k];
                                image.IkLinkLimitsMin[j][k] = Math.Min(lo, hi);
                                image.IkLinkLimitsMax[j][k] = Math.Max(lo, hi);
                            }
                            if (image.IkLinkLimitsMin[j].x > -Math.PI * 0.5 &&
                                image.IkLinkLimitsMax[j].x < Math.PI * 0.5)
                            {
                                image.IkTransformOrders[j] = BoneImage.AxisTransformOrder.OrderZxy;
                            }
                            else if (image.IkLinkLimitsMin[j].y > -Math.PI * 0.5 &&
                                     image.IkLinkLimitsMax[j].y < Math.PI * 0.5)
                            {
                                image.IkTransformOrders[j] = BoneImage.AxisTransformOrder.OrderXyz;
                            }
                            if (Math.Abs(image.IkLinkLimitsMin[j].x) < Tools.MmdMathConstEps &&
                                Math.Abs(image.IkLinkLimitsMax[j].x) < Tools.MmdMathConstEps
                                && Math.Abs(image.IkLinkLimitsMin[j].y) < Tools.MmdMathConstEps &&
                                Math.Abs(image.IkLinkLimitsMax[j].y) < Tools.MmdMathConstEps
                                && Math.Abs(image.IkLinkLimitsMin[j].z) < Tools.MmdMathConstEps &&
                                Math.Abs(image.IkLinkLimitsMax[j].z) < Tools.MmdMathConstEps)
                            {
                                image.IkFixTypes[j] = BoneImage.AxisFixType.FixAll;
                            }
                            else if (Math.Abs(image.IkLinkLimitsMin[j].y) < Tools.MmdMathConstEps &&
                                     Math.Abs(image.IkLinkLimitsMax[j].y) < Tools.MmdMathConstEps
                                     && Math.Abs(image.IkLinkLimitsMin[j].z) < Tools.MmdMathConstEps &&
                                     Math.Abs(image.IkLinkLimitsMax[j].z) < Tools.MmdMathConstEps)
                            {
                                image.IkFixTypes[j] = BoneImage.AxisFixType.FixX;
                            }
                            else if (Math.Abs(image.IkLinkLimitsMin[j].x) < Tools.MmdMathConstEps &&
                                     Math.Abs(image.IkLinkLimitsMax[j].x) < Tools.MmdMathConstEps &&
                                     Math.Abs(image.IkLinkLimitsMin[j].z) < Tools.MmdMathConstEps &&
                                     Math.Abs(image.IkLinkLimitsMax[j].z) < Tools.MmdMathConstEps)
                            {
                                image.IkFixTypes[j] = BoneImage.AxisFixType.FixY;
                            }
                            else if (Math.Abs(image.IkLinkLimitsMin[j].x) < Tools.MmdMathConstEps &&
                                     Math.Abs(image.IkLinkLimitsMax[j].x) < Tools.MmdMathConstEps
                                     && Math.Abs(image.IkLinkLimitsMin[j].y) < Tools.MmdMathConstEps &&
                                     Math.Abs(image.IkLinkLimitsMax[j].y) < Tools.MmdMathConstEps)
                            {
                                image.IkFixTypes[j] = BoneImage.AxisFixType.FixZ;
                            }
                        }
                        BoneImages[image.IkLinks[j]].IkLink = true;
                    }
                    image.CcdAngleLimit = bone.IkInfoVal.CcdAngleLimit;
                    image.CcdIterateLimit = Math.Min(bone.IkInfoVal.CcdIterateLimit, 256);
                    image.IkTarget = bone.IkInfoVal.IkTargetIndex;
                }
                if (bone.PostPhysics)
                {
                    _postPhysicsBones.Add(i);
                }
                else
                {
                    _prePhysicsBones.Add(i);
                }
            }
            BoneImage.TransformOrder order = new BoneImage.TransformOrder(model);
            _prePhysicsBones.Sort(order);
            _postPhysicsBones.Sort(order);

            var materialNum = model.Parts.Length;
            MaterialMulImages = new MaterialImage[materialNum];
            MaterialAddImages = new MaterialImage[materialNum];
            for (var i = 0; i < materialNum; ++i)
            {
                MaterialMulImages[i] = new MaterialImage(1.0f);
                MaterialAddImages[i] = new MaterialImage(0.0f);
            }

            var morphNum = model.Morphs.Length;
            MorphRates = new float[morphNum];
            for (var i = 0; i < morphNum; ++i)
            {
                var morph = model.Morphs[i];
                _morphNameMap[morph.Name] = i;
            }

            ResetPosing();
            //Debug.LogFormat("morphIndex count = {0}, vertex count = {1}", morphIndex.Count, model.Vertices.Length);
            //Debug.LogFormat("morphIndex count = {0}, vertex count = {1}", morphIndex.Count, model.Vertices.Length);
        }

        private static List<int> GetMorphIndexes(MmdModel model)
        {
            var morphs = model.Morphs;
            var indexSet = new HashSet<int>();
            foreach (var morph in morphs)
            {
                if (morph.Type != Morph.MorphType.MorphTypeVertex)
                {
                    continue;
                }
                foreach (var morphData in morph.MorphDatas)
                {
                    var data = (Morph.VertexMorph) morphData;
                    indexSet.Add(data.VertexIndex);
                }
            }
            var ret = new List<int>(indexSet);
            ret.Sort();
            return ret;
        }

        public void SetBonePose(int index, BonePose bonePose)
        {
            BoneImages[index].Translation = bonePose.Translation;
            BoneImages[index].Rotation = bonePose.Rotation;
        }

        public void SetBonePose(string name, BonePose bonePose)
        {
            int index;
            if (_boneNameMap.TryGetValue(name, out index))
            {
                SetBonePose(index, bonePose);
            }
        }

        public void SetMorphPose(int index, MorphPose morphPose)
        {
            MorphRates[index] = morphPose.Weight;
        }

        public void SetMorphPose(string name, MorphPose morphPose)
        {
            int index;
            if (_morphNameMap.TryGetValue(name, out index))
            {
                SetMorphPose(index, morphPose);
            }
        }

        private void UpdateBoneAppendTransform(int index)
        {
            var image = BoneImages[index];
            if (!image.HasAppend) return;
            if (image.AppendRotate)
            {
                image.TotalRotation = image.TotalRotation *
                                      Quaternion.Slerp(Quaternion.identity,
                                          BoneImages[image.AppendParent].TotalRotation, image.AppendRatio);
            }
            if (image.AppendTranslate)
            {
                image.TotalTranslation = image.TotalTranslation +
                                         image.AppendRatio * BoneImages[image.AppendParent].TotalTranslation;
            }
            
            UpdateLocalMatrix(image);
        }

        private void UpdateLocalMatrix(BoneImage image)
        {
            image.LocalMatrix = MathUtil.QuaternionToMatrix4X4(image.TotalRotation);
            MathUtil.SetTransToMatrix4X4(image.TotalTranslation + image.LocalOffset, ref image.LocalMatrix);

            if (image.HasParent)
            {
                image.LocalMatrix = BoneImages[image.Parent].LocalMatrix * image.LocalMatrix;
            }
        }

        private void UpdateBoneSelfTransform(int index)
        {
            var image = BoneImages[index];
            image.TotalRotation = image.MorphRotation * image.Rotation;
            image.TotalTranslation = image.MorphTranslation + image.Translation;

            if (image.IkLink)
            {
                image.PreIkRotation = image.TotalRotation;
                image.TotalRotation = image.IkRotation * image.TotalRotation;
            }

            UpdateLocalMatrix(image);

            if (!image.HasIk) return;
            var ikLinkNum = image.IkLinks.Length;
            for (var i = 0; i < ikLinkNum; ++i)
            {
                BoneImages[image.IkLinks[i]].IkRotation = Quaternion.identity;
            }
            var ikPosition = MathUtil.GetTransFromMatrix4X4(image.LocalMatrix);
            for (var i = 0; i < ikLinkNum; ++i)
            {
                UpdateBoneSelfTransform(image.IkTarget);
            }
            var targetPosition = MathUtil.GetTransFromMatrix4X4(BoneImages[image.IkTarget].LocalMatrix);
            var ikError = ikPosition - targetPosition;
            if (Vector3.Dot(ikError, ikError) < Tools.MmdMathConstEps)
            {
                return;
            }
            var ikt = image.CcdIterateLimit / 2;
            for (var i = 0; i < image.CcdIterateLimit; ++i)
            {
                for (var j = 0; j < ikLinkNum; ++j)
                {
                    if (image.IkFixTypes[j] == BoneImage.AxisFixType.FixAll) continue;
                    var ikImage = BoneImages[image.IkLinks[j]];
                    var ikLinkPosition = MathUtil.GetTransFromMatrix4X4(ikImage.LocalMatrix);
                    var targetDirection = ikLinkPosition - targetPosition;
                    var ikDirection = ikLinkPosition - ikPosition;

                    targetDirection.Normalize();
                    ikDirection.Normalize();

                    var ikRotateAxis = Vector3.Cross(targetDirection, ikDirection);
                    for (var k = 0; k < 3; ++k)
                    {
                        if (Math.Abs(ikRotateAxis[k]) < Tools.MmdMathConstEps)
                        {
                            ikRotateAxis[k] = Tools.MmdMathConstEps;
                        }
                    }
                    var localizationMatrix = ikImage.HasParent ? BoneImages[ikImage.Parent].LocalMatrix : Matrix4x4.identity;
                    if (image.IkLinkLimited[j] && image.IkFixTypes[j] != BoneImage.AxisFixType.FixNone &&
                        i < ikt)
                    {
                        switch (image.IkFixTypes[j])
                        {
                            case BoneImage.AxisFixType.FixX:
                                ikRotateAxis.x =
                                    Nabs(Vector3.Dot(ikRotateAxis,
                                        MathUtil.Matrix4x4ColDowngrade(localizationMatrix, 0)));
                                ikRotateAxis.y = ikRotateAxis.z = 0.0f;
                                break;
                            case BoneImage.AxisFixType.FixY:
                                ikRotateAxis.y =  
                                    Nabs(Vector3.Dot(ikRotateAxis,
                                        MathUtil.Matrix4x4ColDowngrade(localizationMatrix, 1)));
                                ikRotateAxis.x = ikRotateAxis.z = 0.0f;
                                break;
                            case BoneImage.AxisFixType.FixZ:
                                ikRotateAxis.z =
                                    Nabs(Vector3.Dot(ikRotateAxis,
                                        MathUtil.Matrix4x4ColDowngrade(localizationMatrix, 2)));
                                ikRotateAxis.x = ikRotateAxis.y = 0.0f;
                                break;
                        }
                    }
                    else
                    { 
                        ikRotateAxis = Matrix4x4.Transpose(localizationMatrix).MultiplyVector(ikRotateAxis);
                        ikRotateAxis.Normalize();
                    }
                    var ikRotateAngle =
                        Mathf.Min(Mathf.Acos(Mathf.Clamp(Vector3.Dot(targetDirection, ikDirection), -1.0f, 1.0f)),
                            image.CcdAngleLimit * (j + 1));
                    ikImage.IkRotation =
                        Quaternion.AngleAxis((float) (ikRotateAngle / Math.PI * 180.0), ikRotateAxis) * ikImage.IkRotation;
                    if (image.IkLinkLimited[j])
                    {
                        var localRotation = ikImage.IkRotation * ikImage.PreIkRotation;
                        switch (image.IkTransformOrders[j])
                        {
                            case BoneImage.AxisTransformOrder.OrderZxy:
                            {
                                var eularAngle = MathUtil.QuaternionToZxy(localRotation);
                                eularAngle = LimitEularAngle(eularAngle, image.IkLinkLimitsMin[j],
                                    image.IkLinkLimitsMax[j], i < ikt);
                                localRotation = MathUtil.ZxyToQuaternion(eularAngle);
                                break;
                            }
                            case BoneImage.AxisTransformOrder.OrderXyz:
                            {
                                var eularAngle = MathUtil.QuaternionToXyz(localRotation);
                                eularAngle = LimitEularAngle(eularAngle, image.IkLinkLimitsMin[j],
                                    image.IkLinkLimitsMax[j], i < ikt);
                                localRotation = MathUtil.XyzToQuaternion(eularAngle);
                                break;
                            }
                            case BoneImage.AxisTransformOrder.OrderYzx:
                            {
                                var eularAngle = MathUtil.QuaternionToYzx(localRotation);
                                eularAngle = LimitEularAngle(eularAngle, image.IkLinkLimitsMin[j],
                                    image.IkLinkLimitsMax[j], i < ikt);
                                localRotation = MathUtil.YzxToQuaternion(eularAngle);
                                break;
                            }
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                        ikImage.IkRotation = localRotation * Quaternion.Inverse(image.PreIkRotation);
                    }
                    for (var k = 0; k <= j; ++k)
                    {
                        var linkImage = BoneImages[image.IkLinks[j - k]];
                        linkImage.TotalRotation = linkImage.IkRotation * linkImage.PreIkRotation;
                        UpdateLocalMatrix(linkImage);
                    }
                    UpdateBoneSelfTransform(image.IkTarget);
                    targetPosition = MathUtil.Matrix4x4ColDowngrade(BoneImages[image.IkTarget].LocalMatrix, 3);
                }
                ikError = ikPosition - targetPosition;
                if (Vector3.Dot(ikError, ikError) < Tools.MmdMathConstEps)
                {
                    return;
                }
            }
        }

        private static float Nabs(float x)
        {
            if (x > 0.0f)
            {
                return 1.0f;
            }
            return -1.0f;
        }

        private static Vector3 LimitEularAngle(Vector3 eular, Vector3 eularMin, Vector3 eularMax, bool ikt)
        {
            var result = eular;
            for (var i = 0; i < 3; ++i)
            {
                if (result[i] < eularMin[i])
                {
                    var tf = 2 * eularMin[i] - result[i];
                    if (tf <= eularMax[i] && ikt)
                    {
                        result[i] = tf;
                    }
                    else
                    {
                        result[i] = eularMin[i];
                    }
                }
                if (result[i] > eularMax[i])
                {
                    var tf = 2 * eularMax[i] - result[i];
                    if (tf >= eularMin[i] && ikt)
                    {
                        result[i] = tf;
                    }
                    else
                    {
                        result[i] = eularMax[i];
                    }
                }
            }
            return result;
        }

        public void ResetPosing()
        {
            for (var i = 0; i < MorphRates.Length; ++i)
            {
                MorphRates[i] = 0.0f;
            }
            foreach (var boneImage in BoneImages)
            {
                boneImage.Rotation = new Quaternion(0.0f, 0.0f, 0.0f, 1.0f);
                boneImage.Translation = Vector4.zero;
            }

            PrePhysicsPosing();
            PostPhysicsPosing();
        }

        public void PrePhysicsPosing(bool calculateMorph = true)
        {
            foreach (var boneImage in BoneImages)
            {
                boneImage.MorphTranslation = Vector3.zero;
                boneImage.MorphRotation = Quaternion.identity;
                boneImage.LocalMatrix = Matrix4x4.identity;
                boneImage.PreIkRotation = Quaternion.identity;
                boneImage.IkRotation = Quaternion.identity;
                boneImage.TotalRotation = Quaternion.identity;
                boneImage.TotalTranslation = Vector3.zero;
            }
            foreach (var materialMulImage in MaterialMulImages)
            {
                materialMulImage.Init(1.0f);
            }
            foreach (var materialAddImage in MaterialAddImages)
            {
                materialAddImage.Init(0.0f);
            }
            if (calculateMorph)
            {
                for (var i = 0; i < MorphRates.Length; ++i)
                {
                    UpdateMorphTransform(i, MorphRates[i]);
                }
            }
            UpdateBoneTransform(_prePhysicsBones);
            UpdateBoneSkinningMatrix(_prePhysicsBones);
        }

        private void UpdateBoneSkinningMatrix(List<int> indexList)
        {
            foreach (var index in indexList)
            {
                var image = BoneImages[index];
                image.SkinningMatrix = image.LocalMatrix * image.GlobalOffsetMatrix;
            }
        }

        private void UpdateBoneTransform(List<int> indexList)
        {
            foreach (var index in indexList)
            {
                UpdateBoneSelfTransform(index);
            }
            foreach (var index in indexList)
            {
                UpdateBoneAppendTransform(index);
            }
            foreach (var index in indexList)
            {
                UpdateLocalMatrix(BoneImages[index]);
            }
        }

        private void UpdateMorphTransform(int index, float rate)
        {
            if (rate < Tools.MmdMathConstEps)
            {
                return;
            }
            var morph = Model.Morphs[index];
            switch (morph.Type)
            {
                case Morph.MorphType.MorphTypeGroup:
                    foreach (var morphData in morph.MorphDatas)
                    {
                        var data = (Morph.GroupMorph) morphData;
                        UpdateMorphTransform(data.MorphIndex, data.MorphRate * rate);
                    }
                    break;
                case Morph.MorphType.MorphTypeVertex:
                    //顶点Morph分开计算
                    break;
                case Morph.MorphType.MorphTypeBone:
                    foreach (var morphData in morph.MorphDatas)
                    {
                        var data = (Morph.BoneMorph) morphData;
                        var boneImage = BoneImages[data.BoneIndex];
                        boneImage.MorphTranslation = boneImage.MorphTranslation + data.Translation * rate;
                        boneImage.MorphRotation = boneImage.MorphRotation *
                                                  Quaternion.Slerp(Quaternion.identity, data.Rotation, rate);
                    }
                    break;
                case Morph.MorphType.MorphTypeUv:
                    break;
                case Morph.MorphType.MorphTypeExtUv1:
                    break;
                case Morph.MorphType.MorphTypeExtUv2:
                    break;
                case Morph.MorphType.MorphTypeExtUv3:
                    break;
                case Morph.MorphType.MorphTypeExtUv4:
                    break;
                case Morph.MorphType.MorphTypeMaterial:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void PostPhysicsPosing()
        {
            UpdateBoneTransform(_postPhysicsBones);
            UpdateBoneSkinningMatrix(_postPhysicsBones);
        }

        //public Vector3[] VertexImages { get; set; }
        public BoneImage[] BoneImages { get; set; }
        public MaterialImage[] MaterialMulImages { get; set; }
        public MaterialImage[] MaterialAddImages { get; set; }

        public float[] MorphRates { get; set; }

        public MmdModel Model { get; set; }

        private readonly Dictionary<string, int> _boneNameMap = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _morphNameMap = new Dictionary<string, int>();

        private readonly List<int> _prePhysicsBones = new List<int>();
        private readonly List<int> _postPhysicsBones = new List<int>();
    }
}