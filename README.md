# Innovator Backend — Microservice Architecture (.NET 7, PostgreSQL)

## Architecture Overview

```
InnovatorBackend/
├── ApiGateway/              ← Yarp reverse proxy (port 5000)
├── Services/
│   ├── AuthService/         ← Port 8010  (JWT, OTP, SSO)
│   ├── ProfileService/      ← Port 8011  (users, avatars, follows)
│   ├── FeedService/         ← Port 8005  (posts, reactions, reels)  [Phase 2]
│   ├── ChatService/         ← Port 8006  (WebSocket, messages)      [Phase 3]
│   └── NotificationService/ ← Port 8007  (FCM, polling)             [Phase 4]
└── Shared/
    └── Innovator.Shared/    ← DTOs, JWT helpers, base entities
```

Each service owns its own **PostgreSQL database**. They never share a DB.
Services talk to each other only through HTTP (no direct DB cross-reads).

---

## Mac Setup (dotnet 7.0.317)

### 1. Verify your SDK
```bash
dotnet --version
# should print 7.0.317
```

### 2. Install PostgreSQL (Homebrew)
```bash
brew install postgresql@15
brew services start postgresql@15
echo 'export PATH="/opt/homebrew/opt/postgresql@15/bin:$PATH"' >> ~/.zshrc
source ~/.zshrc

# Create databases
psql postgres -c "CREATE USER innovator WITH PASSWORD 'innovator123';"
psql postgres -c "CREATE DATABASE innovator_auth OWNER innovator;"
psql postgres -c "CREATE DATABASE innovator_profile OWNER innovator;"
psql postgres -c "CREATE DATABASE innovator_feed OWNER innovator;"
```

### 3. Install EF Core tools
```bash
dotnet tool install --global dotnet-ef --version 7.*
# verify
dotnet ef --version
```

### 4. Clone and restore
```bash
cd InnovatorBackend
dotnet restore InnovatorBackend.sln
```

### 5. Run migrations (AuthService example)
```bash
cd Services/AuthService
dotnet ef migrations add InitialCreate --project AuthService.csproj
dotnet ef database update
```

### 6. Start all services (development)
Open 3 terminal tabs:
```bash
# Tab 1
cd Services/AuthService && dotnet run

# Tab 2
cd Services/ProfileService && dotnet run

# Tab 3
cd ApiGateway && dotnet run
```

### 7. Swagger UI
- Auth:    http://localhost:8010/swagger
- Profile: http://localhost:8011/swagger
- Gateway: http://localhost:5000/swagger

---

## Phase Roadmap

| Phase | Services             | Status      |
|-------|----------------------|-------------|
| 1     | Auth + Profile       | ✅ This PR  |
| 2     | Feed + Reels         | Next        |
| 3     | Chat (WebSocket)     | Upcoming    |
| 4     | Notifications (FCM)  | Upcoming    |
| 5     | Search + Suggestions | Upcoming    |
