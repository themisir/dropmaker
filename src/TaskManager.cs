using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dropmaker
{
    public class TaskManager<TState>
    {
        public readonly int ThreadCount;
        public readonly List<TState> States;
        public readonly Action<TState> OnTask;

        public TaskManager(int threadCount, Action<TState> taskRunner)
        {
            States = new List<TState>();
            OnTask = taskRunner;
            ThreadCount = threadCount;
        }

        public void Add(TState state)
        {
            States.Add(state);
        }

        public void Run()
        {
            SemaphoreSlim maxThread = new SemaphoreSlim(ThreadCount);

            foreach (var task in States)
            {
                maxThread.Wait();

                Task.Factory
                    .StartNew(() => OnTask(task), TaskCreationOptions.LongRunning)
                    .ContinueWith((task) => maxThread.Release());
            }
        }
    }
}
