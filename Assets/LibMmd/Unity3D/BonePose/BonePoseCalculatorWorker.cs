using System.Collections.Generic;
using System.Threading;

namespace LibMMD.Unity3D.BonePose
{
    public class BonePoseCalculatorWorker
    {
        private readonly HashSet<BonePosePreCalculator> _calculators = new HashSet<BonePosePreCalculator>();
        private volatile Thread _workThread;

        //返回id
        public void Start(BonePosePreCalculator calculator)
        {
            lock (this)
            {
                _calculators.Add(calculator);
                if (_workThread == null)
                {
                    _workThread = new Thread(Run);
                    _workThread.Start();
                }
            }
            lock (_calculators)
            {
                Monitor.PulseAll(_calculators);
            }
        }

        public void Stop(BonePosePreCalculator calculator)
        {
            lock (this)
            {
                _calculators.Remove(calculator);
            }
        }

        private void Run()
        {
            while (true)
            {
                var shouldContinue = false;
                List<BonePosePreCalculator> calculators;
                lock (this)
                {
                    calculators = new List<BonePosePreCalculator>(_calculators);
                }
                foreach (var calculator in calculators)
                {
                    if (calculator.Step())
                    {
                        shouldContinue = true;
                    }
                }
                if (shouldContinue) continue;
                lock (_calculators)
                {
                    Monitor.Wait(_calculators);
                }
            }
        }

        public void NotifyTake(BonePosePreCalculator calculator)
        {
            lock (_calculators)
            {
                Monitor.PulseAll(_calculators);
            }
        }
    }
}