# DormGO Server

DormGO Server is the backend API for DormGO, DormGO is a mobile application that helps users save on
transportation costs by connecting people traveling to the same destination. Users can coordinate and share
rides by subscribing to the post, allowing them to split expenses and make travel more affordable and convenient.
This repository contains only the server-side (.NET) code.

---

## Features

- User authentication & registration (ASP.NET Identity, JWT)
- Manage posts through CRUD operations
- Booking/reservation management
- Real-time notifications & chat (SignalR hubs)
- Search/filter for posts
- Admin endpoints
- API documentation (Swagger)
- Email notifications
- Logging with Serilog

---

## Tech Stack

- **.NET 8 / ASP.NET Core**
- **Entity Framework Core** (SQL Server)
- **SignalR** (real-time communication)
- **Serilog** (logging)
- **Swagger** (API docs)
- **Mapster**(Dto mappings)
- **JWT** authentication

---

## Project Structure

```
/DormGO
  /Controllers        # API controllers
  /Models             # Entity/data models
  /Data               # DbContext and migrations
  /DTOs               # Data transfer objects
  /Services           # Business logic & notification services
  /Hubs               # SignalR hubs (real-time)
  /Filters            # Custom filters (e.g. user email validation)
  /Mappings           # Mapster config for DTOs
  /Constants          # Auth and config constants
  Program.cs          # Main entry point (see below)
  appsettings.json    # Configuration
  README.md           # This file
```

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download)
- SQL Server (or Azure SQL)

### Setup

1. **Clone the repository:**
   ```bash
   git clone https://github.com/blendereru/DormGO.git
   cd DormGO
   ```

2. **Configure Environment:**
    - Copy `appsettings.json` and set your connection strings and secrets.
    - Optionally use environment variables for sensitive values.

3**Run the Server:**
   ```bash
   dotnet run
   ```
   The API will start on `http://localhost:5093` by default.

---

## Running with Docker Compose

1. **Edit Environment Variables:**
  - Replace `<your_password>` in the `compose.yaml` under the `MSSQL_SA_PASSWORD` environment variable for the `db` service with a secure password.
  - Make sure your app's connection string (in `appsettings.json` or as an environment variable) matches the database service:
    ```
    Server=db,1433;Database=YOUR_DB_NAME;User Id=sa;Password=YOUR_PASSWORD;
    ```
  - (Optional) Set any other required secrets or configuration via your environment or override files.

2. **Build and start all services:**
   ```bash
   docker compose up --build
   ```
  - This command builds and launches all services defined in the `compose.yaml` file.
  - The API will be accessible at `http://localhost` (port 80) and `http://localhost:8080`.
  - Seq logging UI will be available at `http://localhost:8081`.
  - SQL Server will be available at `localhost:8002` for development tools.

3. **Stopping the services:**
   ```bash
   docker compose down
   ```

---

### Service Overview

- **app**: The DormGO backend API (.NET)  
  Exposes ports 80 and 8080 (adjust as needed).

- **db**: Microsoft SQL Server 2022  
  Accessible on port 8002 (host) → 1433 (container).  
  Default user: `sa`, password: set by `MSSQL_SA_PASSWORD`.

- **seq**: [Seq](https://datalust.co/seq) (structured log UI for Serilog)  
  Accessible at http://localhost:8081.

---

**Example connection string for the app (in `appsettings.json`):**
```json
"ConnectionStrings": {
  "IdentityConnection": "Server=db,1433;Database=DormGO;User Id=sa;Password=YOUR_PASSWORD;"
}
```

**Tip:**
- For production, always use strong secrets and manage them securely.
- You may use the `.env` file to inject environment variables into your containers.

---

## API Overview

- Full Swagger UI at `/swagger`
- Example endpoints:
    - `POST /api/signin` — Login
    - `POST /api/signup` — Register
    - `GET /api/posts` — List posts that were created by users
    - `POST /api/posts` — Add a post. You specify a price per user and the maximum people capacity
    - `GET /api/posts/{id}/messages` — List all messages that were sent by members of the post
    - SignalR hubs: `/api/userhub`, `/api/posthub`, `/api/chathub`, `/api/notificationhub`

---

## Media & Illustrations

- **System Architecture Diagram**  
  ![Example architecture diagram](docs/images/architecture.png)  
  _Shows ASP.NET Core backend, SignalR, SQL Server, and how they interact with the client._

- **API Usage Example**  
  ![Postman API example](docs/images/api-request.png)  
  _Screenshot of a Swagger or Postman test of a typical endpoint._

- **Entity Relationship Diagram**  
  ![ER diagram](docs/images/db-er.png)  
  _Visual of the main models: User, Dormitory, Booking, etc._

- **Demo Video**  
  [Demo Video](docs/videos/server-demo.mp4)  
  _Short screen recording showing registration, login, and a real-time notification via SignalR._

---

## Contributing

Pull requests are welcome! For major changes, please open an [issue](https://github.com/blendereru/DormGO/issues/new)
first to discuss.

Also contain a [discussions](https://github.com/blendereru/DormGO/discussions) section, where I can answer your
questions.

---

## License

[MIT](LICENSE)

---

## Related

- [DormGO Client](https://github.com/Raimbek-pro/DormGo-ios-client) — Frontend for DormGO
