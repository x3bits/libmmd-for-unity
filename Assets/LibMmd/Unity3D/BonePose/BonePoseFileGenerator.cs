using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using LibMMD.Model;
using LibMMD.Motion;
using LibMMD.Pyhsics;
using UnityEngine;

namespace LibMMD.Unity3D.BonePose
{
    /*
    骨骼姿势文件格式
	4字节 magic 固定为 VBP空格
	2字节 short 主版本号
	2字节 short 次版本号
	4字节 int 骨骼数量boneCount
	4字节 int 帧数frameCount
	4字节 float 每帧时间长度
	16字节 bytes 模型Hash值
	frameCount个帧
	帧格式：
		boneCount个骨骼位姿，格式
		4字节 float pos x
		4字节 float pos y
		4字节 float pos z
		4字节 float enlar angal x
		4字节 float enlar angal y
		4字节 float enlar angal z
    */

    public class BonePoseFileGenerator
    {
        private static readonly Encoding CharEncoding = Encoding.UTF8;
        private const float DefaultStepLength = 1.0f / 60.0f;

        public enum GenerateStatus
        {
            Ready,
            Preparing,
            CalculatingFrames,
            Finished,
            Failed,
            Canceled
        }

        private volatile GenerateStatus _status = GenerateStatus.Ready;
        private volatile int _totalFrames;
        private volatile int _calculatedFrames;
        private volatile bool _canceled = false;

        public GenerateStatus Status
        {
            get { return _status; }
        }

        public int TotalFrames
        {
            get { return _totalFrames; }
        }

        public int CalculatedFrames
        {
            get { return _calculatedFrames; }
        }

        public void Cancel()
        {
            _canceled = true;
        }


        public static void Generate(MmdModel model, MmdMotion motion, string savePath,
            float frameStepLength = DefaultStepLength, float timeAfterMotionFinish = 0.0f,
            float physicsStepLength = DefaultStepLength)
        {
            new BonePoseFileGenerator().DoGenerate(model, motion, savePath, frameStepLength, timeAfterMotionFinish,
                physicsStepLength);
        }

        public static BonePoseFileGenerator GenerateAsync(MmdModel model, MmdMotion motion, string savePath,
            float frameStepLength = DefaultStepLength, float timeAfterMotionFinish = 0.0f,
            float physicsStepLength = DefaultStepLength)
        {
            var ret = new BonePoseFileGenerator();
            new Thread(() =>
            {
                ret.DoGenerate(model, motion, savePath, frameStepLength, timeAfterMotionFinish, physicsStepLength);
            }).Start();
            return ret;
        }

        private BonePoseFileGenerator()
        {
        }

        private void DoGenerate(MmdModel model, MmdMotion motion, string savePath, float frameStepLength,
            float timeAfterMotionFinish, float physicsStepLength)
        {
            try
            {
                _status = GenerateStatus.Preparing;
                if (physicsStepLength > frameStepLength)
                {
                    physicsStepLength = frameStepLength;
                }
                var poser = new Poser(model);
                var motionPlayer = new MotionPlayer(motion, poser);
                var physicsReactor = new BulletPyhsicsReactor();

                var totalTimeLength = motion.Length / 30.0 + timeAfterMotionFinish;
                var totalStepCount = (int) (totalTimeLength / frameStepLength) + 1;
                var playPos = 0.0;
                var maxSubSteps = (int) (frameStepLength / physicsStepLength) + 1;

                using (var fileStream = new FileStream(savePath, FileMode.Create))
                {
                    using (var bufferedStream = new BufferedStream(fileStream))
                    {
                        using (var binaryWriter = new BinaryWriter(bufferedStream))
                        {
                            WriteHeader(binaryWriter, model, totalStepCount + 1, frameStepLength);
                            _status = GenerateStatus.CalculatingFrames;
                            _totalFrames = totalStepCount + 1;
                            _calculatedFrames = 0;
                            physicsReactor.AddPoser(poser);
                            motionPlayer.SeekFrame(0);
                            poser.PrePhysicsPosing();
                            physicsReactor.Reset();
                            poser.PostPhysicsPosing();
                            WritePose(binaryWriter, poser);
                            _calculatedFrames = 1;
                            for (var i = 0; i < totalStepCount; ++i)
                            {
                                playPos += frameStepLength;
                                motionPlayer.SeekTime(playPos);
                                poser.PrePhysicsPosing();
                                physicsReactor.React(frameStepLength, maxSubSteps, physicsStepLength);
                                poser.PostPhysicsPosing();
                                WritePose(binaryWriter, poser);
                                _calculatedFrames = i + 2;
                                if (_canceled)
                                {
                                    _status = GenerateStatus.Canceled;
                                    return;
                                }
                            }
                        }
                    }
                }
                _status = GenerateStatus.Finished;
            }
            catch (Exception e)
            {
                _status = GenerateStatus.Failed;
                Debug.LogException(e);
            }
            finally
            {
                if (_status != GenerateStatus.Finished)
                {
                    try
                    {
                        File.Delete(savePath);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
        }

        private static void WritePose(BinaryWriter writer, Poser poser)
        {
            var bonePoseImages = BonePosePreCalculator.GetBonePoseImage(poser);
            foreach (var bonePose in bonePoseImages)
            {
                WriteVector3(writer, bonePose.Position);
                WriteVector3(writer, bonePose.Rotation.eulerAngles);
            }
        }

        private static void WriteVector3(BinaryWriter writer, Vector3 v)
        {
            writer.Write(v.x);
            writer.Write(v.y);
            writer.Write(v.z);
        }

        public static byte[] CalculateModelHash(MmdModel model)
        {
            var byteList = new List<byte>();
            byteList.AddRange(CharEncoding.GetBytes(model.Name));
            byteList.Add(0);
            byteList.AddRange(GetArrayLengthBytes(model.Vertices));
            byteList.Add(0);
            byteList.AddRange(GetArrayLengthBytes(model.TriangleIndexes));
            byteList.Add(0);
            byteList.AddRange(GetArrayLengthBytes(model.Parts));
            byteList.Add(0);
            byteList.AddRange(GetArrayLengthBytes(model.Morphs));
            byteList.Add(0);
            byteList.AddRange(GetArrayLengthBytes(model.Rigidbodies));
            byteList.Add(0);
            byteList.AddRange(GetArrayLengthBytes(model.Constraints));
            byteList.Add(0);
            foreach (var bone in model.Bones)
            {
                byteList.AddRange(CharEncoding.GetBytes(bone.Name));
                byteList.Add(0);
            }
            var bytes = byteList.ToArray();
            var md5 = MD5.Create();
            return md5.ComputeHash(bytes);
        }


        private static void WriteHeader(BinaryWriter binaryWriter, MmdModel model, int totalFrameCount,
            float frameStepLength)
        {
            binaryWriter.Write(CharEncoding.GetBytes("VBP "));
            binaryWriter.Write((short) 1);
            binaryWriter.Write((short) 0);
            binaryWriter.Write(model.Bones.Length);
            binaryWriter.Write(totalFrameCount);
            binaryWriter.Write(frameStepLength);
            binaryWriter.Write(CalculateModelHash(model));
        }

        private static byte[] GetArrayLengthBytes<T>(T[] array)
        {
            var length = array == null ? 0 : array.Length;
            return CharEncoding.GetBytes(length.ToString());
        }
    }
}