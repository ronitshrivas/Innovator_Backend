using System.Text;
using System.Text.Json.Serialization;
using FeedService.Common;
using FeedService.Data;
using FeedService.Services;
using Innovator.Shared.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<FeedDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IFeedService, FeedBusinessService>();
builder.Services.AddScoped<IReactionService, ReactionService>();
builder.Services.AddScoped<ICommentService, CommentBusinessService>();
builder.Services.AddScoped<IMediaStorageService, MediaStorageService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddFirebasePush(builder.Configuration);

// Used to resolve authors' current avatars from the profile service.
builder.Services.AddHttpClient("profile", c =>
{
    var baseUrl = builder.Configuration["ProfileServiceUrl"] ?? "http://profile-service:8011";
    c.BaseAddress = new Uri(baseUrl);
    c.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddScoped<IProfileAvatarResolver, ProfileAvatarResolver>();
builder.Services.AddScoped<ISettingsClient, SettingsClient>();

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is required.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = SnakeCaseNamingPolicy.Instance;
        options.JsonSerializerOptions.DictionaryKeyPolicy = SnakeCaseNamingPolicy.Instance;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    });
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Innovator Feed Service", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(p =>
        p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

await Innovator.Shared.Helpers.StartupDb.InitializeAsync(async () =>
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FeedDbContext>();
    await db.Database.MigrateAsync();

    // Social notifications + FCM tokens are created outside EF migrations so
    // the feature ships without new migration files. Idempotent.
    await db.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS ""Notifications"" (
            ""Id"" uuid PRIMARY KEY,
            ""UserId"" uuid NOT NULL,
            ""Title"" varchar(200) NOT NULL DEFAULT '',
            ""Message"" varchar(500) NOT NULL DEFAULT '',
            ""Type"" varchar(50) NOT NULL DEFAULT '',
            ""SenderId"" uuid NULL,
            ""SenderUsername"" text NULL,
            ""SenderAvatar"" text NULL,
            ""RelatedPostId"" uuid NULL,
            ""IsRead"" boolean NOT NULL DEFAULT false,
            ""CreatedAt"" timestamptz NOT NULL DEFAULT now(),
            ""UpdatedAt"" timestamptz NOT NULL DEFAULT now()
        );
        CREATE INDEX IF NOT EXISTS ""IX_Notifications_User_Created""
            ON ""Notifications"" (""UserId"", ""CreatedAt"");

        CREATE TABLE IF NOT EXISTS ""FcmTokens"" (
            ""Id"" uuid PRIMARY KEY,
            ""UserId"" uuid NOT NULL,
            ""Token"" text NOT NULL,
            ""DeviceName"" text NULL,
            ""CreatedAt"" timestamptz NOT NULL DEFAULT now(),
            ""UpdatedAt"" timestamptz NOT NULL DEFAULT now()
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ""IX_FcmTokens_User_Token""
            ON ""FcmTokens"" (""UserId"", ""Token"");
    ");
});

// Normalise trailing slashes so the app's "/api/notifications/" style URLs
// route to the canonical slash-less endpoints (keeps Swagger unambiguous).
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;
    if (!string.IsNullOrEmpty(path) && path.Length > 1 && path.EndsWith('/'))
        context.Request.Path = path.TrimEnd('/');
    await next();
});

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
