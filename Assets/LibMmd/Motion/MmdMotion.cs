using System.Collections.Generic;
using UnityEngine;

namespace LibMMD.Motion
{
    public class MmdMotion
    {
        public string Name { get; set; }

        public int Length { get; set; }

        public Dictionary<string, List<KeyValuePair<int, BoneKeyframe>>> BoneMotions { get; set; }
        public Dictionary<string, List<KeyValuePair<int, MorphKeyframe>>> MorphMotions { get; set; }

        public MmdMotion()
        {
            BoneMotions = new Dictionary<string, List<KeyValuePair<int, BoneKeyframe>>>();
            MorphMotions = new Dictionary<string, List<KeyValuePair<int, MorphKeyframe>>>();
        }

        public BonePose GetBonePose(string boneName, int frame)
        {
            List<KeyValuePair<int, BoneKeyframe>> keyFrames;
            BoneMotions.TryGetValue(boneName, out keyFrames);
            if (keyFrames == null || keyFrames.Count == 0)
            {
                return new BonePose
                {
                    Translation = Vector3.zero,
                    Rotation = Quaternion.identity
                };
            }

            if (keyFrames[0].Key >= frame)
            {
                var key = keyFrames[0].Value;
                return new BonePose
                {
                    Translation = key.Translation,
                    Rotation = key.Rotation
                };
            }

            if (keyFrames[keyFrames.Count - 1].Key <= frame)
            {
                var key = keyFrames[keyFrames.Count - 1].Value;
                return new BonePose
                {
                    Translation = key.Translation,
                    Rotation = key.Rotation
                };
            }

            var toSearch = new KeyValuePair<int, BoneKeyframe>(frame, null);
            var rightBoundIndex = keyFrames.BinarySearch(toSearch, BoneKeyframeSearchComparator.Instance);
            if (rightBoundIndex < 0)
            {
                rightBoundIndex = ~rightBoundIndex;
            }
            int leftBoundIndex;
            if (rightBoundIndex == 0)
            {
                leftBoundIndex = 0;
            } else if (rightBoundIndex >= keyFrames.Count)
            {
                rightBoundIndex = leftBoundIndex = keyFrames.Count - 1;
            }
            else
            {
                leftBoundIndex = rightBoundIndex - 1;
            }
            var rightBound = keyFrames[rightBoundIndex];
            var rightFrame = rightBound.Key;
            var rightKey = rightBound.Value;
            var leftBound = keyFrames[leftBoundIndex];
            var leftFrame = leftBound.Key;
            var leftKey = leftBound.Value;
            if (leftFrame == rightFrame)
            {
                return new BonePose
                {
                    Translation = leftKey.Translation,
                    Rotation = leftKey.Rotation
                };
            }
            var baryPos = (frame - leftFrame) / (float) (rightFrame - leftFrame);
            var translation = new Vector3();
            var lambda = leftKey.XInterpolator.Calculate(baryPos);
            translation.x = leftKey.Translation.x * (1 - lambda) + rightKey.Translation.x * lambda;
            lambda = leftKey.YInterpolator.Calculate(baryPos);
            translation.y = leftKey.Translation.y * (1 - lambda) + rightKey.Translation.y * lambda;
            lambda = leftKey.ZInterpolator.Calculate(baryPos);
            translation.z = leftKey.Translation.z * (1 - lambda) + rightKey.Translation.z * lambda;
            lambda = leftKey.RInterpolator.Calculate(baryPos);
            var rotation = Quaternion.Lerp(leftKey.Rotation, rightKey.Rotation, lambda);
            return new BonePose
            {
                Translation = translation,
                Rotation = rotation
            };
        }

