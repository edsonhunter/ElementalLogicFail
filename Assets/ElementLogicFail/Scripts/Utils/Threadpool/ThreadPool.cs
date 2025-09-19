using System;

namespace ElementLogicFail.Scripts.Utils.Threadpool
{
    public class ThreadPool
    {
        public static ThreadPoolController ThreadController { get; private set; }

        public static void Init(ThreadPoolController controller)
        {
            if (ThreadController != null)
            {
                ThreadPoolController.Destroy(controller.gameObject);
                ThreadController = controller;
            }

            ThreadController = controller;
        }

        public static void Run(Action action)
        {
            ThreadController.Run(TimeSpan.Zero, action);
        }
        
        public static void Run(TimeSpan delay, Action action)
        {
            ThreadController.Run(delay, action);
        }

        public static void RunOnMainThread(Action action)
        {
            ThreadController.RunOnMainThread(TimeSpan.Zero, action);
        }

        public static void RunOnMainThread(TimeSpan delay, Action action)
        {
            ThreadController.RunOnMainThread(delay, action);
        }

        public static bool IsMainThread()
        {
            return ThreadController.IsMainThread();
        }
    }
}