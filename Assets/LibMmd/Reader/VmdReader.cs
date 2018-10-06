using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibMMD.Motion;
using LibMMD.Util;
using UnityEngine;

namespace LibMMD.Reader
{
    public class VmdReader
    {
        public MmdMotion Read(string path)
        {
            using (var fileStream = new FileStream(path, FileMode.Open))
            {
                using (var bufferedStream = new BufferedStream(fileStream))
                {
                    using (var binaryReader = new BinaryReader(bufferedStream))
                    {
                        return Read(binaryReader);
                    }
                }
            }
        }

        public MmdMotion Read(BinaryReader reader)
        {
            var motion = new TempMmdMotion();
            var magic = MmdReaderUtil.ReadStringFixedLength(reader, 30, Tools.JapaneseEncoding);
            if (!"Vocaloid Motion Data 0002".Equals(magic))
            {
                throw new MmdFileParseException("File is not a VMD file.");
            }
            motion.Name = MmdReaderUtil.ReadStringFixedLength(reader, 20, Tools.JapaneseEncoding);

            var boneMotionNum = reader.ReadInt32();
            for (var i = 0; i < boneMotionNum; ++i)
            {
                var b = ReadVmdBone(reader);
                var keyFrame = motion.GetOrCreateBoneKeyFrame(b.BoneName, b.NFrame);
                keyFrame.Translation = b.Translation;
                keyFrame.Rotation = b.Rotation;

                Vector2 c0, c1;
                const float r = 1.0f / 127.0f;
                c0.x = b.XInterpolator[0] * r;
                c0.y = b.XInterpolator[4] * r;
                c1.x = b.XInterpolator[8] * r;
                c1.y = b.XInterpolator[12] * r;
                keyFrame.XInterpolator = new Interpolator();
                keyFrame.XInterpolator.SetC(c0, c1);

                c0.x = b.YInterpolator[0] * r;
                c0.y = b.YInterpolator[4] * r;
                c1.x = b.YInterpolator[8] * r;
                c1.y = b.YInterpolator[12] * r;
                keyFrame.YInterpolator = new Interpolator();
                keyFrame.YInterpolator.SetC(c0, c1);

                c0.x = b.ZInterpolator[0] * r;
                c0.y = b.ZInterpolator[4] * r;
                c1.x = b.ZInterpolator[8] * r;
                c1.y = b.ZInterpolator[12] * r;
                keyFrame.ZInterpolator = new Interpolator();
                keyFrame.ZInterpolator.SetC(c0, c1);

                c0.x = b.RInterpolator[0] * r;
                c0.y = b.RInterpolator[4] * r;
                c1.x = b.RInterpolator[8] * r;
                c1.y = b.RInterpolator[12] * r;
                keyFrame.RInterpolator = new Interpolator();
                keyFrame.RInterpolator.SetC(c0, c1);
            }

            var morphMotionNum = reader.ReadInt32();
            for (var i = 0; i < morphMotionNum; ++i)
            {
                var vmdMorph = ReadVmdMorph(reader);
                var keyFrame = motion.GetOrCreateMorphKeyFrame(vmdMorph.MorphName, vmdMorph.NFrame);
                keyFrame.Weight = vmdMorph.Weight;
                keyFrame.WInterpolator = new Interpolator();
            }

            //忽略后面的相机数据

            return motion.BuildMmdMotion();
        }
        
        public CameraMotion ReadCameraMotion(string path)
        {
            using (var fileStream = new FileStream(path, FileMode.Open))
            {
                using (var bufferedStream = new BufferedStream(fileStream))
                {
                    using (var binaryReader = new BinaryReader(bufferedStream))
                    {
                        return ReadCameraMotion(binaryReader);
                    }
                }
            }
        }

        public CameraMotion ReadCameraMotion(BinaryReader reader, bool motionReadAlready = false)
        {
            if (!motionReadAlready)
            {
                Read(reader);
            }
            var ret = new CameraMotion();
            var cameraMotionNum = reader.ReadInt32();
            Dictionary<int, CameraKeyframe> keyframes = new Dictionary<int, CameraKeyframe>();
            for(var i=0;i<cameraMotionNum;++i)
            {

                var nFrame = reader.ReadInt32();
                var focalLength = reader.ReadSingle();
                var position = MmdReaderUtil.ReadVector3(reader);
                var rotation = MmdReaderUtil.ReadVector3(reader);
                var interpolator = reader.ReadBytes(24);
                var fov = reader.ReadUInt32();
                var orthographic = reader.ReadByte();
                var keyframe = new CameraKeyframe
                {
                    Fov = fov,
                    FocalLength = focalLength,
                    Orthographic = orthographic != 0,
                    Position = position,
                    Rotation = rotation,
                    Interpolation = interpolator
                };
                keyframes[nFrame] = keyframe;
            }
            var frameList = keyframes.Select(entry => entry).ToList().OrderBy(kv => kv.Key).ToList();
            ret.KeyFrames = frameList;
            return ret;
        }

