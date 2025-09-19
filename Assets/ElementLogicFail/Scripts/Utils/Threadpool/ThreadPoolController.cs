using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace ElementLogicFail.Scripts.Utils.Threadpool
{
    public class ThreadPoolController : MonoBehaviour
    {
        private ConcurrentQueue<(Action action, string stack)> _mainThreadActions = new();

        private System.Threading.Thread _mainThread;
        private IList<ThreadManagerTask> _tasks = new List<ThreadManagerTask>();
        private object _lock = new();

        private void Awake()
        {
            name = nameof(ThreadPoolController);
        }

        private void Start()
        {
            DontDestroyOnLoad(this);
        }

        internal void Run(TimeSpan delay, Action action)
        {
            var stackFrame = new StackTrace(true).GetFrame(1);
            var task = new ThreadManagerTask(action, delay, stackFrame.ToString());

            lock (_lock)
            {
                _tasks.Add(task);
            }

            System.Threading.ThreadPool.QueueUserWorkItem(task.Callback, this);
        }

        internal void RunOnMainThread(TimeSpan delay, Action action)
        {
            if (delay == TimeSpan.Zero)
            {
                if (System.Threading.Thread.CurrentThread == _mainThread)
                {
                    action();
                }
                else
                {
                    var stackFrame = new StackTrace(true).GetFrame(1);
                    _mainThreadActions.Enqueue((action, stackFrame.ToString()));
                }
            }
            else
            {
                Run(delay, () => { RunOnMainThread(delay, action); });
            }
        }

        internal void FinalizeTask(ThreadManagerTask task)
        {
            lock (_lock)
            {
                var index = _tasks.IndexOf(task);
                if (index < 0)
                {
                    return;
                }

                _tasks.RemoveAt(index);
            }
        }

        private void Update()
        {
            while (_mainThreadActions.TryDequeue(out (Action action, string stack) result))
            {
                try
                {
                    result.action();
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError(
                        $"Exception on MainThresd: {e.GetType()} triggered by action:" +
                        $" {result.action.Method.Name}|{result.action.Method.DeclaringType.AssemblyQualifiedName} " +
                        $"stack: {result.stack}");
                }
            }
        }

        internal bool IsMainThread()
        {
            return System.Threading.Thread.CurrentThread == _mainThread;
        }

        private void DestroyTasks()
        {
            lock (_lock)
            {
                foreach (var task in _tasks)
                {
                    task.DestroyTask();
                }
                _tasks.Clear();
            }
        }

        private void OnDestroy()
        {
            DestroyTasks();
        }
    }
}