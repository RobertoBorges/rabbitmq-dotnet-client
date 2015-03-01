﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Util;

namespace RabbitMQ.Client
{
    class ConsumerWorkService
    {
        public const int MAX_THUNK_EXECUTION_BATCH_SIZE = 16;
        private TaskScheduler scheduler;
        private BatchingWorkPool<IModel, Action> workPool;
        private int shutdownTimeout;

        public ConsumerWorkService(int shutdownTimeout) :
            this(TaskScheduler.Default, shutdownTimeout) {}

        public ConsumerWorkService(TaskScheduler scheduler, int shutdownTimeout)
        {
            this.workPool = new BatchingWorkPool<IModel, Action>();
            this.shutdownTimeout = shutdownTimeout;
        }

        public int ShutdownTimeout
        {
            get { return shutdownTimeout; }
        }

        public void ExecuteThunk()
        {
            var actions = new List<Action>(MAX_THUNK_EXECUTION_BATCH_SIZE);

            try
            {
                IModel key = this.workPool.NextWorkBlock(ref actions, MAX_THUNK_EXECUTION_BATCH_SIZE);
                if (key == null) { return; }

                try
                {
                    foreach (var fn in actions)
                    {
                        fn();
                    }
                }
                finally
                {
                    if (this.workPool.FinishWorkBlock(key))
                    {
                        var t = new Task(new Action(ExecuteThunk));
                        t.Start();
                    }
                }
            }
            catch (Exception e)
            {
                Thread.CurrentThread.Interrupt();
            }
        }

        public void AddWork(IModel model, Action fn)
        {
            if(this.workPool.AddWorkItem(model, fn))
            {
                var t = new Task(new Action(ExecuteThunk));
                t.Start();
            }
        }

        public void RegisterKey(IModel model)
        {
            this.workPool.RegisterKey(model);
        }

        public void StopWork(IModel model)
        {
            this.workPool.UnregisterKey(model);
        }
    }
}
