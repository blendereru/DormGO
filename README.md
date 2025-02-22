# DormGO
[![CI/CD](https://github.com/blendereru/DormGO/actions/workflows/release.yml/badge.svg)](https://github.com/blendereru/DormGO/actions/workflows/release.yml)
<a href="https://dotnet.microsoft.com/en-us/"><img src="https://img.shields.io/badge/version-8.0-600aa6?style=flat&logo=dotnet&link=https://dotnet.microsoft.com/en-us/" alt="version" /></a>
<a href="https://www.swift.org/"><img src="https://img.shields.io/badge/Swift-5.0-e35424?style=flat&logo=swift&logoColor=white&link=https://www.swift.org/" alt="Swift" /></a>
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://opensource.org/licenses/MIT)

The mobile application that allows people to spend less money to a transport. People who are aiming to the
same destination can gather with each other in the app, and divide the money thus saving their money.
## Demos
### Home Screen & Post Creation Page
<div style="display: flex; gap: 20px;">
  <img src="resources/screen.jpg" width="300" />
  <img src="resources/screen2.png" width="300" />
</div>

## 🏗️ Architecture
This project follows the MVVM (Model-View-ViewModel) architecture pattern with SwiftUI for the user interface and Combine for reactive data binding.

## Setup for the app
    • Xcode (version 15 or later)
    • macOS (version 12 or later)
## 📦 Dependencies
    • Combine: Reactive programming framework for data binding.
    • CoreLocation: For location services and retrieving the user’s location.
    • MapKit: For map view and geolocation-related functionality.
    • SignalRClient: For websocket implementation
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

4. If you want to test the API by running it on [Docker](https://www.docker.com/get-started/),
you have to pass your secrets to the following files:
* [compose file](https://github.com/blendereru/LoginForm/blob/1c92e0acf566d70069fcfa99a24913562baa6e65/compose.yaml): 
```yaml
environment:
  - ACCEPT_EULA=Y
  - MSSQL_SA_PASSWORD=<your_password>
```
* appsettings:
```json
"ConnectionStrings": {
  "IdentityConnection": "Server=db, 1433;Database=dormgo-db;User Id=sa;Password=<your_password>;TrustServerCertificate=true"
}
```
## 📗 License
The project code and all the resources are distributed under the terms of [MIT license](https://github.com/blendereru/LoginForm/blob/f9ec9cd269e0b785c8a7b778e4d4f16fdb4a1427/LICENSE)

