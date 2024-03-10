using System.Collections.Concurrent;

namespace proset.Models;

public interface IEventSubscriber {
    public string id { get; }
    public bool alive { get; }
    public Task Emit(string data);
}

public interface IEventEmitter {
    public void Subscribe(string id, IEventSubscriber subscriber);
    public void Unsubscribe(string id, string subscriber_id);
    public Task Emit(string id, string data);
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

    public async Task Emit(string data) {
        await _response.WriteAsync($"data: {data}\r\r");
        await _response.Body.FlushAsync();

        if (_response.HttpContext.RequestAborted.IsCancellationRequested == true) {
            alive = false;
        }
    }
}

public class GameEventEmitter : IEventEmitter {
    private ConcurrentDictionary<string, ConcurrentDictionary<string, IEventSubscriber>> _subscribers;

    public GameEventEmitter() {
        _subscribers = new ConcurrentDictionary<string, ConcurrentDictionary<string, IEventSubscriber>>();
    }

    public void Subscribe(string id, IEventSubscriber subscriber) {
        _subscribers.TryAdd(id, new ConcurrentDictionary<string, IEventSubscriber>());
        _subscribers[id].TryAdd(subscriber.id, subscriber);
    }

    public void Unsubscribe(string id, string subscriber_id) {
        if (_subscribers.TryGetValue(id, 
                out ConcurrentDictionary<string, IEventSubscriber>? subscribers)) {
            if (subscribers is not null) {
                subscribers.TryRemove(subscriber_id, out _);
            }
        }
    }

    public async Task Emit(string id, string data) {
        if (_subscribers.TryGetValue(id, 
                out ConcurrentDictionary<string, IEventSubscriber>? subscribers)) {
            if (subscribers is not null) {
                List<Task> tasks = subscribers.Select(s => s.Value.Emit(data)).ToList();
                await Task.WhenAll(tasks);
            }
        }
    }
}