        public BonePose GetBonePose(string boneName, double time)
        {
            List<KeyValuePair<int, BoneKeyframe>> keyFrames;
            BoneMotions.TryGetValue(boneName, out keyFrames);
            if (keyFrames == null || keyFrames.Count == 0)
            {
                return new BonePose
                {
                    Translation = Vector3.zero,
                    Rotation = Quaternion.identity
                };
            }

            var dFrame = time * 30.0;

            if (keyFrames[0].Key >= dFrame)
            {
                var key = keyFrames[0].Value;
                return new BonePose
                {
                    Translation = key.Translation,
                    Rotation = key.Rotation
                };
            }

            if (keyFrames[keyFrames.Count - 1].Key <= dFrame)
            {
                var key = keyFrames[keyFrames.Count - 1].Value;
                return new BonePose
                {
                    Translation = key.Translation,
                    Rotation = key.Rotation
                };
            }

            var toSearch = new KeyValuePair<int, BoneKeyframe>((int) dFrame, null);
            var rightBoundIndex = keyFrames.BinarySearch(toSearch, BoneKeyframeSearchComparator.Instance);
            if (rightBoundIndex < 0)
            {
                rightBoundIndex = ~rightBoundIndex;
            }
            int leftBoundIndex;
            if (rightBoundIndex == 0)
            {
                leftBoundIndex = 0;
            } else if (rightBoundIndex >= keyFrames.Count)
            {
                rightBoundIndex = leftBoundIndex = keyFrames.Count - 1;
            }
            else
            {
                leftBoundIndex = rightBoundIndex - 1;
            }
            var rightBound = keyFrames[rightBoundIndex];
            var rightFrame = rightBound.Key;
            var rightKey = rightBound.Value;
            var leftBound = keyFrames[leftBoundIndex];
            var leftFrame = leftBound.Key;
            var leftKey = leftBound.Value;
            if (leftFrame == rightFrame)
            {
                return new BonePose
                {
                    Translation = leftKey.Translation,
                    Rotation = leftKey.Rotation
                };
            }
            var baryPos = (float) (dFrame - leftFrame) / (rightFrame - leftFrame);
            var translation = new Vector3();
            var lambda = leftKey.XInterpolator.Calculate(baryPos);
            translation.x = leftKey.Translation.x * (1 - lambda) + rightKey.Translation.x * lambda;
            lambda = leftKey.YInterpolator.Calculate(baryPos);
            translation.y = leftKey.Translation.y * (1 - lambda) + rightKey.Translation.y * lambda;
            lambda = leftKey.ZInterpolator.Calculate(baryPos);
            translation.z = leftKey.Translation.z * (1 - lambda) + rightKey.Translation.z * lambda;
            lambda = leftKey.RInterpolator.Calculate(baryPos);
            var rotation = Quaternion.Lerp(leftKey.Rotation, rightKey.Rotation, lambda);
            return new BonePose
            {
                Translation = translation,
                Rotation = rotation
            };
        }

        public MorphPose GetMorphPose(string morphName, int frame)
        {
            List<KeyValuePair<int, MorphKeyframe>> keyFrames;
            MorphMotions.TryGetValue(morphName, out keyFrames);
            if (keyFrames == null || keyFrames.Count == 0)
            {
                return new MorphPose(0.0f);
            }
            if (keyFrames[0].Key >= frame)
            {
                var key = keyFrames[0].Value;
                return new MorphPose(key.Weight);
            }

            if (keyFrames[keyFrames.Count - 1].Key <= frame)
            {
                var key = keyFrames[keyFrames.Count - 1].Value;
                return new MorphPose(key.Weight);
            }

            var toSearch = new KeyValuePair<int, MorphKeyframe>(frame, null);
            var rightBoundIndex = keyFrames.BinarySearch(toSearch, MorphKeyframeSearchComparator.Instance);
            if (rightBoundIndex < 0)
            {
                rightBoundIndex = ~rightBoundIndex;
            }
            int leftBoundIndex;
            if (rightBoundIndex == 0)
            {
                leftBoundIndex = 0;
            } else if (rightBoundIndex >= keyFrames.Count)
            {
                rightBoundIndex = leftBoundIndex = keyFrames.Count - 1;
            }
            else
            {
                leftBoundIndex = rightBoundIndex - 1;
            }
            var rightBound = keyFrames[rightBoundIndex];
            var rightFrame = rightBound.Key;
            var rightKey = rightBound.Value;
            var leftBound = keyFrames[leftBoundIndex];
            var leftFrame = leftBound.Key;
            var leftKey = leftBound.Value;
            if (leftFrame == rightFrame)
            {
                return new MorphPose(leftKey.Weight);
            }
            var baryPos = (frame - leftFrame) / (float) (rightFrame - leftFrame);
            var lWeight = leftKey.Weight;
            var rWeight = rightKey.Weight;
            var lambda = leftKey.WInterpolator.Calculate(baryPos);

            return new MorphPose(lWeight * (1 - lambda) + rWeight * lambda);
        }

