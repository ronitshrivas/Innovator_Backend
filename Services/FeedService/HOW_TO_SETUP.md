# FeedService — Setup Instructions

## Step 1 — Copy FeedService into your project

Copy the entire `FeedService` folder into:
```
innovatorbackend/Services/FeedService/
```

So the structure becomes:
```
innovatorbackend/
├── Services/
│   ├── AuthService/
│   ├── ProfileService/
│   └── FeedService/        ← paste here
```

## Step 2 — Replace ApiGateway appsettings

Copy `ApiGateway_appsettings.json` (from this zip) and replace the existing file at:
```
innovatorbackend/ApiGateway/appsettings.json
```

## Step 3 — Add FeedService to the solution

In your terminal:
```bash
cd ~/innovatorbackend
dotnet sln add Services/FeedService/FeedService.csproj
```

## Step 4 — Create the database

```bash
psql postgres -c "CREATE DATABASE innovator_feed OWNER innovator;"
```

## Step 5 — Run migrations (from INSIDE FeedService folder)

```bash
cd ~/innovatorbackend/Services/FeedService

dotnet ef migrations add Init \
  --project FeedService.csproj \
  --startup-project FeedService.csproj

dotnet ef database update \
  --project FeedService.csproj \
  --startup-project FeedService.csproj
```

## Step 6 — Run

Open a new terminal tab for FeedService:
```bash
cd ~/innovatorbackend/Services/FeedService
dotnet run
```

Swagger: http://localhost:8012/swagger

## Step 7 — Update your Flutter ApiConstants

Change `_host` from `http://36.253.137.34` to `http://localhost:5000`
Everything routes through the gateway automatically.

## Endpoints covered

| Flutter ApiConstant     | Method | Path                        |
|-------------------------|--------|-----------------------------|
| post (feed)             | GET    | /api/feed/                  |
| createpost              | POST   | /api/posts/                 |
| recordview              | POST   | /api/posts/{id}/view        |
| fetchcategories         | GET    | /api/categories/            |
| sendreaction            | POST   | /api/reactions/             |
| fetchreactions          | GET    | /api/reactions/posts/{id}   |
| getcomments             | GET    | /api/comments/?post={id}    |
| addcomments             | POST   | /api/comments/              |
| updatecomments          | PATCH  | /api/comments/{id}/         |
| deletecomment           | DELETE | /api/comments/{id}/         |
| getcommentreplies       | GET    | /api/replies/?parent={id}   |
| addcommentreplies       | POST   | /api/replies/               |
| updatecommentreplies    | PATCH  | /api/replies/{id}/          |
| deletecommentreplies    | DELETE | /api/replies/{id}/          |
| fetchreelreactions      | GET    | /api/reels/                 |
| Update/Delete post      | PATCH  | /api/posts/{id}/            |
| Delete post             | DELETE | /api/posts/{id}/            |
| Update/Delete reel      | PATCH  | /api/reels/{id}/            |
| Delete reel             | DELETE | /api/reels/{id}/            |
