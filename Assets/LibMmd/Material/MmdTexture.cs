namespace LibMMD.Material
{
    public class MmdTexture
    {
        public string TexturePath { get; set; }

        public MmdTexture(string texturePath)
        {
            TexturePath = texturePath;
        }

        protected bool Equals(MmdTexture other)
        {
            return string.Equals(TexturePath, other.TexturePath);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MmdTexture) obj);
        }

        public override int GetHashCode()
        {
            return TexturePath != null ? TexturePath.GetHashCode() : 0;
        }

        private MmdTexture()
        {
            
        }
    }
}