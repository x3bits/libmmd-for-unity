using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using LibMMD.Model;
using LibMMD.Motion;
using LibMMD.Pyhsics;
using LibMMD.Util;

namespace LibMMD.Unity3D.BonePose
{
    public class BonePosePreCalculator
    {
        private class BonePoseFrame
        {
            public double TimePos { get; private set; }
            public BonePoseImage[] PoseImages { get; private set; }

            public BonePoseFrame(double timePos, BonePoseImage[] poseImages)
            {
                TimePos = timePos;
                PoseImages = poseImages;
            }
        }

        private readonly BonePoseCalculatorWorker _worker;
        private readonly Poser _poser;
        private readonly BulletPyhsicsReactor _physicsReactor;
        private readonly MotionPlayer _motionPlayer;
        private readonly float _stepLength;
        private volatile float _timePos;
        private volatile float _timePosOut;
        private volatile float _takeTimePos;
        private readonly object _takePosLock = new object();
        private volatile SynchronizedQueue<BonePoseFrame> _bonePoseImagesStore;
        private volatile bool _stopped;
        private readonly bool _autoStepLength;
        private readonly int _frameCacheSize;
        private volatile float _lastMissedPos = -1.0f;
        private readonly bool _poseMode;

        public BonePosePreCalculator(BonePoseCalculatorWorker worker, Poser poser, BulletPyhsicsReactor physicsReactor, MotionPlayer motionPlayer, float stepLength, float startTimePos, int frameCacheSize, bool autoStepLength)
        {
            _poseMode = false;
            _poser = poser;
            _physicsReactor = physicsReactor;
            _motionPlayer = motionPlayer;
            _stepLength = stepLength;
            _bonePoseImagesStore = new SynchronizedQueue<BonePoseFrame>();
            _timePos = startTimePos;
            _autoStepLength = autoStepLength;
            _frameCacheSize = frameCacheSize;
            _motionPlayer.SeekTime(startTimePos);
            _poser.PrePhysicsPosing();
            _physicsReactor.Reset();
            _poser.PostPhysicsPosing();
            var image = GetBonePoseImage(_poser);
            _bonePoseImagesStore.Enqueue(new BonePoseFrame(startTimePos, image));
            _worker = worker;
        }

        public BonePosePreCalculator(BonePoseCalculatorWorker worker, MmdPose pose, Poser poser, BulletPyhsicsReactor physicsReactor, float stepLength,float startTimePos, int frameCacheSize, bool autoStepLength)
        {
            _poseMode = true;
            _poser = poser;
            _physicsReactor = physicsReactor;
            _stepLength = stepLength;
            _bonePoseImagesStore = new SynchronizedQueue<BonePoseFrame>();
            _timePos = startTimePos;
            _autoStepLength = autoStepLength;
            _frameCacheSize = frameCacheSize;
            poser.ResetPosing();
            SetPoseToPoser(pose, _poser);
            _poser.PrePhysicsPosing();
            _physicsReactor.Reset();
            _poser.PostPhysicsPosing();
            var image = GetBonePoseImage(_poser);
            _bonePoseImagesStore.Enqueue(new BonePoseFrame(startTimePos, image));
            _worker = worker;
        }

        private void SetPoseToPoser(MmdPose pose, Poser poser)
        {
            var nameToIndex = BuildBoneNameToIndexDictionary(poser.Model);
            foreach (var entry in pose.BonePoses)
            {
                var name = entry.Key;
                var bonePose = entry.Value;
                int index;
                if (!nameToIndex.TryGetValue(name, out index))
                {
                    continue;
                }
                poser.SetBonePose(index, bonePose);
            }
        }

        private Dictionary<string, int> BuildBoneNameToIndexDictionary(MmdModel model)
        {
            var bones = model.Bones;
            if (bones == null || bones.Length == 0)
            {
                return new Dictionary<string, int>();
            }
            var ret = new Dictionary<string, int>();
            for (var i = 0; i < bones.Length; ++i)
            {
                ret.Add(bones[i].Name, i);
            }
            return ret;
        }

