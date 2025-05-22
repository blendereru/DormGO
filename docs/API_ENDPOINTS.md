# DormGO API Endpoints

This document describes the **current API endpoints** for DormGO as of commit `35dbc34bb784b16be46589d419f22d6728d68193`.  
All endpoints require authentication (unless otherwise stated).  
All request and response bodies are JSON.  
All error responses use standard HTTP status codes and a JSON `ProblemDetails` object.

---

## Table of Contents

- [Posts API](#posts-api)
- [Authentication & Account API](#authentication--account-api)
- [Profile API](#profile-api)
- [Chat API](#chat-api)
- [Notifications API](#notifications-api)
- [Error Response Schema](#error-response-schema)
- [Notes](#notes)

---

## Posts API

### Create a Post

**POST** `/api/posts`

**Request:**
```json
{
  "title": "Room for 2",
  "description": "Near KBTU, furnished",
  "currentPrice": 1000,
  "latitude": 43.2,
  "longitude": 76.9,
  "maxPeople": 2
}
```

**Success Response:** `201 Created`
```json
{
  "postId": "abc123",
  "title": "Room for 2",
  "description": "Near KBTU, furnished",
  "currentPrice": 1000,
  "latitude": 43.2,
  "longitude": 76.9,
  "createdAt": "2025-05-22T09:45:00Z",
  "maxPeople": 2,
  "creator": {
    "email": "alice@kbtu.kz",
    "name": "Alice"
  },
  "members": []
}
```

**Error Response:**
```json
{
  "title": "Unauthorized",
  "detail": "User information is missing.",
  "status": 401,
  "instance": "POST /api/posts"
}
```

---

### Search Posts

**POST** `/api/posts/search`

**Request:**
```json
{
  "searchText": "room",
  "startDate": "2025-05-01T00:00:00Z",
  "endDate": "2025-05-31T23:59:59Z",
  "maxPeople": 2,
  "members": [],
  "onlyAvailable": true
}
```

**Success Response:** `200 OK`
```json
[
  {
    "postId": "abc123",
    "title": "Room for 2",
    "description": "Near KBTU, furnished",
    "currentPrice": 1000,
    "latitude": 43.2,
    "longitude": 76.9,
    "createdAt": "2025-05-22T09:45:00Z",
    "maxPeople": 2,
    "creator": {
      "email": "alice@kbtu.kz",
      "name": "Alice"
    },
    "members": []
  }
]
```

**Error Response:**
```json
{
  "title": "Unauthorized",
  "detail": "User information is missing.",
  "status": 401,
  "instance": "POST /api/posts/search"
}
```

---

### Get Posts

**GET** `/api/posts`  
Supports optional query: `?membership=joined|own|notjoined`

**Success Response:** `200 OK`
- If `membership` set:
```json
[
  {
    "postId": "abc123",
    ...
  }
]
```
- If no `membership` param:
```json
{
  "yourPosts": [ ... ],
  "joinedPosts": [ ... ],
  "notJoinedPosts": [ ... ]
}
```

**Error Response:**
```json
{
  "title": "Unauthorized",
  "detail": "User information is missing.",
  "status": 401,
  "instance": "GET /api/posts"
}
```

---

### Get a Single Post

**GET** `/api/posts/{id}`

**Success Response:** `200 OK`
```json
{
  "postId": "abc123",
  "title": "Room for 2",
  ...
}
```

**Error Response:**
```json
{
  "title": "Not Found",
  "detail": "The post with the specified ID was not found.",
  "status": 404,
  "instance": "GET /api/posts/{id}"
}
```

---

### Update a Post

**PUT** `/api/posts/{id}`

**Request:**
```json
{
  "title": "...",
  "description": "...",
  "currentPrice": 1000,
  "latitude": 43.2,
  "longitude": 76.9,
  "maxPeople": 2,
  "membersToRemove": [
    { "email": "bob@kbtu.kz" }
  ]
}
```

**Success Response:** `204 No Content`

**Error Response:**
```json
{
  "title": "Not Found",
  "detail": "The post with the specified ID was not found.",
  "status": 404,
  "instance": "PUT /api/posts/{id}"
}
```

---

### Delete a Post

**DELETE** `/api/posts/{id}`

**Success Response:** `204 No Content`

**Error Responses:**
- Not found:
```json
{
  "title": "Not Found",
  "detail": "The post with the specified ID was not found.",
  "status": 404,
  "instance": "DELETE /api/posts/{id}"
}
```
- Not owner:
```json
{
  "title": "Forbidden",
  "detail": "You are not authorized to delete this post.",
  "status": 403,
  "instance": "DELETE /api/posts/{id}"
}
```

---

### Join a Post

**POST** `/api/posts/{id}/membership`

**Success Response:** `204 No Content`

**Error Responses:**
- Not found / already joined / own post:
```json
{
  "title": "Not Found",
  "detail": "Post not found.",
  "status": 404,
  "instance": "POST /api/posts/{id}/membership"
}
```
- Post full:
```json
{
  "title": "Post Full",
  "detail": "The post has reached its maximum member capacity.",
  "status": 409,
  "instance": "POST /api/posts/{id}/membership"
}
```

---

### Leave a Post

**DELETE** `/api/posts/{id}/membership`

**Success Response:** `204 No Content`

**Error Response:**
```json
{
  "title": "Not Found",
  "detail": "The post with the specified ID was not found.",
  "status": 404,
  "instance": "DELETE /api/posts/{id}/membership"
}
```

---

### Transfer Post Ownership

**PUT** `/api/posts/{id}/ownership`

**Request:**
```json
{
  "email": "newowner@kbtu.kz"
}
```

**Success Response:** `204 No Content`

**Error Response:**
```json
{
  "title": "Not Found",
  "detail": "The post does not exist.",
  "status": 404,
  "instance": "PUT /api/posts/{id}/ownership"
}
```

---

## Authentication & Account API

### Sign Up

**POST** `/api/account/register`

**Request:**
```json
{
  "email": "example@kbtu.kz",
  "password": "your_password",
  "visitorId": "your_device_id"
}
```

**Success Response:** `204 No Content`

**Error Response:**
```json
{
  "title": "Conflict",
  "detail": "A user with this email already exists.",
  "status": 409,
  "instance": "POST /api/account/register"
}
```

---

### Sign In

**POST** `/api/account/login`

**Request:**
```json
{
  "email": "example@kbtu.kz",
  "password": "your_password",
  "visitorId": "your_device_id"
}
```

**Success Response:** `200 OK`
```json
{
  "accessToken": "jwt_token_here",
  "refreshToken": "refresh_token_here"
}
```

**Error Response:**
```json
{
  "title": "Unauthorized",
  "detail": "Invalid email or password.",
  "status": 401,
  "instance": "POST /api/account/login"
}
```

---

### Log Out

**DELETE** `/api/account/signout`

**Request:**
```json
{
  "refreshToken": "refresh_token_here",
  "visitorId": "your_device_id"
}
```

**Success Response:** `204 No Content`

---

### Refresh Tokens

**POST** `/api/account/refresh`

**Request:**
```json
{
  "refreshToken": "refresh_token_here",
  "visitorId": "your_device_id"
}
```

**Success Response:** `200 OK`
```json
{
  "accessToken": "new_jwt_token",
  "refreshToken": "new_refresh_token"
}
```

**Error Response:**
```json
{
  "title": "Unauthorized",
  "detail": "Invalid refresh token.",
  "status": 401,
  "instance": "POST /api/account/refresh"
}
```

---

### Email Confirmation

**GET** `/api/account/email/confirm?userId=...&token=...&visitorId=...`

**Success Response:** `200 OK`
```json
"good on you"
```

**Error Response:**
```json
{
  "title": "Invalid or Expired Link",
  "detail": "The email confirmation link is invalid, expired, or missing required parameters.",
  "status": 400,
  "instance": "GET /api/account/email/confirm"
}
```

---

### Resend Confirmation Email

**POST** `/api/account/email/confirmation/resend`

**Request:**
```json
{
  "email": "example@kbtu.kz",
  "visitorId": "your_device_id"
}
```

**Success Response:** `204 No Content`

---

### Forgot Password

**POST** `/api/account/password/forgot`

**Request:**
```json
{
  "email": "example@kbtu.kz",
  "visitorId": "your_device_id"
}
```

**Success Response:** `204 No Content`

---

### Validate Password Reset Link

**GET** `/api/account/password/reset/validate?userId=...&token=...`

**Success Response:** `204 No Content`

**Error Response:**
```json
{
  "title": "Invalid or Expired Link",
  "detail": "The password reset link is invalid, expired, or missing required parameters.",
  "status": 400,
  "instance": "GET /api/account/password/reset/validate"
}
```

---

### Reset Password

**POST** `/api/account/password/reset`

**Request:**
```json
{
  "email": "example@kbtu.kz",
  "token": "reset_token_here",
  "newPassword": "new_pass"
}
```

**Success Response:** `204 No Content`

---

### Change Email (via confirmation link)

**GET** `/api/account/email/change/confirm?userId=...&newEmail=...&token=...`

**Success Response:** `204 No Content`

**Error Response:**
```json
{
  "title": "Invalid or expired link",
  "status": 400,
  "detail": "The link is invalid, expired, or missing required parameters."
}
```

---

## Profile API

### Get Current User Profile

**GET** `/api/profile/me`

**Success Response:** `200 OK`
```json
{
  "email": "<your_email>",
  "name": "<your_name>"
}
```

---

### Update Current User Profile

**PATCH** `/api/profile/me`

**Request:**
```json
{
  "userName": "New Name",
  "newEmail": "new@example.com"
}
```

**Success Response:** `204 No Content`

---

## Chat API

### Get Messages for a Post

**GET** `/api/chat/{postId}/messages`

**Success Response:** `200 OK`
```json
{
  "messages": [
    {
      "messageId": "msg1",
      "content": "Hey, is this still available?",
      "sender": {
        "email": "bob@kbtu.kz",
        "name": "Bob"
      },
      "sentAt": "2025-05-22T09:55:00Z"
    }
  ]
}
```

**Error Response:**
```json
{
  "title": "Unauthorized",
  "detail": "User authentication required.",
  "status": 401,
  "instance": "GET /api/chat/{postId}/messages"
}
```

---

### Send a Message to a Post

**POST** `/api/chat/{postId}/messages`

**Request:**
```json
{
  "content": "Is there parking nearby?"
}
```

**Success Response:** `201 Created`
```json
{
  "message": {
    "messageId": "msg2",
    "content": "Is there parking nearby?",
    "sender": {
      "email": "alice@kbtu.kz",
      "name": "Alice"
    },
    "sentAt": "2025-05-22T10:01:00Z"
  }
}
```

**Error Response:**
```json
{
  "title": "Unauthorized",
  "detail": "User authentication required.",
  "status": 401,
  "instance": "POST /api/chat/{postId}/messages"
}
```

---

### Update a Message in a Post

**PUT** `/api/chat/posts/{postId}/messages/{messageId}`

**Request:**
```json
{
  "content": "Updated message content"
}
```

**Success Response:** `200 OK`
```json
{
  "message": "The message was successfully updated."
}
```

**Error Response:**
```json
{
  "title": "Not Found",
  "detail": "The message with the specified ID was not found.",
  "status": 404,
  "instance": "PUT /api/chat/posts/{postId}/messages/{messageId}"
}
```

---

### Delete a Message in a Post

**DELETE** `/api/chat/posts/{postId}/messages/{messageId}`

**Success Response:** `200 OK`
```json
{
  "message": "The message was successfully removed."
}
```

**Error Response:**
```json
{
  "title": "Not Found",
  "detail": "The message with the specified ID was not found.",
  "status": 404,
  "instance": "DELETE /api/chat/posts/{postId}/messages/{messageId}"
}
```

---

## Notifications API

### Get All Notifications

**GET** `/api/notifications/`

**Success Response:** `200 OK`
```json
[
  {
    "notificationId": "notification_id",
    "user": {
      "email": "user_email@example.com",
      "name": "User Name"
    },
    "message": "Your message",
    "isRead": false,
    "createdAt": "2025-02-05T21:38:47Z",
    "post": {
      "postId": "post_id",
      "title": "<your_post_title>",
      "description": "Post description",
      "currentPrice": 1500,
      "latitude": 12.345,
      "longitude": 23.678,
      "createdAt": "2025-02-05T21:38:47Z",
      "maxPeople": 2,
      "creator": {
        "email": "creator_email@example.com",
        "name": "Creator Name"
      },
      "members": []
    }
  }
]
```

---

### Update a Notification (mark as read/unread)

**PATCH** `/api/notifications/{id}`

**Request:**
```json
{
  "isRead": true
}
```

**Success Response:** `204 No Content`

---

### Delete Notification

**DELETE** `/api/notifications/{id}`

**Success Response:** `200 OK`
```json
{
  "message": "The notification was successfully deleted."
}
```

---

## Error Response Schema

All error responses use this format:
```json
{
  "title": "...",
  "detail": "...",
  "status": ...,
  "instance": "METHOD /api/..."
}
```

---

## Notes

- Only documented endpoints are supported as of the current commit.
- All endpoints require authentication unless stated otherwise.
- All request/response bodies are JSON.
- Operations that fail due to access rights return `401` or `403` as appropriate.
- For other APIs (notifications, profiles, etc.), see their respective documentation.