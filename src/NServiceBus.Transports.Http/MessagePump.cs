namespace NServiceBus.Transports.Http
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Logging;
    using Microsoft.Net.Http.Server;
    using Transport;

    class MessagePump : IPushMessages
    {
        public MessagePump(string connectionString)
        {
            listenPrefix = connectionString;
        }

        public Task Init(Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError, CriticalError criticalError, PushSettings settings)
        {
            this.onMessage = onMessage;

            listenCircuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("Listen", TimeSpan.FromSeconds(30), ex => criticalError.Raise("Failed to listen " + settings.InputQueue, ex));
            receiveCircuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("Receive", TimeSpan.FromSeconds(30), ex => criticalError.Raise("Failed to receive from " + settings.InputQueue, ex));

            return Task.CompletedTask;
        }

        public void Start(PushRuntimeSettings limitations)
        {
            runningReceiveTasks = new ConcurrentDictionary<Task, Task>();
            concurrencyLimiter = new SemaphoreSlim(limitations.MaxConcurrency);

            var settings = new WebListenerSettings();
            settings.UrlPrefixes.Add(listenPrefix);
            stopTokenSource = new CancellationTokenSource();
            listener = new WebListener(settings);
            listener.Start();
            workerTask = Task.Run(Listen);
        }

        async Task Listen()
        {
            while (!stopTokenSource.IsCancellationRequested)
            {
                try
                {
                    await Accept().ConfigureAwait(false);
                    listenCircuitBreaker.Success();
                }
                catch (OperationCanceledException)
                {
                    // For graceful shutdown purposes
                }
                catch (Exception ex)
                {
                    Logger.Error("HTTP message pump failed", ex);
                    await listenCircuitBreaker.Failure(ex).ConfigureAwait(false);
                }
            }
        }

        async Task Accept()
        {
            while (!stopTokenSource.IsCancellationRequested)
            {
                var context = await listener.AcceptAsync().ConfigureAwait(false);

                if (stopTokenSource.IsCancellationRequested)
                {
                    return;
                }

                await concurrencyLimiter.WaitAsync(stopTokenSource.Token).ConfigureAwait(false);

                var receiveTask = ReceiveMessage(context);

                runningReceiveTasks.TryAdd(receiveTask, receiveTask);

                // We insert the original task into the runningReceiveTasks because we want to await the completion
                // of the running receives. ExecuteSynchronously is a request to execute the continuation as part of
                // the transition of the antecedents completion phase. This means in most of the cases the continuation
                // will be executed during this transition and the antecedent task goes into the completion state only
                // after the continuation is executed. This is not always the case. When the TPL thread handling the
                // antecedent task is aborted the continuation will be scheduled. But in this case we don't need to await
                // the continuation to complete because only really care about the receive operations. The final operation
                // when shutting down is a clear of the running tasks anyway.
#pragma warning disable 4014
                receiveTask.ContinueWith((t, state) =>
#pragma warning restore 4014
                {
                    var receiveTasks = (ConcurrentDictionary<Task, Task>)state;
                    Task toBeRemoved;
                    receiveTasks.TryRemove(t, out toBeRemoved);
                }, runningReceiveTasks, TaskContinuationOptions.ExecuteSynchronously);
            }
        }

        Task ReceiveMessage(RequestContext context)
        {
            return RunWorkerTask(async state =>
            {
                var messagePump = (MessagePump)state;

                try
                {
                    await ProcessMessage(context).ConfigureAwait(false);
                    messagePump.receiveCircuitBreaker.Success();
                }
                catch (OperationCanceledException)
                {
                    // Intentionally ignored
                }
                catch (Exception ex)
                {
                    Logger.Warn("HTTP receive operation failed", ex);
                    await messagePump.receiveCircuitBreaker.Failure(ex).ConfigureAwait(false);
                }
                finally
                {
                    messagePump.concurrencyLimiter.Release();
                }
            }, this);
        }

        async Task ProcessMessage(RequestContext context)
        {
            byte[] bodyBuffer;
            if (context.Request.ContentLength.HasValue)
            {
                var length = (int)context.Request.ContentLength.Value;
                bodyBuffer = new byte[length];
                await context.Request.Body.ReadAsync(bodyBuffer, 0, length);
            }
            else
            {
                bodyBuffer = new byte[0];
            }

            var headers = context.Request.Headers
                .Where(x => x.Key.StartsWith("X-NSB-"))
                .ToDictionary(x => WebUtility.UrlDecode(x.Key.Substring(6)), x => WebUtility.UrlDecode(x.Value));

            var uri = new Uri(context.Request.RawUrl);
            var id = uri.Segments.Last().Trim('/');

            using (var tokenSource = new CancellationTokenSource())
            {
                try
                {
                    await onMessage(new MessageContext(id, headers, bodyBuffer, new TransportTransaction(), tokenSource, new ContextBag())).ConfigureAwait(false);
                    if (!tokenSource.IsCancellationRequested)
                    {
                        context.Response.StatusCode = 200;
                    }
                    else
                    {
                        context.Response.StatusCode = 503;
                    }
                }
                catch (Exception)
                {
                    context.Response.StatusCode = 500;
                }
            }
        }

        static Task RunWorkerTask(Func<object, Task> func, object state) 
            => Task.Factory.StartNew(func, state, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default).Unwrap();

        public async Task Stop()
        {
            stopTokenSource.Cancel();
            await workerTask.ConfigureAwait(false);
            listener.Dispose();
        }

        string listenPrefix;
        ConcurrentDictionary<Task, Task> runningReceiveTasks;
        SemaphoreSlim concurrencyLimiter;
        CancellationTokenSource stopTokenSource;
        WebListener listener;
        Task workerTask;
        Func<MessageContext, Task> onMessage;
        RepeatedFailuresOverTimeCircuitBreaker listenCircuitBreaker;
        RepeatedFailuresOverTimeCircuitBreaker receiveCircuitBreaker;

        ILog Logger = LogManager.GetLogger<MessagePump>();
    }
}