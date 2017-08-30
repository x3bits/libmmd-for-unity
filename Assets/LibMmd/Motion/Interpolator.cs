using System;
using LibMMD.Util;
using UnityEngine;

namespace LibMMD.Motion
{
    public class Interpolator
    {
        public Interpolator()
        {
            _c0 = Vector2.zero;
            _c1 = new Vector2(3.0f, 3.0f);
            _presamples = new float[PresampleResolution];
            _isLinear = true;
        }

        public Vector2 GetC(int i)
        {
            if (i == 0)
            {
                return _c0 / 3.0f;
            }
            return _c1 / 3.0f;
        }

        public void SetC(Vector2 c0, Vector2 c1)
        {
            _c0 = c0 * 3;
            _c1 = c1 * 3;
            PreSample();
        }

        public float Calculate(float x)
        {
            if (_isLinear)
            {
                return x;
            }
            x *= PresampleResolution - 1;
            var ix = (int) x;
            var r = x - ix;
            if (ix < PresampleResolution - 1)
            {
                return (1.0f - r) * _presamples[ix] + r * _presamples[ix + 1];
            }
            return _presamples[PresampleResolution - 1];
        }

        private void PreSample()
        {
            if (Math.Abs(_c0.x - _c0.y) < Tools.MmdMathConstEps && Math.Abs(_c1.x - _c1.y) < Tools.MmdMathConstEps)
            {
                _isLinear = true;
            }
            else
            {
                _isLinear = false;
                for (var i = 0; i < PresampleResolution; ++i)
                {
                    var x = i / (float) (PresampleResolution - 1);
                    _presamples[i] = Interpolate(x);
                }
            }
        }

        private float Interpolate(float x)
        {
            var l = 0.0f;
            var r = 1.0f;
            float m, lm = 0.0f, rm;
            for (var i = 0; i < 32; ++i)
            {
                lm = (l + r) * 0.5f;
                rm = 1.0f - lm;
                m = lm * (rm * (rm * _c0.x + lm * _c1.x) + lm * lm);
                if (Math.Abs(m - x) < Tools.MmdMathConstEps)
                {
                    break;
                }
                if (m > x)
                {
                    r = lm;
                }
                else
                {
                    l = lm;
                }
            }
            rm = 1.0f - lm;
            return lm * (rm * (rm * _c0.y + lm * _c1.y) + lm * lm);
        }

        private const int PresampleResolution = 32;
        private Vector2 _c0, _c1;
        private bool _isLinear;
        private float[] _presamples;
    }
}