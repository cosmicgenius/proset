using System.Collections.Concurrent;

namespace proset.Models;

public interface IEventSubscriber {
    public string id { get; }
    public bool alive { get; }
    public void CheckAlive();
    public void Dispose();
    public Task Emit(string data);
    public Task Flush();
}

public interface IEventEmitter {
    public void Subscribe(string id, IEventSubscriber subscriber);

    // Only for manually unsubscribing, 
    // if connection is lost, the emitter should automatically unsubscribe
    public void Unsubscribe(string id, string subscriber_id);
    public ICollection<string>? GetSubscribers(string id);
    public Task Emit(string id, string data);
    public Task Flush(string id);
}

public class GameEventSubscriber : IEventSubscriber {
    private HttpResponse _response;
    public string id { get; }
    public bool alive { get; private set; }

    public GameEventSubscriber(HttpResponse response, string id) {
        _response = response;
        this.id = id;
        this.alive = true;
    }

    public void CheckAlive() {
        if (_response.HttpContext.RequestAborted.IsCancellationRequested == true) {
            Dispose();
        }
    }

    public void Dispose() {
        this.alive = false;
    }

    public async Task Emit(string data) {
        await _response.WriteAsync($"data: {data}\r\r");
    }

    public async Task Flush() {
        await _response.Body.FlushAsync();
        CheckAlive();
    }
}

public class GameEventEmitter : IEventEmitter {
    private ConcurrentDictionary<string, ConcurrentDictionary<string, IEventSubscriber>> _subscribers;

    public GameEventEmitter() {
        _subscribers = new ConcurrentDictionary<string, ConcurrentDictionary<string, IEventSubscriber>>();
    }

    public void Subscribe(string id, IEventSubscriber subscriber) {
        _subscribers.TryAdd(id, new ConcurrentDictionary<string, IEventSubscriber>());
        _subscribers[id].AddOrUpdate(subscriber.id, subscriber, (_, old) => {
            old.Dispose();
            return subscriber;});
    }

    public void Unsubscribe(string id, string subscriber_id) {
        if (_subscribers.TryGetValue(id, 
                out ConcurrentDictionary<string, IEventSubscriber>? subscribers)
            && subscribers is not null) {
            subscribers.TryRemove(subscriber_id, out _);
        }
    }

    public ICollection<string>? GetSubscribers(string id) {
        if (_subscribers.TryGetValue(id, 
                out ConcurrentDictionary<string, IEventSubscriber>? subscribers)
            && subscribers is not null) {
            return subscribers.Where(item => item.Value.alive).Select(item => item.Key).ToHashSet();
        }
        return null;
    }

    public async Task Emit(string id, string data) {
        if (_subscribers.TryGetValue(id, 
                out ConcurrentDictionary<string, IEventSubscriber>? subscribers)
            && subscribers is not null) {
            List<Task> tasks = subscribers.Select(s => s.Value.Emit(data)).ToList();
            await Task.WhenAll(tasks);
        }
    }

    public async Task Flush(string id) {
        if (_subscribers.TryGetValue(id, 
                out ConcurrentDictionary<string, IEventSubscriber>? subscribers)
            && subscribers is not null) {
            List<Task> tasks = subscribers.Select(s => s.Value.Flush()).ToList();
            await Task.WhenAll(tasks);

            // Remove all dead subscribers
            foreach (var item in subscribers.Where(item => !item.Value.alive).ToList()) {
                subscribers.TryRemove(item.Key, out _);
            }
        }
    }
}
