using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace MultiToolLoader.Tests
{
    public abstract class UITestBase : TestBase
    {
        private Application? _app;
        protected ManualResetEvent _testCompleted = new ManualResetEvent(false);

        [TestInitialize]
        public void UITestInitialize()
        {
            var thread = new Thread(() =>
            {
                _app = new Application();
                _app.Startup += (s, e) => OnApplicationStartup();
                _app.Run();
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            // Wait for the application to start
            _testCompleted.WaitOne();
            _testCompleted.Reset();
        }

        [TestCleanup]
        public void UITestCleanup()
        {
            if (_app != null)
            {
                _app.Dispatcher.Invoke(() =>
                {
                    _app.Shutdown();
                });
            }
        }

        protected virtual void OnApplicationStartup()
        {
            _testCompleted.Set();
        }

        protected void RunOnUIThread(Action action)
        {
            if (_app == null) throw new InvalidOperationException("Application not initialized");

            _app.Dispatcher.Invoke(action);
        }

        protected T RunOnUIThread<T>(Func<T> func)
        {
            if (_app == null) throw new InvalidOperationException("Application not initialized");

            return _app.Dispatcher.Invoke(func);
        }

        protected async Task RunOnUIThreadAsync(Func<Task> action)
        {
            if (_app == null) throw new InvalidOperationException("Application not initialized");

            await _app.Dispatcher.InvokeAsync(action);
        }
    }
}