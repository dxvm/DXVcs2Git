using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace DXVcs2Git.Core.Git {
    public static class FuncExtensions {
        public static void Retry(this Action block, int retries = 2) {
            ((Func<object>)(() => {
                block();
                return (object)null;
            })).Retry(retries);
        }

        public static T Retry<T>(this Func<T> block, int retries = 2) {
            while (true) {
                try {
                    return block();
                }
                catch (Exception ex) {
                    if (!ex.CanRetry() || retries <= 0)
                        throw;
                    --retries;
                    Thread.Sleep(10);
                }
            }
        }
    }

    public static class LinqExtensions {
        public static void ForEach<T>(this IEnumerable<T> en, Action<T> action) {
            if (en == null)
                return;
            foreach (var x in en) {
                action(x);
            }
        }
    }

    public static class ObservableEx {
        static readonly ConcurrentDictionary<string, object> currentObservables = new ConcurrentDictionary<string, object>();
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")] public static readonly Func<int, TimeSpan> ExpontentialBackoff =
            n => TimeSpan.FromSeconds(Math.Pow(n, 2.0));

        public static IObservable<IReadOnlyList<T>> ToReadOnlyList<T>(this IObservable<T> source) {
            return source.ToList().Select(list => new ReadOnlyCollection<T>(list));
        }

        public static IObservable<List<T>> ToConcreteList<T>(this IObservable<T> source) {
            return source.Aggregate(new List<T>(), (list, item) => {
                list.Add(item);
                return list;
            });
        }

        public static IObservable<TSource> CatchNonCritical<TSource>(this IObservable<TSource> first, Func<Exception, IObservable<TSource>> second) {
            return first.Catch((Func<Exception, IObservable<TSource>>)(e => {
                if (!e.IsCriticalException())
                    return second(e);
                return Observable.Throw<TSource>(e);
            }));
        }

        public static IObservable<TSource> CatchNonCritical<TSource>(this IObservable<TSource> first, IObservable<TSource> second) {
            return first.CatchNonCritical(e => second);
        }

        public static IObservable<T> LogErrors<T>(this IObservable<T> observable, string message = null) {
            return Observable.Create((Func<IObserver<T>, IDisposable>)(subj => observable.Subscribe(subj.OnNext, ex => {
                string str = message ?? "0x" + observable.GetHashCode().ToString("x", CultureInfo.InvariantCulture);
                Log.Message(string.Format(CultureInfo.InvariantCulture, "{0} failed.", new object[1] {
                    str
                }), ex);
                subj.OnError(ex);
            }, subj.OnCompleted)));
        }

        public static IObservable<T> RateLimit<T>(this IObservable<T> observable, TimeSpan interval, IScheduler scheduler) {
            return observable.RateLimitImpl(interval, scheduler);
        }

        public static IObservable<T> RateLimit<T>(this IObservable<T> observable, TimeSpan interval) {
            return observable.RateLimitImpl(interval, null);
        }

        static IObservable<T> RateLimitImpl<T>(this IObservable<T> observable, TimeSpan interval, IScheduler scheduler) {
            if (interval <= TimeSpan.Zero)
                throw new ArgumentException("The interval must not be negative", "interval");
            DateTimeOffset lastSeen = DateTimeOffset.MinValue;
            return (scheduler == null ? observable.Timestamp() : observable.Timestamp(scheduler)).Where(item => {
                if (!(lastSeen == DateTimeOffset.MinValue))
                    return item.Timestamp.Ticks > lastSeen.Ticks + interval.Ticks;
                return true;
            }).Do(item => lastSeen = item.Timestamp).Select(item => item.Value);
        }

        public static IObservable<T> PublishAsync<T>(this IObservable<T> observable) {
            IConnectableObservable<T> connectableObservable = observable.Multicast(new AsyncSubject<T>());
            connectableObservable.Connect();
            return connectableObservable;
        }

        public static IObservable<T> StartWithRetry<T>(Func<IObservable<T>> block, int retries) {
            return StartWithRetry(block, retries);
        }

        public static IObservable<T> StartWithRetry<T>(Func<IObservable<T>> block, int retries, IScheduler scheduler) {
            return
                Observable.Defer(
                    () =>
                        Observable.Start(block, scheduler)
                            .Merge()
                            .Select(item => new Tuple<bool, T, Exception>(true, item, null))
                            .Catch((Func<Exception, IObservable<Tuple<bool, T, Exception>>>)(e => {
                                if (!e.CanRetry())
                                    return Observable.Return(new Tuple<bool, T, Exception>(false, default(T), e));
                                return Observable.Throw<Tuple<bool, T, Exception>>(e);
                            }))).Retry(retries).SelectMany(t => {
                                if (!t.Item1)
                                    return Observable.Throw<T>(t.Item3);
                                return Observable.Return(t.Item2);
                            }).PublishAsync();
        }
        public static IObservable<T> CurrentOrCreate<T>(Func<IObservable<T>> sourceThunk, string key = null) {
            if (sourceThunk == null)
                throw new ArgumentNullException("sourceThunk");
            key = key ?? sourceThunk.Method.DeclaringType.FullName + "." + sourceThunk.Method.Name;
            var replaySubject1 = new ReplaySubject<T>();
            var replaySubject2 = (ReplaySubject<T>)currentObservables.GetOrAdd(key, replaySubject1);
            if (replaySubject2 != replaySubject1)
                return replaySubject2;
            object removed;
            Action finallyAction = () => currentObservables.TryRemove(key, out removed);
            IObservable<T> source;
            try {
                source = sourceThunk();
                if (source == null) {
                    finallyAction();
                    throw new InvalidOperationException("The observable returned by the lambda expression is null.");
                }
            }
            catch (Exception ex) {
                finallyAction();
                throw;
            }
            source.Do(_ => { }, ex => finallyAction(), finallyAction).Multicast(replaySubject1).Connect();
            return replaySubject1;
        }

        public static IObservable<Unit> AsCompletion<T>(this IObservable<T> observable) {
            return observable.Aggregate(Unit.Default, (unit, _) => unit);
        }

        public static IObservable<T> WhereNotNull<T>(this IObservable<T> observable) where T : class {
            return observable.Where(item => (object)item != null);
        }

        public static IObservable<Unit> SelectUnit<T>(this IObservable<T> observable) {
            return observable.Select(_ => Unit.Default);
        }

        public static IObservable<TRet> ContinueAfter<T, TRet>(this IObservable<T> observable, Func<IObservable<TRet>> selector) {
            return observable.AsCompletion().SelectMany(_ => selector());
        }

        public static IObservable<TRet> ContinueAfter<T, TRet>(this IObservable<T> observable, Func<IObservable<TRet>> selector, IScheduler scheduler) {
            return observable.AsCompletion().ObserveOn(scheduler).SelectMany(_ => selector());
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        public static IObservable<T> RetryWithBackoffStrategy<T>(this IObservable<T> source, int retryCount = 3, Func<int, TimeSpan> strategy = null, Func<Exception, bool> retryOnError = null,
            IScheduler scheduler = null) {
            strategy = strategy ?? ExpontentialBackoff;
            scheduler = scheduler ?? null;
            if (retryOnError == null)
                retryOnError = e => e.CanRetry();
            var attempt = 0;
            return Observable.Defer(
                () =>
                    (++attempt == 1 ? source : source.DelaySubscription(strategy(attempt - 1), scheduler)).Select(item => new Tuple<bool, T, Exception>(true, item, null))
                        .Catch((Func<Exception, IObservable<Tuple<bool, T, Exception>>>)(e => {
                            if (!retryOnError(e))
                                return Observable.Return(new Tuple<bool, T, Exception>(false, default(T), e));
                            return Observable.Throw<Tuple<bool, T, Exception>>(e);
                        }))).Retry(retryCount).SelectMany(t => {
                            if (!t.Item1)
                                return Observable.Throw<T>(t.Item3);
                            return Observable.Return(t.Item2);
                        });
        }

        //public static IObservable<T> Using<TResource, T>(Func<IObservable<TResource>> resourceFactory, Func<TResource, IObservable<T>> observableFactory) where TResource : IDisposable {
        //    return Observable.Create((Func<IObserver<T>, IDisposable>)(obs => {
        //        SingleAssignmentDisposable disp = new SingleAssignmentDisposable();
        //        Observable.Materialize(resourceFactory()).Take(1).Subscribe(notification => {
        //            if (notification.Kind == NotificationKind.OnCompleted)
        //                obs.OnCompleted();
        //            else if (notification.Kind == NotificationKind.OnError)
        //                obs.OnError(notification.Exception);
        //            else if (disp.IsDisposed) {
        //                if (notification.Value == null)
        //                    return;
        //                notification.Value.Dispose();
        //            }
        //            else
        //                disp.Disposable = Observable.Using(() => notification.Value, observableFactory).Subscribe(obs);
        //        });
        //        return (IDisposable)disp;
        //    }));
        //}

        public static IObservable<T> ErrorIfEmpty<T>(this IObservable<T> source, Exception exc) {
            return source.Materialize().Scan(Tuple.Create(false, (Notification<T>)null), (prev, cur) => Tuple.Create(prev.Item1 || cur.Kind == NotificationKind.OnNext, cur)).SelectMany(x => {
                if (x.Item1 || x.Item2.Kind != NotificationKind.OnCompleted)
                    return Observable.Return(x.Item2);
                return Observable.Throw<Notification<T>>(exc);
            }).Dematerialize();
        }

        public static IObservable<TRet> Select<T1, T2, TRet>(this IObservable<Tuple<T1, T2>> source, Func<T1, T2, TRet> lambda) {
            return source.Select(x => lambda(x.Item1, x.Item2));
        }

        public static IObservable<TRet> Select<T1, T2, T3, TRet>(this IObservable<Tuple<T1, T2, T3>> source, Func<T1, T2, T3, TRet> lambda) {
            return source.Select(x => lambda(x.Item1, x.Item2, x.Item3));
        }

        public static IObservable<TRet> Select<T1, T2, T3, T4, T5, T6, T7, TRet>(this IObservable<Tuple<T1, T2, T3, T4, T5, T6, T7>> source, Func<T1, T2, T3, T4, T5, T6, T7, TRet> lambda) {
            return source.Select(x => lambda(x.Item1, x.Item2, x.Item3, x.Item4, x.Item5, x.Item6, x.Item7));
        }

        public static IObservable<T> AddErrorData<T>(this IObservable<T> source, string key, object value) {
            return source.Do(_ => { }, ex => ex.AddData(key, value));
        }

        public static IObservable<T> AddErrorData<T>(this IObservable<T> source, object values) {
            return source.Do(_ => { }, ex => ex.AddData(values));
        }
    }

    public static class ExceptionExtensions {
        static readonly Lazy<Regex> stackFrameRegex = new Lazy<Regex>(() => new Regex("^\\s+\\S+\\s+\\S+\\(.*?\\)", RegexOptions.Multiline | RegexOptions.Compiled));

        public static Exception InnermostException(this Exception exception) {
            Exception innermostException = null;
            exception.VisitPostOrder(e => innermostException = innermostException ?? e);
            return innermostException;
        }

        //public static Exception UnwrapCompositionException(this Exception exception) {
        //    CompositionException compositionException1 = exception as CompositionException;
        //    if (compositionException1 == null)
        //        return exception;
        //    Exception exception1;
        //    for (CompositionException compositionException2 = compositionException1; compositionException2 != null; compositionException2 = exception1 as CompositionException) {
        //        CompositionError compositionError = ((IEnumerable<CompositionError>)compositionException2.Errors).FirstOrDefault();
        //        if (compositionError != null) {
        //            exception1 = compositionError.Exception;
        //            if (exception1 != null) {
        //                ComposablePartException composablePartException = exception1 as ComposablePartException;
        //                if (composablePartException != null && composablePartException.InnerException != null) {
        //                    CompositionException compositionException3 = composablePartException.InnerException as CompositionException;
        //                    if (compositionException3 == null)
        //                        return exception1.InnerException ?? exception;
        //                    exception1 = (Exception)compositionException3;
        //                }
        //            }
        //            else
        //                break;
        //        }
        //        else
        //            break;
        //    }
        //    return exception;
        //}

        public static void VisitPostOrder(this Exception exception, Action<Exception> visitor) {
            ReflectionTypeLoadException typeLoadException = exception as ReflectionTypeLoadException;
            if (typeLoadException != null)
                typeLoadException.LoaderExceptions.ForEach(e => VisitPostOrder(e, visitor));
            AggregateException aggregateException = exception as AggregateException;
            if (aggregateException != null)
                aggregateException.InnerExceptions.ForEach(e => VisitPostOrder(e, visitor));
            else if (exception.InnerException != null)
                VisitPostOrder(exception.InnerException, visitor);
            visitor(exception);
        }

        public static string FullStackTrace(this Exception exception) {
            StringBuilder builder = new StringBuilder();
            VisitPostOrder(exception, e => {
                if (builder.Length > 0)
                    builder.AppendLine("---------------");
                builder.AppendLine(e.VerboseMessage(true));
                builder.AppendLine(e.StackTrace);
            });
            return builder.ToString();
        }

        public static string VerboseMessage(this Exception exception, bool includeExceptionType = false) {
            string input = exception.ToString();
            if (!includeExceptionType) {
                string fullName = exception.GetType().FullName;
                if (input.StartsWith(fullName, StringComparison.Ordinal)) {
                    string str = input.Substring(fullName.Length);
                    if (str.StartsWith(": ", StringComparison.Ordinal))
                        str = str.Substring(2);
                    input = str.TrimStart();
                }
            }
            int length = input.IndexOf(" --->", StringComparison.Ordinal);
            if (length >= 0)
                return input.Substring(0, length);
            Match match = stackFrameRegex.Value.Match(input);
            if (match.Success)
                return input.Substring(0, match.Index);
            return input;
        }

        public static bool CanRetry(this Exception exception) {
            if (!exception.IsCriticalException())
                return !(exception is ObjectDisposedException);
            return false;
        }

        public static bool IsCriticalException(this Exception exception) {
            if (exception == null)
                throw new ArgumentNullException("exception");
            if (!exception.IsFatalException() && !(exception is AppDomainUnloadedException) && !(exception is BadImageFormatException) && !(exception is CannotUnloadAppDomainException) &&
                !(exception is InvalidProgramException) && !(exception is NullReferenceException))
                return exception is ArgumentException;
            return true;
        }

        public static bool IsFatalException(this Exception exception) {
            if (exception == null)
                throw new ArgumentNullException("exception");
            if (!(exception is StackOverflowException) && !(exception is OutOfMemoryException) && !(exception is ThreadAbortException))
                return exception is AccessViolationException;
            return true;
        }

        public static T AddData<T>(this T exception, string key, object value) where T : Exception {
            if (exception == null)
                throw new ArgumentNullException("exception");
            if (key == null)
                throw new ArgumentNullException("key");
            if (value == null || value.GetType().IsSerializable)
                exception.Data.Add(key, value);
            return exception;
        }

        public static T AddData<T>(this T exception, object values) where T : Exception {
            if (values != null) {
                foreach (PropertyDescriptor propertyDescriptor in TypeDescriptor.GetProperties(values))
                    exception.AddData(propertyDescriptor.Name, propertyDescriptor.GetValue(values));
            }
            return exception;
        }
    }
}