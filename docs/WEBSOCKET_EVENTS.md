# DormGO WebSocket (SignalR) Notification Events

This document lists **all discovered SignalR (WebSocket) events** sent from the backend to clients, based on code in:
- `Services/HubNotifications/`
- `Services/NotificationService.cs`
- `Controllers/PostController.cs`

> **Note:** Results may be incomplete. See the [GitHub code search results for all `SendAsync` usages](https://github.com/blendereru/DormGO/search?q=SendAsync) for more.

---

## UserHub Events (`/api/userhub`)

| Event Name                | Payload Example/Description                                            | When Triggered                                  |
|---------------------------|-----------------------------------------------------------------------|-------------------------------------------------|
| `EmailChanged`            | `{ "Email": "user@example.com" }`                                     | When user changes their email                   |
| `PasswordResetLinkValidated` | `{ "Email": "user@example.com" }`                                 | When user validates a password reset link       |
| `EmailConfirmed`          | `{ "Email": "...", "AccessToken": "...", "RefreshToken": "..." }`     | When user confirms email (after registration)   |

---

## PostHub Events (`/api/posthub`)

| Event Name        | Payload Example/Description                                                      | When Triggered                        |
|-------------------|---------------------------------------------------------------------------------|---------------------------------------|
| `PostCreated`     | `{ Id, Title, CreatedAt, CreatorName, MaxPeople }`                               | When a post is created                |
| `PostUpdated`     | `{ Id, Title, UpdatedAt, CreatorName, MaxPeople }`                               | When a post is updated                |
| `PostDeleted`     | `{ Id }`                                                                         | When a post is deleted                |
| `PostJoined`      | `{ UserId, UserName, postId }`                                                   | When a user joins a post              |
| `PostLeft`        | `{ UserId, UserName, postId }`                                                   | When a user leaves a post             |

---

## ChatHub Events (`/api/chathub`)

| Event Name      | Payload Example/Description                                                                                   | When Triggered                  |
|-----------------|--------------------------------------------------------------------------------------------------------------|---------------------------------|
| `MessageSent`   | `{ Id, Content, SenderName, SentAt, UpdatedAt }`                                                             | When a new chat message is sent |
| `MessageUpdated`| `{ Id, Content, SenderName, SentAt, UpdatedAt }`                                                             | When a message is edited        |
| `MessageDeleted`| `{ Id, SenderName }`                                                                                         | When a message is deleted       |

> These events are typically sent to the SignalR group for the relevant post, excluding the sender's own connections.

---

## NotificationHub Events (`/api/notificationhub`)

| Event Name            | Payload Example/Description                                | When Triggered              |
|-----------------------|-----------------------------------------------------------|-----------------------------|
| (Dynamic, via `SendNotificationAsync`) | Payload is the NotificationResponseDto for the notification; event name is dynamic | When a notification is created for a user. For example:<br> - `"OwnershipTransferred"` when post ownership changes<br> - `"PostLeft"` when a user is removed from a post |

---

## Controller-Triggered WebSocket Events: Full Response Payloads

This document details the full structure of SignalR event payloads sent by the `PostController` for post ownership transfer and member removal actions, based on your provided DTOs.

---

### 1. OwnershipTransferred

**Event Name:** `"OwnershipTransferred"`

**Triggered When:**  
A user is made the new owner of a post (ownership transferred).

**Payload Example:**
```json
{
  "notificationId": "notif_123",
  "user": {
    "email": "bob@kbtu.kz",
    "name": "Bob"
  },
  "title": "Post ownership",
  "description": "You are now the owner of the post: Dorm Room in Center",
  "isRead": false,
  "createdAt": "2025-05-22T09:45:00Z",
  "type": "Post",
  "post": {
    "id": "post_456",
    "title": "Dorm Room in Center",
    "description": "Spacious room near campus.",
    "currentPrice": 1200,
    "latitude": 43.25,
    "longitude": 76.92,
    "createdAt": "2025-05-22T09:45:00Z",
    "maxPeople": 2,
    "creator": {
      "id": "user_002",
      "email": "alice@kbtu.kz",
      "name": "Alice"
    },
    "members": [
      { "email": "bob@kbtu.kz", "name": "Bob" }
    ]
  }
}
```

---

### 2. PostLeft

**Event Name:** `"PostLeft"`

**Triggered When:**  
A user is removed from a post (either by the owner or as a result of an update).

**Payload Example:**
```json
{
  "notificationId": "notif_789",
  "user": {
    "email": "charlie@kbtu.kz",
    "name": "Charlie"
  },
  "title": "Post leave",
  "description": "You have been removed from the post: Dorm Room in Center",
  "isRead": false,
  "createdAt": "2025-05-22T09:50:00Z",
  "type": "Post",
  "post": {
    "id": "post_456",
    "title": "Dorm Room in Center",
    "description": "Spacious room near campus.",
    "currentPrice": 1200,
    "latitude": 43.25,
    "longitude": 76.92,
    "createdAt": "2025-05-22T09:45:00Z",
    "maxPeople": 2,
    "creator": {
      "id": "user_002",
      "email": "alice@kbtu.kz",
      "name": "Alice"
    },
    "members": [
      { "email": "bob@kbtu.kz", "name": "Bob" }
    ]
  }
}
```

---

### Field Descriptions

- `notificationId`: Unique identifier for the notification.
- `user`: The recipient user (see `UserResponseDto`).
- `title`: Notification title.
- `description`: Notification message, includes post title.
- `isRead`: Whether the notification has been read.
- `createdAt`: When the notification was created (ISO string).
- `type`: Always `"Post"` for these events.
- `post`: The related post (see `PostResponseDto` for full structure).

---

## How the Events Are Sent

- All events use `SendAsync(eventName, payload)` on the appropriate SignalR hub/context.
- Most events are sent to individual users (`Clients.User(user.Id)`), groups, or all except the sender’s connections.

---

## See Also

- [All SendAsync usages in the codebase (GitHub search)](https://github.com/blendereru/DormGO/search?q=SendAsync)
- [UserHubNotificationService.cs](https://github.com/blendereru/DormGO/blob/main/src/server/DormGO/Services/HubNotifications/UserHubNotificationService.cs)
- [ChatHubNotificationService.cs](https://github.com/blendereru/DormGO/blob/main/src/server/DormGO/Services/HubNotifications/ChatHubNotificationService.cs)
- [PostHubNotificationService.cs](https://github.com/blendereru/DormGO/blob/main/src/server/DormGO/Services/HubNotifications/PostHubNotificationService.cs)
- [NotificationService.cs](https://github.com/blendereru/DormGO/blob/main/src/server/DormGO/Services/NotificationService.cs)
- [PostController.cs](https://github.com/blendereru/DormGO/blob/main/src/server/DormGO/Controllers/PostController.cs)

---

**Note:** For a full list of dynamic notification event names, review all usages of `SendNotificationAsync` and event name arguments in your project.