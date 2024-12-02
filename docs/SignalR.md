## What is SignalR ?
`SignalR` is a library that allows to server to send updates right after it has one. It is a perfect library to use
in apps where `real-time` updates is required(just like in chat apps). The connection between the client and server
is persistent, unlike a classic `HTTP` connection, which is re-established for each communication.
### WebSockets
`SignalR` uses `WebSocket` transport by default(is possible). WebSocket is a `protocol`, where client's and server's 
connection remains persistent until it gets terminated by either party(client disconnects). `WebSocket` is the optimal
transport because it has the most efficient use of server memory.
### Other transports
There are 3 types of transports that `SignalR` can support if the client is unable to support `WebSocket` transport:
* `Forever Frame` - uses hidden `<iframe>` in the browser. The request sent to server doesn't immediately complete.
Server sends script to the client which is immediately executed and eventual updates from the server are written
as `<script>` tags to the `<iframe>`, which the browser executes to deliver messages
* `Server Sent Events(SSE)` - establishes a one-way communication channel from the server to the client. 
Server sends updates to the client over an `HTTP` connection using the `text/event-stream` MIME type. This connection
remain open for streaming messages until explicitly closed by either side.
* `Long polling` - no persistent connection is applied. Client periodically polls server for updates, until it sends
him one.
### About hubs
`Hub` is the essential component in connection setting. Once an endpoint is set upon hub, both client and server can
can send and receive the methods defined. `Hubs` abstract the complexity of writing different implementations for each
transport type.
### Difference between `Client()` and `User()` in `HubContext`
`Hubs` provide built-in methods to manage client connections. `ConnectionIDs` uniquely identify each connected client.
`Groups` allow organizing clients into logical groups (e.g., rooms in a chat app) for targeted message delivery.
In each method defined in the `Hub` class, we can set who do we want to send the message to:
```csharp
public async Task NotifyPostCreated(string userName, PostDto post)
{
    await Clients.All.SendAsync("PostCreated", userName, post);
}
```
In this case, we say that all connected clients receive `PostCreated` method when it gets invoked. Other properties
of `Clients` property include more specific audience of message:
```csharp
public async Task NotifyPostCreated(string userName, PostDto post)
{
    await Clients.User("your_userId").SendAsync("PostCreated", userName, post);
    //or
    await Clients.Client("your_connectionId").SendAsync("PostCreated", userName, post);
}
```
`Client()` method requires connectionId as a parameter. That means the method `PostCreated` will be invoked for a specific
connected client. Connected client is everyone who is connected to our `Hub`, that is every single connection gets
its own assigned `connectionId`. `User()`, in comparison, required `userId`, which is not unique for each connected client.
If both connected clients (the same user, but connected through laptop and phone) have the same name claim,
they will get the same userId. By default, `SignalR` searches for the first value of `NameIdentifier` from the 
user's claims:
```csharp
Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
```


