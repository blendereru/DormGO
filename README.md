# LoginForm
<a href="https://dotnet.microsoft.com/en-us/"><img src="https://img.shields.io/badge/version-8.0-600aa6?style=flat&logo=dotnet&link=https://dotnet.microsoft.com/en-us/" alt="version" /></a>
<a href="https://www.swift.org/"><img src="https://img.shields.io/badge/Swift-5.0-e35424?style=flat&logo=swift&logoColor=white&link=https://www.swift.org/" alt="Swift" /></a>
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://opensource.org/licenses/MIT)

The process of sign-in, sign-up in mobile-app. It uses `Swift` and `ASP.NET Core` for client and server interaction, and is available
in `Apple` devices. 
## Endpoints
`Server-side` defines multiple `endpoints` for client to send requests to. 
* `/api/signup` - is needed to register new user in a database
* `/api/confirm-email` - is needed to confirm user's email.
* `/api/signin` - is needed to log an existing user in the system. Doesn't allow to sign in until the `email` is confirmed.
* `/api/protected` - example of protected resource to which only `authorized` users can get access to.` Client-side` needs to pass
`JSON Web Token`(shortly `jwt`) in a request header.

## 🚨 Requirements
To use this project, ensure the following requirements are met:
1. A running instance of `SQL Server` (local or remote). Update the `ConnectionStrings:IdentityConnection` in the
`appsettings.json` file with your `SQL Server` details:
```json
"ConnectionStrings": {
  "IdentityConnection": "Data Source=your-server-name;Initial Catalog=your-database-name;Integrated Security=True;"
}
```
2. Register your application with `Google` to enable `OAuth2` login. Obtain your `ClientId` and `ClientSecret` from the `Google Cloud Console` and
update the following section in `appsettings.json`:
```json
"GoogleServices": {
  "ClientId": "your-client-id",
  "ClientSecret": "your-client-secret"
}
```
3. Configure `email` settings for sending confirmation emails. Use a valid `SMTP` provider such as `Gmail`. Then
update the `EmailSettings` section in the `appsettings.json` file:
```json
"EmailSettings": {
  "MailServer": "smtp.gmail.com",
  "MailPort": 587,
  "SenderName": "email-identity",
  "FromEmail": "your-email@gmail.com",
  "Password": "your-email-password"
}
```
If you're using Gmail, use an [App Password](https://support.google.com/accounts/answer/185833?hl=en). 
## 🎯 ToDos
1. [X] Set `Webhook/Long polling` to  wait for server's `JWT` after email confirmation
2. [ ] Track user's session in `database` to ensure no suspicious action is done
3. [ ] Ask to confirm `email` after `/api/signin` to ensure the right user is logging in.
## 📗 License
The project code and all the resources are distributed under the terms of [MIT license](https://github.com/blendereru/LoginForm/blob/f9ec9cd269e0b785c8a7b778e4d4f16fdb4a1427/LICENSE)

