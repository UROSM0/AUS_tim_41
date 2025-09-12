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
            const ushort L_ADDR = 1000;

         
            const int MAX_LITERS = 12000;       
            const int L_PER_METER = 3000;       
            const int DRAINAGE_L = 2 * L_PER_METER; 
            const int INFLOW_UNIT = 80;           
            const int OUTFLOW_LPS = 50;          

            bool firstScan = true;
            int lastStop = 0;

            var conv = new EGUConverter();

            
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

            void WriteAO(IAnalogPoint pt, ushort addr, int eguLiters)
            {
                if (pt?.ConfigItem == null) return;
                int clamped = Math.Max(0, Math.Min(eguLiters, MAX_LITERS));
                int raw = conv.ConvertToRaw(pt.ConfigItem.ScaleFactor, pt.ConfigItem.Deviation, clamped);
                if (pt.RawValue == raw) return;
                processingManager.ExecuteWriteCommand(
                    pt.ConfigItem,
                    configuration.GetTransactionId(),
                    configuration.UnitAddress,
                    addr,
                    raw
                );
            }

           
            DateTime lastTick = DateTime.UtcNow;

            while (!disposedValue)
            {
                try
                {
                    
                    automationTrigger?.WaitOne();  

                    
                    DateTime now = DateTime.UtcNow;
                    double dt = (now - lastTick).TotalSeconds;
                    lastTick = now;

                    
                    if (dt < 0.1) dt = 0.1;
                    if (dt > 5.0) dt = 5.0;

                  
                    var ids = new List<PointIdentifier>
            {
                new PointIdentifier(PointType.DIGITAL_OUTPUT, STOP_ADDR),
                new PointIdentifier(PointType.DIGITAL_OUTPUT, V1_ADDR),
                new PointIdentifier(PointType.DIGITAL_OUTPUT, P1_ADDR),
                new PointIdentifier(PointType.DIGITAL_OUTPUT, P2_ADDR),
                new PointIdentifier(PointType.ANALOG_OUTPUT,  L_ADDR),
            };

                    var points = storage.GetPoints(ids);

                    var pSTOP = points[0] as IDigitalPoint;
                    var pV1 = points[1] as IDigitalPoint;
                    var pP1 = points[2] as IDigitalPoint;
                    var pP2 = points[3] as IDigitalPoint;
                    var pL = points[4] as IAnalogPoint;

                    if (pSTOP == null || pV1 == null || pP1 == null || pP2 == null || pL == null)
                        continue; 

                    int stop = pSTOP.RawValue; 
                    int v1 = pV1.RawValue;
                    int p1 = pP1.RawValue;
                    int p2 = pP2.RawValue;

                    int L = (int)Math.Round(
                        (pL.EguValue != 0 || pL.RawValue == 0)
                            ? pL.EguValue
                            : conv.ConvertToEGU(pL.ConfigItem.ScaleFactor, pL.ConfigItem.Deviation, pL.RawValue)
                    );

                    int highAlarm = (int)pL.ConfigItem.HighLimit;
                    int lowAlarm = (int)pL.ConfigItem.LowLimit;

                    
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

                    
                    if (stop == 0)
                    {
                        if (v1 != 0) WriteDO(pV1, V1_ADDR, 0);
                    }
                    else 
                    {
                        if (p1 != 0) WriteDO(pP1, P1_ADDR, 0);
                        if (p2 != 0) WriteDO(pP2, P2_ADDR, 0);
                    }

                    
                    int inflowLps = (2 * p1 + 1 * p2) * INFLOW_UNIT;
                    int outflowLps = (v1 == 1 && L > DRAINAGE_L) ? OUTFLOW_LPS : 0;

                    int newL = (int)Math.Round(L + (inflowLps - outflowLps) * dt);
                    newL = Math.Max(0, Math.Min(newL, MAX_LITERS));

                    if (newL != L)
                        WriteAO(pL, L_ADDR, newL);

                    
                    if (newL >= highAlarm)
                    {
                        WriteDO(pP1, P1_ADDR, 0);
                        WriteDO(pP2, P2_ADDR, 0);
                        WriteDO(pV1, V1_ADDR, 1);
                        WriteDO(pSTOP, STOP_ADDR, 1);
                    }
                }
                catch
                {
                    
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
