using System;
using System.Collections.Generic;
using System.Threading;

namespace Dropmaker
{
    public class TaskManager
    {
        public readonly Queue<Action> Tasks;
        public readonly List<Thread> Threads;
        public int TotalTasks { get; private set; }
        public int CompletedTasks => TotalTasks - Tasks.Count;
        public int ThreadCount { get; private set; }

        public TaskManager(int threadCount)
        {
            Tasks = new Queue<Action>();
            Threads = new List<Thread>();
            ThreadCount = threadCount;
        }

        public void AddTask(Action task)
        {
            Tasks.Enqueue(task);
        }

        public void Run()
        {
            TotalTasks = Tasks.Count;

            for (var i = 0; i < ThreadCount; i++)
            {
                Thread thread = new Thread(Work);
                Threads.Add(thread);
                thread.Start();
            }
        }

        protected void Work()
        {
            while (Tasks.Count > 0)
            {
                Action task;
                lock (Tasks)
                {
                    task = Tasks.Dequeue();
                }
                task.Invoke();
            }
        }
    }
}
