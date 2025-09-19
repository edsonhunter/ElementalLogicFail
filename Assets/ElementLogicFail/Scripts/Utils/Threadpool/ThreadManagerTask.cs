using System;

namespace ElementLogicFail.Scripts.Utils.Threadpool
{
    public class ThreadManagerTask
    {
        private TimeSpan _delay;
        private Action _action;
        private bool _isDestroyed => _destroyed;
        private volatile bool _destroyed;
        private string _stackTraceEntry;

        public ThreadManagerTask(Action action, TimeSpan delay, string stackTraceEntry)
        {
            _action = action;
            _delay = delay;
            _stackTraceEntry = stackTraceEntry;
            _destroyed = false;
        }

        public void Callback(object obj)
        {
            ThreadPoolController threadController = null;

            if (obj is not ThreadPoolController)
            {
                throw new InvalidOperationException($"Thread pool controller object must be of type {nameof(ThreadPoolController)}");
            }
            threadController = (ThreadPoolController)obj;

            if (_delay != TimeSpan.Zero)
            {
                System.Threading.Thread.Sleep(_delay);
            }

            if (_isDestroyed)
            {
                return;
            }

            try
            {
                _action();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception: {e}");
                throw;
            }
            finally
            {
                if (threadController != null && _isDestroyed)
                {
                    threadController.FinalizeTask(this);
                }
            }
        }

        internal void DestroyTask()
        {
            _destroyed = true;
        }
    }
}