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
    using Microsoft.Extensions.Primitives;
    using Microsoft.Net.Http.Server;
    using Transport;

    class MessagePump : IPushMessages
    {
        public Task Init(Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError, CriticalError criticalError, PushSettings settings)
        {
            this.onMessage = onMessage;
            this.onError = onError;
            inputQueue = settings.InputQueue;

            listenCircuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("Listen", TimeSpan.FromSeconds(30), ex => criticalError.Raise("Failed to listen " + settings.InputQueue, ex));
            receiveCircuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("Receive", TimeSpan.FromSeconds(30), ex => criticalError.Raise("Failed to receive from " + settings.InputQueue, ex));

            return Task.FromResult(0);
        }

        public void Start(PushRuntimeSettings limitations)
        {
            runningReceiveTasks = new ConcurrentDictionary<Task, Task>();
            concurrencyLimiter = new SemaphoreSlim(limitations.MaxConcurrency);

            var settings = new WebListenerSettings();
            settings.UrlPrefixes.Add(inputQueue);
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
                // ReSharper disable once UnusedVariable
                var ignored = receiveTask.ContinueWith((t, state) =>
                {
                    var receiveTasks = (ConcurrentDictionary<Task, Task>)state;
                    receiveTasks.TryRemove(t, out Task _);
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
                    context.Dispose();
                    messagePump.receiveCircuitBreaker.Success();
                }
                catch (OperationCanceledException)
                {
                    // Intentionally ignored
                }
                catch (Exception ex)
                {
                    context.Abort();
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
                await context.Request.Body.ReadAsync(bodyBuffer, 0, length).ConfigureAwait(false);
            }
            else
            {
                bodyBuffer = new byte[0];
            }

            var headers = context.Request.Headers
                .Where(x => x.Key.StartsWith("X-NSB-"))
                .ToDictionary(x => WebUtility.UrlDecode(x.Key.Substring(6)), x => WebUtility.UrlDecode(x.Value));

            var processingFailures = 0;
            StringValues processingFailuresValues;
            if (context.Request.Headers.TryGetValue("X-NSBHttp-ImmediateFailures", out processingFailuresValues))
            {
                processingFailures = int.Parse(processingFailuresValues[0]);
            }

            var id = context.Request.Path.Trim('/');

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
                catch (Exception ex)
                {
                    var errorHandlingResult = await onError(new ErrorContext(ex, headers, id, bodyBuffer, new TransportTransaction(), processingFailures + 1)).ConfigureAwait(false);
                    if (errorHandlingResult == ErrorHandleResult.Handled)
                    {
                        context.Response.StatusCode = 200;
                    }
                    else
                    {
                        context.Response.StatusCode = 503;
                    }
                }
            }
        }

        static Task RunWorkerTask(Func<object, Task> func, object state) 
            => Task.Factory.StartNew(func, state, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default).Unwrap();

        public async Task Stop()
        {
            listener.Dispose();
            stopTokenSource.Cancel();
            await workerTask.ConfigureAwait(false);
        }

        string inputQueue;
        ConcurrentDictionary<Task, Task> runningReceiveTasks;
        SemaphoreSlim concurrencyLimiter;
        CancellationTokenSource stopTokenSource;
        WebListener listener;
        Task workerTask;
        Func<MessageContext, Task> onMessage;
        RepeatedFailuresOverTimeCircuitBreaker listenCircuitBreaker;
        RepeatedFailuresOverTimeCircuitBreaker receiveCircuitBreaker;

        ILog Logger = LogManager.GetLogger<MessagePump>();
        Func<ErrorContext, Task<ErrorHandleResult>> onError;
    }
}