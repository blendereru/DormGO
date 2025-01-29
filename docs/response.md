## API
The API contains a number of endpoints to which a client can send requests to. All the endpoints are written
using HttpGet and HttpPost http methods, but this behaviour can be rewritten using REST APIs(e.g. adding
different http methods).

##  Endpoints
<table border="1">
  <thead>
    <tr>
      <th>HTTP Method</th>
      <th>Endpoint</th>
      <th>Definition</th>
      <th>Request Example</th>
      <th>Response Example</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td>POST</td>
      <td>/api/signup</td>
      <td>Register a new user in the system.</td>
      <td>
        <pre lang="json">{
  "email": "example@kbtu.kz",
  "password": "&lt;your_password&gt;",
  "visitorId": "&lt;your_device_id&gt;"
}</pre>
      </td>
      <td>
        <pre lang="json">{
  "message": "User registered successfully. Email confirmation is pending."
}</pre>
      </td>
    </tr>
    <tr>
      <td>POST</td>
      <td>/api/signin</td>
      <td>Log an existing user into the system. Requires email confirmation before login.</td>
      <td>
        <pre lang="json">{
  "email": "example@kbtu.kz",
  "password": "&lt;your_password&gt;",
  "visitorId": "&lt;your_device_id&gt;"
}</pre>
      </td>
      <td>
        <pre lang="json">{
  "message": "Login successful",
  "access_token": "&lt;your_access_token&gt;",
  "refresh_token": "&lt;your_refresh_token&gt;"
}</pre>
      </td>
    </tr>
        <tr>
      <td>DELETE</td>
      <td>/api/signout</td>
      <td>Log out a user and remove their refresh token.</td>
      <td>
        <pre lang="json">{
  "refreshToken": "&lt;refresh_token&gt;",
  "accessToken": "&lt;access_token&gt;"
}</pre>
      </td>
      <td>
        <pre lang="json">"The refresh token was successfully removed."</pre>
      </td>
    </tr>
    <tr>
      <td>PUT</td>
      <td>/api/refresh-tokens</td>
      <td>Refresh the access and refresh tokens.</td>
      <td>
        <pre lang="json">{
  "accessToken": "&lt;your_access_token&gt;",
  "refreshToken": "&lt;your_refresh_token&gt;",
  "visitorId": "&lt;your_device_id&gt;"
}</pre>
      </td>
      <td>
        <pre lang="json">{
  "access_token": "&lt;your_new_access_token&gt;",
  "refresh_token": "&lt;your_new_refresh_token&gt;"
}</pre>
      </td>
    </tr>
    <tr>
      <td>POST</td>
      <td>/api/forgot-password</td>
      <td>Send a password recovery request when a user forgets their password.</td>
      <td>
        <pre lang="json">{
  "email": "example@kbtu.kz",
  "visitorId": "&lt;your_device_id&gt;"
}</pre>
      </td>
      <td>
        <pre lang="json">"Forgot password email sent successfully."</pre>
      </td>
    </tr>
    <tr>
      <td>GET</td>
      <td>/api/reset-password/{userId}/{token}</td>
      <td>Validate the token and allow the client to reset the password.</td>
      <td>Requires `userId` and `token` in the query parameters.</td>
      <td>
        <pre lang="json">{
  "message": "The link is valid. You can now reset your password.",
  "email": "example@kbtu.kz",
  "token": "&lt;your_token&gt;"
}</pre>
      </td>
    </tr>
    <tr>
      <td>POST</td>
      <td>/api/reset-password</td>
      <td>Save the new password in the system.</td>
      <td>
        <pre lang="json">{
  "email": "example@kbtu.kz",
  "token": "&lt;your_token&gt;",
  "new_password": "&lt;your_new_password&gt;"
}</pre>
      </td> 
      <td>
        <pre lang="json">{
  "message": "Your password has been reset successfully"
}</pre>
      </td>
    </tr>
    <tr>
      <td>POST</td>
      <td>/api/resend-confirmation-email</td>
      <td>Resend the confirmation email to the user. Requires `email` and `visitorId` in body.</td>
      <td>
        <pre lang="json">{
  "email": "example@kbtu.kz",
  "visitorId": "&lt;your_device_id&gt;"
}</pre>
      </td>
      <td>
        <pre lang="json">{
  "message": "Confirmation email sent successfully."
}</pre>
      </td>
    </tr>
    <tr>
      <td>GET</td>
      <td>/api/profile/me</td>
      <td>Retrieve profile details of the current user</td>
      <td>No request body required.</td>
      <td>
        <pre lang="json">{
  "email": "&lt;your_gmail_here&gt;",
  "name": "&lt;your_name&gt;",
  "registeredAt": "&lt;your_registration_date&gt;"
}</pre>
      </td>
    </tr>
