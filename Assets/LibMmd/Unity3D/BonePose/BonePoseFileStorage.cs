using System;
using System.IO;
using System.Linq;
using System.Text;
using LibMMD.Model;
using LibMMD.Util;
using UnityEngine;

namespace LibMMD.Unity3D.BonePose
{
    public class BonePoseFileStorage
    {
        private const int BufferSize = 4096;
        private const int BonePoseByteCount = 4 * 6;
        
        private FileStream _fileStream;
        private BufferedStream _bufferedStream;
        private BinaryReader _reader;
        private Meta _meta;
        private long _dataStartPosition;
        private int _readFramePos;
        private BonePoseImage[] _lastFrame;//存下最后一帧，防止外层到达最后一帧后导致反复Seek

        public BonePoseFileStorage(MmdModel model, string path)
        {
            Load(model, path);
        }

        private void Load(MmdModel model, string path)
        {
            Release();
            try
            {
                _fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
                _bufferedStream = new BufferedStream(_fileStream, BufferSize);
                _reader = new BinaryReader(_bufferedStream);
            }
            catch (Exception e)
            {
                Release();
                throw e;
            }
            _meta = ReadMetaAndCheck(model);
            _dataStartPosition = _bufferedStream.Position;
            _readFramePos = 0;
        }

        public BonePoseImage[] GetBonePose(double time)
        {
            var framePos = (int) Math.Round(time / _meta.StepLength);
            var lastFrame = false;
            if (framePos >= _meta.FrameCount)
            {
                framePos = _meta.FrameCount - 1;
                if (_lastFrame != null)
                {
                    return _lastFrame;
                }
                lastFrame = true;
            }
            if (framePos < _readFramePos ||
                (framePos - _readFramePos) * _meta.BoneCount * BonePoseByteCount > BufferSize)
            {
                SeekToFrame(framePos);
            }
            BonePoseImage[] frame = null;
            while (_readFramePos <= framePos)
            {
                frame = ReadFrame();
                _readFramePos++;
            }
            if (lastFrame)
            {
                _lastFrame = frame;
            }
            return frame;
        }

        private void SeekToFrame(int framePos)
        {
            Debug.LogFormat("BonePoseFileStorage Seek {0}", framePos);
            _bufferedStream.Seek(_dataStartPosition + _meta.BoneCount * BonePoseByteCount * framePos, SeekOrigin.Begin);
            _readFramePos = framePos;
        }

        private Meta ReadMetaAndCheck(MmdModel model)
        {
            var magic = MmdReaderUtil.ReadStringFixedLength(_reader, 4, Encoding.UTF8);
            if (!"VBP ".Equals(magic))
            {
                throw new BonePoseFileFormatException("error magic " + magic);
            }
            var mainVersion = _reader.ReadInt16();
            var subVersion = _reader.ReadInt16();
            if (mainVersion != 1 || subVersion != 0)
            {
                throw new BonePoseFileFormatException("not supported version: " + mainVersion + "." + subVersion);
            }
            var ret = new Meta
            {
                BoneCount = _reader.ReadInt32(),
                FrameCount = _reader.ReadInt32(),
                StepLength = _reader.ReadSingle()
            };
            var exceptedModelHash = _reader.ReadBytes(16);
            var modelHash = BonePoseFileGenerator.CalculateModelHash(model);
            if (!exceptedModelHash.SequenceEqual(modelHash))
            {
                throw new BonePoseNotSuitableException("model hash not equals the value in bone pose file");
            }
            return ret;
        }

        private BonePoseImage[] ReadFrame()
        {
            var boneCount = _meta.BoneCount;
            var ret = new BonePoseImage[boneCount];
            for (var i = 0; i < boneCount; i++)
            {
                var position = MmdReaderUtil.ReadVector3(_reader);
                var rotationEular = MmdReaderUtil.ReadVector3(_reader);
                ret[i] = new BonePoseImage
                {
                    Position = position,
                    Rotation = Quaternion.Euler(rotationEular)
                };
            }
            return ret;
        }

        public void Release()
        {
            _meta = null;
            Utils.DisposeIgnoreException(_reader);
            Utils.DisposeIgnoreException(_bufferedStream);
            Utils.DisposeIgnoreException(_fileStream);
        }

        private class Meta
        {
            public int BoneCount { get; set; }
            public int FrameCount { get; set; }
            public float StepLength { get; set; }
        }
        
    }
}