using System;
using System.Collections.Generic;
using LibMMD.Model;
using LibMMD.Util;
using UnityEngine;

namespace LibMMD.Motion
{
    public class MotionPlayer
    {
        public MotionPlayer(MmdMotion motion, Poser poser)
        {
            _motion = motion;
            _poser = poser;
            var model = poser.Model;
            for (var i = 0; i < model.Bones.Length; ++i)
            {
                var name = model.Bones[i].Name;
                if (motion.IsBoneRegistered(name))
                {
                    _boneMap.Add(new KeyValuePair<string, int>(name, i));
                }
            }

            for (var i = 0; i < model.Morphs.Length; ++i)
            {
                var name = model.Morphs[i].Name;
                if (motion.IsMorphRegistered(name))
                {
                    _morphMap.Add(new KeyValuePair<string, int>(name, i));
                }
            }
        }

        public void SeekFrame(int frame)
        {
            foreach (var entry in _morphMap)
            {
                _poser.SetMorphPose(entry.Value, _motion.GetMorphPose(entry.Key, frame));
            }
            foreach (var entry in _boneMap)
            {
                _poser.SetBonePose(entry.Value, _motion.GetBonePose(entry.Key, frame));
            }
        }
        
        public void SeekTime(double time)
        {
            foreach (var entry in _morphMap)
            {
                _poser.SetMorphPose(entry.Value, _motion.GetMorphPose(entry.Key, time));
            }
            foreach (var entry in _boneMap)
            {
                _poser.SetBonePose(entry.Value, _motion.GetBonePose(entry.Key, time));
            }
        }

        public void CalculateMorphVertexOffset(MmdModel model, double time, Vector3[] output)
        {
            if (output.Length != model.Vertices.Length)
            {
                throw new ArgumentException("model vertex count not equals to output array length");
            }
            Array.Clear(output, 0, output.Length);
            foreach (var entry in _morphMap)
            {
                var morphPose = _motion.GetMorphPose(entry.Key, time);
                UpdateVertexOffsetByMorph(model, entry.Value, morphPose.Weight, output);
            }
        } 
        
        private static void UpdateVertexOffsetByMorph(MmdModel model, int index, float rate, Vector3[] output)
        {
            if (rate < Tools.MmdMathConstEps)
            {
                return;
            }
            var morph = model.Morphs[index];
            switch (morph.Type)
            {
                case Morph.MorphType.MorphTypeGroup:
                    foreach (var morphData in morph.MorphDatas)
                    {
                        var data = (Morph.GroupMorph) morphData;
                        UpdateVertexOffsetByMorph(model, data.MorphIndex, data.MorphRate * rate, output);
                    }
                    break;
                case Morph.MorphType.MorphTypeVertex:
                    foreach (var morphData in morph.MorphDatas)
                    {
                        var data = (Morph.VertexMorph) morphData;
                        output[data.VertexIndex] = output[data.VertexIndex] + data.Offset * rate;
                    }
                    break;                  
            }
        }
        
        public double GetMotionTimeLength()
        {
            return _motion.Length * 30.0;
        }

        public int GetMotionFrameLength()
        {
            return _motion.Length;
        }

        private readonly List<KeyValuePair<string, int>> _boneMap = new List<KeyValuePair<string, int>>();
        private readonly List<KeyValuePair<string, int>> _morphMap = new List<KeyValuePair<string, int>>();

        private readonly MmdMotion _motion;
        private readonly Poser _poser;
    }
}