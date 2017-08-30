using System.IO;
using LibMMD.Model;

namespace LibMMD.Reader
{
    public abstract class ModelReader
    {      
        public MmdModel Read(string path, ModelReadConfig config)
        {
            using (var fileStream = new FileStream(path, FileMode.Open))
            {
                using (var bufferedStream = new BufferedStream(fileStream)) {
                    using (var binaryReader = new BinaryReader(bufferedStream))
                    {
                        return Read(binaryReader, config);
                    }
                }
            }
        }

        public abstract MmdModel Read(BinaryReader reader, ModelReadConfig config);
        
        public static MmdModel LoadMmdModel(string path, ModelReadConfig config)
        {
            var fileExt = new FileInfo(path).Extension.ToLower();
            if (".pmd".Equals(fileExt))
            {
                return new PmdReader().Read(path, config);
            }
            if (".pmx".Equals(fileExt))
            {
                return new PmxReader().Read(path, config);
            }
            throw new MmdFileParseException("File " + path +
                                            " is not a MMD model file. File name should ends with \"pmd\" or \"pmx\".");
        }

    }
}