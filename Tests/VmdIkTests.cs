using System;
using System.IO;
using System.Text;
using LibMMD.Model;
using LibMMD.Motion;
using LibMMD.Reader;
using LibMMD.Util;

// Standalone regression tests; run with Run-VmdIkTests.ps1.
class VmdIkTests
{
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static void Name(BinaryWriter writer, string name, int size)
    {
        var bytes = new byte[size];
        var text = Tools.JapaneseEncoding.GetBytes(name);
        Array.Copy(text, bytes, text.Length);
        writer.Write(bytes);
    }

    static void Key(BinaryWriter writer, int frame, string name, bool enabled)
    {
        writer.Write(frame);
        writer.Write((byte)0); // Visibility must not turn off all IKs.
        writer.Write(1);
        Name(writer, name, 20);
        writer.Write((byte)(enabled ? 1 : 0));
    }

    static byte[] Fixture(int sections)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            Name(writer, "Vocaloid Motion Data 0002", 30);
            Name(writer, "test", 20);
            writer.Write(0); // Bones.
            writer.Write(0); // Morphs.
            if (sections >= 1)
            {
                writer.Write(1); // Camera: exercise the real 61-byte record layout.
                writer.Write(new byte[61]);
            }
            if (sections >= 2) { writer.Write(1); writer.Write(new byte[28]); }
            if (sections >= 3) { writer.Write(1); writer.Write(new byte[9]); }
            if (sections >= 4)
            {
                writer.Write(5);
                Key(writer, 30, "左足ＩＫ", true);
                Key(writer, 10, "左足ＩＫ", true);
                Key(writer, 20, "右足ＩＫ", false);
                Key(writer, 10, "左足ＩＫ", false); // Last duplicate wins.
                Key(writer, 60, "右足ＩＫ", true);
            }
            return stream.ToArray();
        }
    }

    static MmdMotion Read(byte[] bytes)
    {
        using (var reader = new BinaryReader(new MemoryStream(bytes)))
            return new VmdReader().Read(reader);
    }

    static void Main()
    {
        for (var sections = 0; sections <= 3; ++sections)
        {
            var old = Read(Fixture(sections));
            Check(old.IkMotions.Count == 0 && old.GetIkEnabled("左足ＩＫ", 100), "Legacy VMD default");
        }
        var bytes = Fixture(4);
        var motion = Read(bytes);
        Check(motion.Length == 60 && motion.IkMotions.Count == 2, "IK-only duration and Japanese names");
        Check(motion.GetIkEnabled("左足ＩＫ", 9.999), "Default before first key");
        Check(!motion.GetIkEnabled("左足ＩＫ", 10), "Exact off key and duplicate resolution");
        Check(!motion.GetIkEnabled("左足ＩＫ", 29.999), "Hold without interpolation");
        Check(motion.GetIkEnabled("左足ＩＫ", 30), "Exact on key");
        Check(!motion.GetIkEnabled("右足ＩＫ", 40), "Independent IK tracks");
        Check(motion.GetIkEnabled("unknown", 40), "Unknown IK defaults on");

        using (var reader = new BinaryReader(new MemoryStream(bytes)))
        {
            new VmdReader().ReadCameraMotion(reader);
            Check(reader.BaseStream.Position == 123, "Camera parser position preserved");
        }
        var truncated = new byte[bytes.Length - 1];
        Array.Copy(bytes, truncated, truncated.Length);
        try { Read(truncated); throw new Exception("Truncated IK accepted"); }
        catch (EndOfStreamException) { }
        var partialCount = new byte[59];
        Array.Copy(Fixture(0), partialCount, 58);
        try { Read(partialCount); throw new Exception("Partial section count accepted"); }
        catch (EndOfStreamException) { }

        var model = new MmdModel { Vertices = new Vertex[0], Bones = new Bone[0],
            Morphs = new Morph[0], Parts = new Part[0] };
        var poser = new Poser(model);
        model.Bones = new[] { new Bone { Name = "左足ＩＫ", HasIk = true } };
        poser.BoneImages = new[] { new BoneImage { HasIk = true } };
        var player = new MotionPlayer(motion, poser);
        player.SeekFrame(10);
        Check(!poser.BoneImages[0].IkEnabled, "Frame seek applies off");
        player.SeekTime(1.0);
        Check(poser.BoneImages[0].IkEnabled, "Time seek applies on at 30fps");
        player.SeekTime(0.5);
        Check(!poser.BoneImages[0].IkEnabled, "Backward seek restores off");
        player.SeekFrame(0);
        Check(poser.BoneImages[0].IkEnabled, "Rewind restores default");
        player.SeekFrame(15);
        new MotionPlayer(new MmdMotion(), poser).SeekFrame(15);
        Check(poser.BoneImages[0].IkEnabled, "Switching to a motion without IK resets state");
        Console.WriteLine("PASS: VMD IK parsing, legacy files, camera, truncation, and seek states");
    }
}
