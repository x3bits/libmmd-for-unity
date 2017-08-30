using UnityEngine;

namespace LibMMD.Unity3D
{
    public class AudioFrameTimer : MonoBehaviour {

        private AudioSource audioSource;
        private float time;
        private float length;

        public float Time {
            get {
                return time;
            }
        }

        public float Length {
            get {
                return length;
            }
        }

        void Awake() {
            audioSource = GetComponent<AudioSource> ();
        }
	
        void Update () {
            time = audioSource.time;
            length = GetAudioLength ();
        }

        private float GetAudioLength() {
            var clip = audioSource.clip;
            if (clip == null) {
                return 0.0f;
            }
            return clip.length;
        }


    }
}
