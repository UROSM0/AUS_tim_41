using Common;
using System;
using System.Collections.Generic;
using System.Threading;

namespace ProcessingModule
{
    /// <summary>
    /// Class containing logic for automated work.
    /// </summary>
    public class AutomationManager : IAutomationManager, IDisposable
	{
		private Thread automationWorker;
        private AutoResetEvent automationTrigger;
        private IStorage storage;
		private IProcessingManager processingManager;
		private int delayBetweenCommands;
        private IConfiguration configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="AutomationManager"/> class.
        /// </summary>
        /// <param name="storage">The storage.</param>
        /// <param name="processingManager">The processing manager.</param>
        /// <param name="automationTrigger">The automation trigger.</param>
        /// <param name="configuration">The configuration.</param>
        public AutomationManager(IStorage storage, IProcessingManager processingManager, AutoResetEvent automationTrigger, IConfiguration configuration)
		{
			this.storage = storage;
			this.processingManager = processingManager;
            this.configuration = configuration;
            this.automationTrigger = automationTrigger;
        }

        /// <summary>
        /// Initializes and starts the threads.
        /// </summary>
		private void InitializeAndStartThreads()
		{
			InitializeAutomationWorkerThread();
			StartAutomationWorkerThread();
		}

        /// <summary>
        /// Initializes the automation worker thread.
        /// </summary>
		private void InitializeAutomationWorkerThread()
		{
			automationWorker = new Thread(AutomationWorker_DoWork);
			automationWorker.Name = "Aumation Thread";
		}

        /// <summary>
        /// Starts the automation worker thread.
        /// </summary>
		private void StartAutomationWorkerThread()
		{
			automationWorker.Start();
		}


		private void AutomationWorker_DoWork()
		{

            const ushort STOP_ADDR = 2000;
            const ushort V1_ADDR = 2002;
            const ushort P1_ADDR = 2005;
            const ushort P2_ADDR = 2006;

            bool firstScan = true;
            int lastStop = 0;

            void WriteDO(IDigitalPoint pt, ushort addr, int value)
            {
                if (pt?.ConfigItem == null) return;
                if (pt.RawValue == value) return;
                processingManager.ExecuteWriteCommand(
                    pt.ConfigItem,
                    configuration.GetTransactionId(),
                    configuration.UnitAddress,
                    addr,
                    value
                );
            }

            while (!disposedValue)
            {
                try
                {
                    var ids = new List<PointIdentifier>
                            {
                            new PointIdentifier(PointType.DIGITAL_OUTPUT, STOP_ADDR),
                            new PointIdentifier(PointType.DIGITAL_OUTPUT, V1_ADDR),
                            new PointIdentifier(PointType.DIGITAL_OUTPUT, P1_ADDR),
                            new PointIdentifier(PointType.DIGITAL_OUTPUT, P2_ADDR),
                            };

                    var points = storage.GetPoints(ids);

                    var pSTOP = points[0] as IDigitalPoint;
                    var pV1 = points[1] as IDigitalPoint;
                    var pP1 = points[2] as IDigitalPoint;
                    var pP2 = points[3] as IDigitalPoint;

                    if (pSTOP == null || pV1 == null || pP1 == null || pP2 == null)
                    {
                        automationTrigger?.WaitOne(delayBetweenCommands);
                        continue;
                    }

                    int stop = pSTOP.RawValue;
                    int v1 = pV1.RawValue;
                    int p1 = pP1.RawValue;
                    int p2 = pP2.RawValue;

                    if (firstScan)
                    {
                        lastStop = stop;
                        firstScan = false;
                    }

                    if (lastStop != stop)
                    {
                        if (stop == 1)
                        {
                            WriteDO(pP1, P1_ADDR, 0);
                            WriteDO(pP2, P2_ADDR, 0);
                        }
                        else 
                        {
                            WriteDO(pV1, V1_ADDR, 0);
                        }

                        lastStop = stop;
                    }

                }
                catch
                {
                    automationTrigger?.WaitOne(delayBetweenCommands);
                }
            }
        }

		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls


        /// <summary>
        /// Disposes the object.
        /// </summary>
        /// <param name="disposing">Indication if managed objects should be disposed.</param>
		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
				}
				disposedValue = true;
			}
		}


		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// GC.SuppressFinalize(this);
		}

        /// <inheritdoc />
        public void Start(int delayBetweenCommands)
		{
			this.delayBetweenCommands = delayBetweenCommands*1000;
            InitializeAndStartThreads();
		}

        /// <inheritdoc />
        public void Stop()
		{
			Dispose();
		}
		#endregion
	}
}