        private VmdBone ReadVmdBone(BinaryReader reader)
        {
            return new VmdBone
            {
                BoneName = MmdReaderUtil.ReadStringFixedLength(reader, 15, Tools.JapaneseEncoding),
                NFrame = reader.ReadInt32(),
                Translation = MmdReaderUtil.ReadVector3(reader),
                Rotation = MmdReaderUtil.ReadQuaternion(reader),
                XInterpolator = reader.ReadBytes(16),
                YInterpolator = reader.ReadBytes(16),
                ZInterpolator = reader.ReadBytes(16),
                RInterpolator = reader.ReadBytes(16)
            };
        }

        private VmdMorph ReadVmdMorph(BinaryReader reader)
        {
            return new VmdMorph
            {
                MorphName = MmdReaderUtil.ReadStringFixedLength(reader, 15, Tools.JapaneseEncoding),
                NFrame = reader.ReadInt32(),
                Weight = reader.ReadSingle()
            };
        }

        private class VmdBone
        {
            public string BoneName { get; set; } //15
            public int NFrame { get; set; }
            public Vector3 Translation { get; set; }
            public Quaternion Rotation { get; set; }
            public byte[] XInterpolator { get; set; } //16
            public byte[] YInterpolator { get; set; } //16
            public byte[] ZInterpolator { get; set; } //16
            public byte[] RInterpolator { get; set; } //16
        }

        private class VmdMorph
        {
            public string MorphName { get; set; } //15
            public int NFrame { get; set; }
            public float Weight { get; set; }
        }

        private class TempMmdMotion
        {
            public string Name { get; set; }

            public int Length { get; private set; }

            public Dictionary<string, Dictionary<int, BoneKeyframe>> BoneMotions { get; set; }
            public Dictionary<string, Dictionary<int, MorphKeyframe>> MorphMotions { get; set; }

            public TempMmdMotion()
            {
                BoneMotions = new Dictionary<string, Dictionary<int, BoneKeyframe>>();
                MorphMotions = new Dictionary<string, Dictionary<int, MorphKeyframe>>();
            }

            public BoneKeyframe GetOrCreateBoneKeyFrame(string boneName, int frame)
            {
                Dictionary<int, BoneKeyframe> framesForBone;
                if (frame > Length)
                {
                    Length = frame;
                }
                if (!BoneMotions.TryGetValue(boneName, out framesForBone))
                {
                    framesForBone = new Dictionary<int, BoneKeyframe>();
                    BoneMotions.Add(boneName, framesForBone);
                }
                BoneKeyframe boneKeyframe;
                if (!framesForBone.TryGetValue(frame, out boneKeyframe))
                {
                    boneKeyframe = new BoneKeyframe();
                    framesForBone.Add(frame, boneKeyframe);
                }
                return boneKeyframe;
            }

            public MorphKeyframe GetOrCreateMorphKeyFrame(string boneName, int frame)
            {
                Dictionary<int, MorphKeyframe> framesForBone;
                if (frame > Length)
                {
                    Length = frame;
                }
                if (!MorphMotions.TryGetValue(boneName, out framesForBone))
                {
                    framesForBone = new Dictionary<int, MorphKeyframe>();
                    MorphMotions.Add(boneName, framesForBone);
                }
                MorphKeyframe morphKeyframe;
                if (!framesForBone.TryGetValue(frame, out morphKeyframe))
                {
                    morphKeyframe = new MorphKeyframe();
                    framesForBone.Add(frame, morphKeyframe);
                }
                return morphKeyframe;
            }

            public MmdMotion BuildMmdMotion()
            {
                var ret = new MmdMotion();
                ret.BoneMotions = new Dictionary<string, List<KeyValuePair<int, BoneKeyframe>>>();
                foreach (var entry in BoneMotions)
                {
                    var value = entry.Value.ToList();
                    value = value.OrderBy(kv => kv.Key).ToList();
                    ret.BoneMotions.Add(entry.Key, value);
                }
                ret.MorphMotions = new Dictionary<string, List<KeyValuePair<int, MorphKeyframe>>>();
                foreach (var entry in MorphMotions)
                {
                    var value = entry.Value.ToList();
                    value = value.OrderBy(kv => kv.Key).ToList();
                    ret.MorphMotions.Add(entry.Key, value);
                }
                ret.Length = Length;
                ret.Name = Name;
                return ret;
            }
        }
    }
}