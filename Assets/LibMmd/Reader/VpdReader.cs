using System;
using System.IO;
using LibMMD.Motion;
using LibMMD.Util;
using UnityEngine;

namespace LibMMD.Reader
{
    public class VpdReader
    {
        public static MmdPose Read(string path)
        {
            var reader = new StreamReader(path, Tools.JapaneseEncoding);
            var ret = new MmdPose();
            ReadMagic(reader);
            ret.ModelName = ReadModelName(reader);
            ReadBoneCount(reader);
            while (true)
            {
                if (!ReadBonePose(reader, ret))
                {
                    break;
                }
            }
            return ret;
        }

        private static bool ReadBonePose(TextReader reader, MmdPose ret)
        {
            var boneName = ReadBoneName(reader);
            if (boneName == null)
            {
                return false;
            }
            var bonePosition = ReadBonePosition(reader);
            var boneQuaternion = ReadBoneQuaternion(reader);
            ReadBonePoseEnd(reader);
            ret.BonePoses.Add(boneName, new BonePose{Translation = bonePosition, Rotation = boneQuaternion});
            return true;
        }

        private static void ReadBonePoseEnd(TextReader reader)
        {
            var line = ReadNonEmptyLine(reader);
            if (line == null || !"}".Equals(line))
            {
                throw new MmdFileParseException("Not a valid vpd file. Invalid bone end line");
            }
        }

        private static Quaternion ReadBoneQuaternion(TextReader reader)
        {
            var line = ReadNonEmptyLine(reader);
            if (line == null)
            {
                throw new MmdFileParseException("Not a valid vpd file. Need bone quaternion line");
            }
            line = RemoveSemicolon(line);
            var values = line.Split(',');
            if (values.Length != 4)
            {
                throw new MmdFileParseException("Not a valid vpd file. Invalid bone quaternion line");
            }
            return new Quaternion(float.Parse(values[0]), float.Parse(values[1]), float.Parse(values[2]), float.Parse(values[3]));
        }

        private static Vector3 ReadBonePosition(TextReader reader)
        {
            var line = ReadNonEmptyLine(reader);
            if (line == null)
            {
                throw new MmdFileParseException("Not a valid vpd file. Need bone position line");
            }
            line = RemoveSemicolon(line);
            var values = line.Split(',');
            if (values.Length != 3)
            {
                throw new MmdFileParseException("Not a valid vpd file. Invalid bone position line");
            }
            return new Vector3(float.Parse(values[0]), float.Parse(values[1]), float.Parse(values[2]));
        }

        private static string ReadBoneName(TextReader reader)
        {
            var line = ReadNonEmptyLine(reader);
            if (line == null)
            {
                return null;
            }
            var pos = line.IndexOf('{');
            if (pos < 0)
            {
                throw new MmdFileParseException("Not a valid vpd file. Invalid bone name line");
            }
            return line.Substring(pos + 1);
        }

        private static void ReadMagic(TextReader reader)
        {
            var magic = ReadNonEmptyLine(reader);
            if (!"Vocaloid Pose Data file".Equals(magic))
            {
                throw new MmdFileParseException("Not a valid vpd file. File not started with vpd magic line.");
            }
        }

        private static string ReadModelName(TextReader reader)
        {
            var line = ReadNonEmptyLine(reader);
            if (line == null || !line.EndsWith(".osm;"))
            {
                throw new MmdFileParseException("Not a valid vpd file. Invalid model name line");
            }
            return line.Substring(0, line.Length - 5);
        }

        private static int ReadBoneCount(TextReader reader)
        {
            var line = ReadNonEmptyLine(reader);
            return int.Parse(RemoveSemicolon(line));
        }

        private static string ReadNonEmptyLine(TextReader reader)
        {
            while (true)
            {
                var line = reader.ReadLine();
                if (line == null)
                {
                    return null;
                }
                line = RemoveComment(line);
                if (!string.IsNullOrEmpty(line))
                {
                    return line;
                }
            }
        }

        private static string RemoveComment(string s)
        {
            var pos = s.LastIndexOf("//", StringComparison.Ordinal);
            if (pos >= 0)
            {
                s = s.Substring(0, pos);
            }
            return s.Trim();
        }

        private static string RemoveSemicolon(string line)
        {
            if (line == null || !line.EndsWith(";"))
            {
                throw new MmdFileParseException("Not a valid vpd file. Line not ends with semicolon");
            }
            return line.Substring(0, line.Length - 1);
        }
    }
}