<tr>
      <td>GET</td>
      <td>/api/profile/{email}</td>
      <td>Retrieve profile details of the specified user</td>
      <td>Provide email in the request query.</td>
      <td>
        <pre lang="json">{
  "email": "&lt;users_gmail&gt;",
  "name": "&lt;user_name&gt;",
  "registeredAt": "&lt;user_registration_date&gt;"
}</pre>
      </td>
    </tr>
    <tr>
      <td>POST</td>
      <td>/api/post/create</td>
      <td>Create a new post and make it visible to all users.</td>
      <td>
        <pre lang="json">{
  "description": "&lt;your_post_description&gt;",
  "currentPrice": 0,
  "latitude": 0,
  "longitude": 0,
  "createdAt": "&lt;date_of_creation&gt;",
  "maxPeople": 0
}</pre>
      </td>
      <td>
        <pre lang="json">{
  "message": "The post was saved to the database"
}</pre>
      </td>
    </tr>
    <tr>
      <td>PUT</td>
      <td>/api/post/update/{id}</td>
      <td>Update the information of a specific post.</td>
      <td>
        <pre lang="json">{
  "description": "&lt;your_post_description&gt;",
  "currentPrice": 0,
  "latitude": 0,
  "maxPeople": 0,
  "longitude": 0,
  "memberToRemove": [
    {
        "email": "example@kbtu.kz",
        "name": "&lt;your_name&gt;"
    }
  ],
}</pre>
      </td>
      <td>
        <pre lang="json">{
  "message": "The post was successfully updated.",
  "post": {
    "postId": "&lt;post_id&gt;",
    "description": "&lt;your_post_description&gt;",
    "currentPrice": 0,
    "latitude": 0,
    "longitude": 0,
    "createdAt": "&lt;date_of_creation&gt;",
    "maxPeople": 0,
    "creator": {
      "email": "example@kbtu.kz",
      "name": "&lt;your_name&gt;"
    },
    "members": []
  }
}</pre>
      </td>
    </tr>
<tr>
      <td>PUT</td>
      <td>/api/post/{id}/transfer-ownership</td>
      <td>Change the creator(owner) of the post</td>
      <td>
        <pre lang="json">{
  "email": "example@kbtu.kz",
  "name": "&lt;creator_name&gt;"
}</pre>
      </td>
      <td><pre lang="json">
"The user was successfully added to the members of the post"</pre></td>
    </tr>
    <tr>
      <td>GET</td>
      <td>/api/post/read/{id}</td>
      <td>Retrieve information about a specific post.</td>
      <td>Requires `id` of the post in the query.</td>
      <td>
        <pre lang="json">{
  "postId": "&lt;post_id&gt;",
  "description": "&lt;your_post_description&gt;",
  "currentPrice": 0,
  "latitude": 0,
  "longitude": 0,
  "createdAt": "&lt;date_of_creation&gt;",
  "maxPeople": 0,
  "creator": {
    "email": "example@kbtu.kz",
    "name": "&lt;your_name&gt;"
  },
  "members": []
}</pre>
      </td>
    </tr>
    <tr>
      <td>PUT</td>
      <td>/api/post/join/{id}</td>
      <td>Add the current user as a member of the post.</td>
      <td>Requires `id` of the post in the query.</td>
      <td>
        <pre lang="json">"The user was successfully added to the members of the post"</pre>
      </td>
    </tr>
    <tr>
      <td>DELETE</td>
      <td>/api/post/unjoin/{id}</td>
      <td>Remove the current user from the post's members.</td>
      <td>Requires `id` of the post in the query.</td>
      <td>
        <pre lang="json">{
  "message": "The user was successfully removed from the post's members."
}</pre>
      </td>
    </tr>
    <tr>
      <td>DELETE</td>
      <td>/api/post/delete/{id}</td>
      <td>Delete the specific post.</td>
      <td>Requires `id` of the post in the query.</td>
      <td>
        <pre lang="json">{
  "message": "The post was successfully removed."
}</pre>
      </td>
    </tr>
    <tr>
      <td>GET</td>
      <td>/api/post/read?joined=false</td>
      <td>Retrieve the posts that user created and the new ones.</td>
      <td>No request body required.</td>
      <td>
        <pre lang="json">{
  "yourPosts": [
    {
      "postId": "&lt;your_post_id&gt;",
      "description": "&lt;your_post_description&gt;",
      "currentPrice": 0,
      "latitude": 0,
      "longitude": 0,
      "createdAt": "&lt;date_of_post_creation&gt;",
      "maxPeople": 0,
      "creator": {
        "email": "yourgmail@kbtu.kz",
        "name": "&lt;your_name&gt;"
      },
      "members": []
    }
  ],
  "restPosts": [
    {
      "postId": "&lt;other_post_id&gt;",
      "description": "&lt;other_post_description&gt;",
      "currentPrice": 0,
      "latitude": 0,
      "longitude": 0,
      "createdAt": "&lt;other-post_creation_date&gt;",
      "maxPeople": 0,
      "creator": {
        "email": "example@kbtu.kz",
        "name": "&lt;other_creator_name&gt;"
      },
      "members": []
    }
  ]
}</pre>
      </td>
    </tr>
    <tr>
      <td>GET</td>
      <td>/api/post/read?joined=true</td>
      <td>Retrieve posts where the user is a member.</td>
      <td>No request body required.</td>
      <td>
        <pre lang="json">{
  "postsWhereMember": [
    {
      "postId": "&lt;other_post_id&gt;",
      "description": "&lt;other_post_description&gt;",
      "currentPrice": 0,
      "latitude": 0,
      "longitude": 0,
      "createdAt": "&lt;other-post_creation_date&gt;",
      "maxPeople": 0,
      "creator": {
        "email": "example@kbtu.kz",
        "name": "&lt;creator_name&gt;"
      },
      "members": [
        {
          "email": "yourgmail@kbtu.kz",
          "name": "&lt;your_name&gt;"
        }
      ]
    }
  ]
}</pre>
      </td>
    </tr>