        public MorphPose GetMorphPose(string morphName, double time)
        {
            List<KeyValuePair<int, MorphKeyframe>> keyFrames;
            MorphMotions.TryGetValue(morphName, out keyFrames);
            if (keyFrames == null || keyFrames.Count == 0)
            {
                return new MorphPose(0.0f);
            }

            var iFrame = time * 30.0;

            if (keyFrames[0].Key >= iFrame)
            {
                var key = keyFrames[0].Value;
                return new MorphPose(key.Weight);
            }

            if (keyFrames[keyFrames.Count - 1].Key <= iFrame)
            {
                var key = keyFrames[keyFrames.Count - 1].Value;
                return new MorphPose(key.Weight);
            }

            var toSearch = new KeyValuePair<int, MorphKeyframe>((int)iFrame, null);
            var rightBoundIndex = keyFrames.BinarySearch(toSearch, MorphKeyframeSearchComparator.Instance);
            if (rightBoundIndex < 0)
            {
                rightBoundIndex = ~rightBoundIndex;
            }
            int leftBoundIndex;
            if (rightBoundIndex == 0)
            {
                leftBoundIndex = 0;
            } else if (rightBoundIndex >= keyFrames.Count)
            {
                rightBoundIndex = leftBoundIndex = keyFrames.Count - 1;
            }
            else
            {
                leftBoundIndex = rightBoundIndex - 1;
            }
            var rightBound = keyFrames[rightBoundIndex];
            var rightFrame = rightBound.Key;
            var rightKey = rightBound.Value;
            var leftBound = keyFrames[leftBoundIndex];
            var leftFrame = leftBound.Key;
            var leftKey = leftBound.Value;
            if (leftFrame == rightFrame)
            {
                return new MorphPose(leftKey.Weight);
            }
            var baryPos = (float) (iFrame - leftFrame) / (rightFrame - leftFrame);
            var lWeight = leftKey.Weight;
            var rWeight = rightKey.Weight;
            var lambda = leftKey.WInterpolator.Calculate(baryPos);

            return new MorphPose(lWeight * (1 - lambda) + rWeight * lambda);
        }

        public bool IsBoneRegistered(string boneName)
        {
            return BoneMotions.ContainsKey(boneName);
        }

        public bool IsMorphRegistered(string morphName)
        {
            return MorphMotions.ContainsKey(morphName);
        }

        private class BoneKeyframeSearchComparator : IComparer<KeyValuePair<int, BoneKeyframe>>
        {
            public static readonly BoneKeyframeSearchComparator Instance = new BoneKeyframeSearchComparator();

            private BoneKeyframeSearchComparator()
            {
            }

            public int Compare(KeyValuePair<int, BoneKeyframe> x, KeyValuePair<int, BoneKeyframe> y)
            {
                return x.Key.CompareTo(y.Key);
            }
        }

        private class MorphKeyframeSearchComparator : IComparer<KeyValuePair<int, MorphKeyframe>>
        {
            public static readonly MorphKeyframeSearchComparator Instance = new MorphKeyframeSearchComparator();

            private MorphKeyframeSearchComparator()
            {
            }

            public int Compare(KeyValuePair<int, MorphKeyframe> x, KeyValuePair<int, MorphKeyframe> y)
            {
                return x.Key.CompareTo(y.Key);
            }
        }
    }
}