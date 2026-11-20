using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.UI;

namespace RevitMCP.Plugin.Core
{
    public class RevitTask
    {
        public Func<UIApplication, object?> Action { get; set; } = null!;
        public TaskCompletionSource<object?> CompletionSource { get; set; } = null!;
        public string ActionSummary { get; set; } = string.Empty;
        public string AgentName { get; set; } = string.Empty;
        public DateTime QueuedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public class RevitTaskDispatcher : IExternalEventHandler
    {
        private readonly ExternalEvent _externalEvent;
        private readonly ConcurrentQueue<RevitTask> _queue = new ConcurrentQueue<RevitTask>();
        private Action<RevitTask>? _onTaskExecuting;
        private Action<RevitTask, bool, string?>? _onTaskCompleted;

        public RevitTaskDispatcher()
        {
            _externalEvent = ExternalEvent.Create(this);
        }

        public void RegisterCallbacks(
            Action<RevitTask> onTaskExecuting,
            Action<RevitTask, bool, string?> onTaskCompleted)
        {
            _onTaskExecuting = onTaskExecuting;
            _onTaskCompleted = onTaskCompleted;
        }

        public Task<object?> DispatchAsync(
            Func<UIApplication, object?> action,
            string actionSummary = "",
            string agentName = "Agent",
            CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (cancellationToken.IsCancellationRequested)
            {
                tcs.TrySetCanceled(cancellationToken);
                return tcs.Task;
            }

            var task = new RevitTask
            {
                Action = action,
                CompletionSource = tcs,
                ActionSummary = actionSummary,
                AgentName = agentName,
                QueuedAtUtc = DateTime.UtcNow
            };

            _queue.Enqueue(task);
            _externalEvent.Raise();

            return tcs.Task;
        }

        public void Execute(UIApplication app)
        {
            while (_queue.TryDequeue(out var task))
            {
                _onTaskExecuting?.Invoke(task);
                try
                {
                    object? result = task.Action(app);
                    task.CompletionSource.TrySetResult(result);
                    _onTaskCompleted?.Invoke(task, true, null);
                }
                catch (Exception ex)
                {
                    task.CompletionSource.TrySetException(ex);
                    _onTaskCompleted?.Invoke(task, false, ex.Message);
                }
            }
        }

        public string GetName()
        {
            return "RevitMcpTaskDispatcher";
        }
    }
}
