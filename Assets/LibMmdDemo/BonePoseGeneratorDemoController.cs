using System.Collections;
using System.Runtime.InteropServices;
using LibMMD.Reader;
using LibMMD.Unity3D;
using LibMMD.Unity3D.BonePose;
using UnityEngine;
using UnityEngine.UI;

namespace LibMmdDemo
{
    public class BonePoseGeneratorDemoController : MonoBehaviour
    {
        public Text StatusText;

        public string ModelPath;

        public string MotionPath;

        public string BonePoseFileOutputPath;

        protected void Start()
        {
            var model = new PmxReader().Read(ModelPath, new ModelReadConfig {GlobalToonPath = ""});
            Debug.Log("model load finished");
            var motion = new VmdReader().Read(MotionPath);
            Debug.Log("motion load finished" + motion.Length + " frames");
            var bonePoseFileGenerator =
                BonePoseFileGenerator.GenerateAsync(model, motion, BonePoseFileOutputPath);
            StartCoroutine(CheckGenerateStatus(bonePoseFileGenerator));
        }

        private IEnumerator CheckGenerateStatus(BonePoseFileGenerator generator)
        {
            while (true)
            {
                var generatorStatus = generator.Status;
                var statusStr = generatorStatus.ToString();
                if (generatorStatus == BonePoseFileGenerator.GenerateStatus.CalculatingFrames)
                {
                    statusStr = statusStr + " " + generator.CalculatedFrames + "/" + generator.TotalFrames;
                }
                StatusText.text = statusStr;
                if (generatorStatus == BonePoseFileGenerator.GenerateStatus.Failed)
                {
                    break;
                }
                if (generatorStatus == BonePoseFileGenerator.GenerateStatus.Finished)
                {
                    StartPlay();
                    break;
                }
                yield return null;
            }
        }

        private void StartPlay()
        {
            var mmdObj = MmdGameObject.CreateGameObject("MmdGameObject");
            var mmdGameObject = mmdObj.GetComponent<MmdGameObject>();

            mmdGameObject.LoadModel(ModelPath);
            mmdGameObject.LoadBonePoseFile(BonePoseFileOutputPath);
            mmdGameObject.Playing = true;
        }
    }
}