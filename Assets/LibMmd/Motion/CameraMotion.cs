using System.Collections.Generic;
using UnityEngine;

namespace LibMMD.Motion
{
    public class CameraMotion
    {
        public List<KeyValuePair<int, CameraKeyframe>> KeyFrames { get; set; }

        public CameraPose GetCameraPose(int frame)
        {
            if (KeyFrames[0].Key >= frame)
            {
                var value = KeyFrames[0].Value;
                return CameraKeyFrameToCameraPose(value);
            }

            if (KeyFrames[KeyFrames.Count - 1].Key <= frame)
            {
                var value = KeyFrames[KeyFrames.Count - 1].Value;
                return CameraKeyFrameToCameraPose(value);
            }

            var toSearch = new KeyValuePair<int, CameraKeyframe>(frame, null);

            var rightBoundIndex = KeyFrames.BinarySearch(toSearch, CameraKeyframeSearchComparator.Instance);

            if (rightBoundIndex < 0)
            {
                rightBoundIndex = ~rightBoundIndex;
            }
            int leftBoundIndex;
            if (rightBoundIndex == 0)
            {
                leftBoundIndex = 0;
            }
            else if (rightBoundIndex >= KeyFrames.Count)
            {
                rightBoundIndex = leftBoundIndex = KeyFrames.Count - 1;
            }
            else
            {
                leftBoundIndex = rightBoundIndex - 1;
            }
            var rightBound = KeyFrames[rightBoundIndex];
            var rightFrame = rightBound.Key;
            var rightKey = rightBound.Value;
            var leftBound = KeyFrames[leftBoundIndex];
            var leftFrame = leftBound.Key;
            var leftKey = leftBound.Value;
            if (leftFrame == rightFrame)
            {
                return CameraKeyFrameToCameraPose(leftKey);
            }
            var t = (float) (frame - leftFrame) / (rightFrame - leftFrame);
            var points = new float[6];
            for (var i = 0; i < 6; i++)
            {
                var p1 = new Vector3(leftKey.Interpolation[i * 4], leftKey.Interpolation[i * 4 + 2]);
                var p2 = new Vector3(leftKey.Interpolation[i * 4 + 1], leftKey.Interpolation[i * 4 + 3]);
                points[i] = CalculBezierPointByTwo(t, p1, p2);
            }
            var x = leftKey.Position.x + points[0] * (rightKey.Position.x - leftKey.Position.x);
            var y = leftKey.Position.y + points[1] * (rightKey.Position.y - leftKey.Position.y);
            var z = leftKey.Position.z + points[2] * (rightKey.Position.z - leftKey.Position.z);
            var rx = leftKey.Rotation.x * 180 / Mathf.PI + points[3] * (rightKey.Rotation.x - leftKey.Rotation.x);
            var ry = leftKey.Rotation.y * 180 / Mathf.PI + points[3] * (rightKey.Rotation.y - leftKey.Rotation.y);
            var rz = leftKey.Rotation.z * 180 / Mathf.PI + points[3] * (rightKey.Rotation.z - leftKey.Rotation.z);
            var focalLength = leftKey.FocalLength + points[4] * (rightKey.FocalLength - leftKey.FocalLength);
            var fov = leftKey.Fov + (rightKey.Fov - leftKey.Fov) * points[5];
            return new CameraPose
            {
                FocalLength = focalLength,
                Fov = fov,
                Orthographic = leftKey.Orthographic,
                Position = new Vector3(x, y, z),
                Rotation = new Vector3(rx, ry, rz)
            };
        }

        private static CameraPose CameraKeyFrameToCameraPose(CameraKeyframe value)
        {
            return new CameraPose
            {
                FocalLength = value.FocalLength,
                Fov = value.Fov,
                Orthographic = value.Orthographic,
                Position = value.Position,
                Rotation = value.Rotation
            };
        }

        public CameraPose GetCameraPose(double time)
        {
            return GetCameraPose((int) (time * 30.0));
        }

        private class CameraKeyframeSearchComparator : IComparer<KeyValuePair<int, CameraKeyframe>>
        {
            public static readonly CameraKeyframeSearchComparator Instance = new CameraKeyframeSearchComparator();

            private CameraKeyframeSearchComparator()
            {
            }

            public int Compare(KeyValuePair<int, CameraKeyframe> x, KeyValuePair<int, CameraKeyframe> y)
            {
                return x.Key.CompareTo(y.Key);
            }
        }


        //https://github.com/lzh1590/MMDCameraPath/blob/master/scripts/VMDCameraWork.cs
        private static float CalculBezierPointByTwo(float t, Vector3 p1, Vector3 p2)
        {
            var p = CalculateBezierPoint(t, Vector3.zero, p1, p2, new Vector3(127, 127, 0));
            var a = p.y / 127.0f;
            return a;
        }

        //https://github.com/lzh1590/MMDCameraPath/blob/master/scripts/VMDCameraWork.cs
        private static Vector3 CalculateBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            var u = 1 - t;
            var tt = t * t;
            var uu = u * u;
            var uuu = uu * u;
            var ttt = tt * t;
            var p = uuu * p0; //first term
            p += 3 * uu * t * p1; //second term
            p += 3 * u * tt * p2; //third term
            p += ttt * p3; //fourth term
            return p;
        }
    }
}