<tr>
      <td>GET</td>
      <td>/api/chat/{postId}/messages</td>
      <td>Retrieve all messages for a specific post.</td>
      <td>No request body is needed.</td>
      <td>
        <pre lang="json">{
  "messages": [
    {
      "messageId": "&lt;_message_id&gt;",
      "content": "&lt;your_post_description&gt;",
      "sender": {
        "email": "sender@kbtu.kz",
        "name": "&lt;sender_name&gt;"
      },
      "sentAt": "&lt;date_of_creation&gt;"
    }
  ]
}</pre>
      </td>
    </tr>
<tr>
      <td>POST</td>
      <td>/api/chat/{postId}/messages</td>
      <td>Add the message to the post.</td>
      <td><pre lang="json">{
    "content": "&lt;your_post_description&gt;",
}</pre></td>
      <td>
        <pre lang="json">{
  "message": {
      "messageId": "&lt;message_id&gt;",
      "content": "&lt;your_post_description&gt;",
      "sender": {
        "email": "sender@kbtu.kz",
        "name": "&lt;sender_name&gt;"
      },
      "sentAt": "&lt;date_of_creation&gt;"
  }
}</pre>
      </td>
    </tr>
    <tr>
      <td>DELETE</td>
      <td>/api/chat/messages/{messageId}</td>
      <td>Delete the message from the post.</td>
      <td>No request body required,</td>
      <td>
        <pre lang="json">"The message was successfully removed"</pre>
      </td>
    </tr>
  </tbody>
</table>

## WebSocket's events
Server initiates `WebSocket` connection with client for specific events to trigger an immediate update
on view.

<table border="1">
  <thead>
    <tr>
      <th>HTTP Endpoint</th>
      <th>Hub Method</th>
      <th>Response Type</th>
      <th>Description</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td>/api/userhub?userName="your_userName"</td>
      <td>EmailConfirmed</td>
      <td>
        <pre lang="json">{
  "target": "EmailConfirmed",
  "arguments": [
    "your_gmail@kbtu.kz",
    {
      "accessToken": "your_access_token",
      "refreshToken": "your_refresh_token"
    }
  ]
}</pre>
      </td>
      <td>Gets triggered when a user confirms their email.</td>
    </tr>
    <tr>
      <td>/api/posthub</td>
      <td>PostCreated</td>
      <td>
        <pre lang="json">{
  "target": "PostCreated",
  "arguments": [
    true,
    {
      "postId": "post_id",
      "description": "your_description",
      "currentPrice": 1500,
      "latitude": 12.345,
      "longitude": 23.678,
      "createdAt": "date_of_creation",
      "maxPeople": 6,
      "creator": {
        "email": "creator_email_address",
        "name": "creator_name"
      },
      "members": []
    }
  ]
}</pre>
      </td>
      <td>Gets triggered when a user creates a post. The first argument indicates if the post creator is the user himself.</td>
    </tr>
    <tr>
      <td>/api/posthub</td>
      <td>PostUpdated</td>
      <td>
        <pre lang="json">{
  "target": "PostUpdated",
  "arguments": [
    {
      "postId": "post_id",
      "description": "your_description",
      "currentPrice": 1500,
      "latitude": 12.345,
      "longitude": 23.678,
      "createdAt": "post_date_of_creation",
      "maxPeople": 2,
      "creator": {
        "email": "creatorgmail@kbtu.kz",
        "name": "creator_name"
      },
      "members": []
    }
  ]
}</pre>
      </td>
      <td>Gets triggered when a user updates their post settings.</td>
    </tr>
    <tr>
      <td>/api/posthub</td>
      <td>PostDeleted</td>
      <td>
        <pre lang="json">{
  "target": "PostDeleted",
  "arguments": [
    "post_id"
  ]
}</pre>
      </td>
      <td>Gets triggered when a user removes a post. The argument indicates the ID of the post that was removed.</td>
    </tr>
    <tr>
  <td>/api/chathub</td>
  <td>ReceiveMessage</td>
  <td>
    <pre lang="json">{
  "target": "ReceiveMessage",
  "arguments": [
    "post_id",
    {
      "content": "Message content from sender",
      "sender": {
        "userId": "sender_user_id",
        "userName": "sender_user_name"
      }
    }
  ]
}</pre>
  </td>
  <td>Gets triggered when a user sends a message to the members of a post. The message contains the post ID and the content along with the sender details.</td>
</tr>
  </tbody>
</table>






 
