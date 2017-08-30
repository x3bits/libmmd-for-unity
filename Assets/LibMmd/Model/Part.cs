namespace LibMMD.Model
{
    public class Part
    {
        public Material.MmdMaterial Material { get; set; }
        public int BaseShift { get; set; } //注意这个和libmmd里的含义不同，这里是三角形顶点数
        public int TriangleIndexNum { get; set; }
    }
}