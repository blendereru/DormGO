## API
The API contains a number of endpoints to which a client can send requests to. All the endpoints are written
using HttpGet and HttpPost http methods, but this behaviour can be rewritten using REST APIs(e.g. adding
different http methods).

##  Endpoints
| HTTP Method | Endpoint                          | Definition                                                                             | Request Example                                                                                                                                              | Response Example                                                                                                                                              |
|-------------|-----------------------------------|---------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------|
| POST        | `/api/signup`                     | Register a new user in the database.                                                  | ```json<br>{<br>  "email": "example@kbtu.kz",<br>  "password": "<your_password>",<br>  "visitorId": "<your_device_id>"<br>}```                                | ```json<br>{<br>  "message": "User registered successfully. Email confirmation is pending."<br>}```                                                          |
| POST        | `/api/signin`                     | Log an existing user into the system. Requires email confirmation before login.       | ```json<br>{<br>  "email": "example@kbtu.kz",<br>  "password": "<your_password>",<br>  "visitorId": "<your_device_id>"<br>}```                                | ```json<br>{<br>  "message": "Login successful",<br>  "access_token": "<your_access_token>",<br>  "refresh_token": "<your_refresh_token>"<br>}```             |
| POST        | `/api/forgot-password`            | Send a request when a user forgets their password.                                    | ```json<br>{<br>  "email": "example@kbtu.kz",<br>  "visitorId": "<your_device_id>"<br>}```                                                                    | "Forgot password email sent successfully."                                                                                                                    |
| GET         | `/api/reset-password/{userId}/{token}` | Validate a token and inform the client to send a new password.                        | Requires `userId` and `token` in the query parameters.                                                                                                       | ```json<br>{<br>  "message": "The link is valid. You can now reset your password.",<br>  "email": "example@kbtu.kz",<br>  "token": "<your_token>"<br>}```      |
| POST        | `/api/reset-password`             | Save the new password in the database.                                                | ```json<br>{<br>  "email": "example@kbtu.kz",<br>  "token": "<your_token>",<br>  "new_password": "<your_new_password>"<br>}```                                | ```json<br>{<br>  "message": "Your password has been reset successfully"<br>}```                                                                              |
| POST        | `/api/resend-confirmation-email`  | Resend the confirmation email to the user. Requires `email` and `visitorId` in body.  | ```json<br>{<br>  "email": "example@kbtu.kz",<br>  "visitorId": "<your_device_id>"<br>}```                                                                    | ```json<br>{<br>  "message": "Confirmation email sent successfully."<br>}```                                                                                 |
| POST        | `/api/post/create`                | Create a new post and make it visible to all users.                                   | ```json<br>{<br>  "description": "<your_post_description>",<br>  "currentPrice": 0,<br>  "latitude": 0,<br>  "longitude": 0,<br>  "createdAt": "<date_of_creation>",<br>  "maxPeople": 0<br>}``` | ```json<br>{<br>  "message": "The post was saved to the database"<br>}```                                                                                     |
| POST        | `/api/post/update/{id}`           | Update the information of a specific post.                                            | Query: Takes the ID of the post.<br>```json<br>{<br>  "Description": "<your_post_description>",<br>  "CurrentPrice": 0,<br>  "Latitude": 0,<br>  "MaxPeople": 0,<br>  "Members": [],<br>  "Longitude": 0,<br>  "CreatedAt": "<date_of_creation>"<br>}``` | ```json<br>{<br>  "message": "The post was successfully updated.",<br>  "post": {<br>    "postId": "<post_id>",<br>    "description": "<your_post_description>",<br>    "currentPrice": 0,<br>    "latitude": 0,<br>    "longitude": 0,<br>    "createdAt": "<date_of_creation>",<br>    "maxPeople": 0,<br>    "creator": {<br>      "email": "example@kbtu.kz",<br>      "name": "<your_name>"<br>    },<br>    "members": []<br>  }<br>}``` |
| GET         | `/api/post/read/{id}`             | Retrieve information about a specific post.                                           | Requires `id` of the post in the query.                                                                                                                       | ```json<br>{<br>  "postId": "<post_id>",<br>  "description": "<your_post_description>",<br>  "currentPrice": 0,<br>  "latitude": 0,<br>  "longitude": 0,<br>  "createdAt": "<date_of_creation>",<br>  "maxPeople": 0,<br>  "creator": {<br>    "email": "example@kbtu.kz",<br>    "name": "<your_name>"<br>  },<br>  "members": []<br>}``` |
| POST        | `/api/post/join/{id}`             | Add the current user as a member of the post.                                         | Requires `id` of the post in the query.                                                                                                                       | "The user was successfully added to the members of the post"                                                                                                 |
| POST        | `/api/post/delete/{id}`           | Delete the specific post.                                                             | Requires `id` of the post in the query.                                                                                                                       | ```json<br>{<br>  "message": "The post was successfully removed."<br>}```                                                                                   |
| GET         | `/api/post/read`                  | Retrieve all posts.                                                                   | No request body required.                                                                                                                                     | ```json<br>{<br>  "yourPosts": [<br>    {<br>      "postId": "<your_post_id>",<br>      "description": "<your_post_description>",<br>      "currentPrice": 0,<br>      "latitude": 0,<br>      "longitude": 0,<br>      "createdAt": "<date_of_post_creation>",<br>      "maxPeople": 0,<br>      "creator": {<br>        "email": "yourgmail@kbtu.kz",<br>        "name": "<your_name>"<br>      },<br>      "members": []<br>    }<br>  ],<br>  "restPosts": [<br>    {<br>      "postId": "<other_post_id>",<br>      "description": "<other_post_description>",<br>      "currentPrice": 0,<br>      "latitude": 0,<br>      "longitude": 0,<br>      "createdAt": "<other-post_creation_date>",<br>      "maxPeople": 0,<br>      "creator": {<br>        "email": "example@kbtu.kz",<br>        "name": "<other_creator_name>"<br>      },<br>      "members": []<br>    }<br>  ]<br>}``` |
| GET         | `/api/post/read/others`           | Retrieve posts that the user has joined.                                              | No request body required.                                                                                                                                     | ```json<br>{<br>  "postsWhereMember": [<br>    {<br>      "postId": "<other_post_id>",<br>      "description": "<other_post_description>",<br>      "currentPrice": 0,<br>      "latitude": 0,<br>      "longitude": 0,<br>      "createdAt": "<other-post_creation_date>",<br>      "maxPeople": 0,<br>      "creator": {<br>        "email": "example@kbtu.kz",<br>        "name": "<creator_name>"<br>      },<br>      "members": [<br>        {<br>          "email": "yourgmail@kbtu.kz",<br>          "name": "<your_name>"<br>        }<br>      ]<br>    }<br>  ]<br>}``` |