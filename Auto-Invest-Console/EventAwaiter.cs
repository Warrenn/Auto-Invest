using System;
using System.Threading.Tasks;

namespace Auto_Invest;

public static class EventAwaiter
{
    public class EventArg<T>
    {
        public T Args { get; set; }
        public object Sender { get; set; }
    }

    public static Task<EventArg<T>> AwaitEvent<T>(Action<EventHandler<T>> addEvent, Action<EventHandler<T>> removeEvent, Action initiate = null)
    {
        var source = new TaskCompletionSource<EventArg<T>>();
        addEvent(Handler);
        initiate?.Invoke();
        return source.Task;

        void Handler(object sender, T e)
        {
            removeEvent(Handler);
            source.SetResult(new EventArg<T> { Args = e, Sender = sender });
        }
    }

    public static Task<EventArg<EventArgs>> AwaitEvent(Action<EventHandler> addEvent, Action<EventHandler> removeEvent, Action initiate = null)
    {
        var source = new TaskCompletionSource<EventArg<EventArgs>>();
        addEvent(Handler);
        initiate?.Invoke();
        return source.Task;

        void Handler(object sender, EventArgs e)
        {
            removeEvent(Handler);
            source.SetResult(new EventArg<EventArgs> { Args = e, Sender = sender });
        }
    }

    public static Task<T> AwaitEvent<T>(Action<Action<T>> addEvent, Action<Action<T>> removeEvent, Action initiate = null)
    {
        var source = new TaskCompletionSource<T>();
        addEvent(Handler);
        initiate?.Invoke();
        return source.Task;

        void Handler(T e)
        {
            removeEvent(Handler);
            source.SetResult(e);
        }
    }
    public static Task AwaitEvent(Action<Action> addEvent, Action<Action> removeEvent, Action initiate = null)
    {
        var source = new TaskCompletionSource();
        addEvent(Handler);
        initiate?.Invoke();
        return source.Task;

        void Handler()
        {
            removeEvent(Handler);
            source.SetResult();
        }
    }
}