        public double StepLength
        {
            get { return _stepLength; }
        }

        public void Start()
        {
            lock (this)
            {
                _worker.Start(this);
            }
        }

        public void Stop()
        {
            lock (this)
            {
                _stopped = true;
                _worker.Stop(this);
            }
        }

        /*
        取出时间timePose上的骨骼姿态。如果还没有计算出来，notCaculatedYet为true并返回队列里的最后一帧，队列为空则返回null
        */
        public BonePoseImage[] Take(double timePos, out bool notCaculatedYet, out double returnPos)
        {
            notCaculatedYet = false;
            lock (_takePosLock)
            {
                if (timePos < _takeTimePos)
                {
                    returnPos = -1.0;
                    return null;
                }
                BonePoseFrame lastTookBonePoseImage = null;
                while (true)
                {
                    var bonePoseImage = _bonePoseImagesStore.Take();
                    _worker.NotifyTake(this);
                    if (bonePoseImage == null)
                    {
                        notCaculatedYet = true;
                        _lastMissedPos = (float) timePos;
                        returnPos = _takeTimePos;
                        return lastTookBonePoseImage == null ? null : lastTookBonePoseImage.PoseImages;
                    }
                    lastTookBonePoseImage = bonePoseImage;
                    _takeTimePos = (float) bonePoseImage.TimePos;
                    if (_takeTimePos + _stepLength / 2 >=  timePos)
                    {
                        returnPos = _takeTimePos;
                        return bonePoseImage.PoseImages;
                    }
                }
            }
        }

        private void Run()
        {
            //var lastTickcount = Environment.TickCount;
            while (!_stopped)
            {
                //var tickCount = Environment.TickCount;
                //Debug.LogFormat("{0} ms since last step", tickCount - lastTickcount);
                Step();
                //Debug.LogFormat("{0} ms for calculation", Environment.TickCount - tickCount);
                //lastTickcount = tickCount;
            }
        }

        public bool Step()
        {
            if (_bonePoseImagesStore.Count() >= _frameCacheSize)
            {
                return false;
            }
            var actualStepLength = _stepLength;
            _timePos += _stepLength;
            if (_autoStepLength && _timePos < _lastMissedPos)
            {
                //Debug.LogWarningFormat("auto step length triggered, _timePos={0}, _lastMissedPos={1}", _timePos, _lastMissedPos);
                actualStepLength = _lastMissedPos - _timePos + _stepLength;
                _timePos = _lastMissedPos;
            }
            if (!_poseMode)
            {
                _motionPlayer.SeekTime(_timePos);
            }
            _poser.PrePhysicsPosing(false);
            //var tickCount = Environment.TickCount;
            _physicsReactor.React(actualStepLength, 2, actualStepLength);
            //Debug.LogFormat("{0} ms for physics", Environment.TickCount - tickCount);
            _poser.PostPhysicsPosing();
            var image = GetBonePoseImage(_poser);
            _bonePoseImagesStore.Enqueue(new BonePoseFrame(_timePos, image));
            //Debug.LogFormat("{0} frames in pose cache", cachedCount);
            return true;
        }
        
        public static BonePoseImage[] GetBonePoseImage(Poser poser)
        {
            var boneCount = poser.BoneImages.Length; 
            var ret  = new BonePoseImage[boneCount];
            var model = poser.Model;
            var poserBoneImages = poser.BoneImages;
            for (var i = 0; i < boneCount; ++i)
            {
                var poserBoneImage = poserBoneImages[i];
                var position = poserBoneImage.SkinningMatrix.MultiplyPoint3x4(model.Bones[i].Position);
                var rotation = poserBoneImage.SkinningMatrix.ExtractRotation();
                ret[i] = new BonePoseImage
                {
                    Position = position,
                    Rotation = rotation
                };
            }
            return ret;
        }
        
    }
}