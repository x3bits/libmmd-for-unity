using UnityEngine;

namespace LibMMD.Motion
{
    public class MaterialImage
    {
        public Vector4 Diffuse { get; set; }
        public Vector4 Specular { get; set; }
        public Vector4 Ambient { get; set; }
        public float Shininess { get; set; }
        public Vector4 EdgeColor { get; set; }
        public float EdgeSize { get; set; }
        public Vector4 Texture { get; set; }
        public Vector4 SubTexture { get; set; }
        public Vector4 ToonTexture { get; set; }

        public MaterialImage(float value)
        {
            Init(value);
        }

        public void Init(float value) {
            var seed = new Vector4();
            seed[0] = seed[1] = seed[2] = seed[3] = Shininess = EdgeSize = value;
            Diffuse = Specular = Ambient = EdgeColor = Texture = SubTexture = ToonTexture = seed;
        }
    }
}