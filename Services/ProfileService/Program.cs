using System.Text;
using System.Text.Json.Serialization;
using ProfileService.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ProfileService.Data;
using ProfileService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ProfileDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IProfileService, ProfileBusinessService>();
builder.Services.AddScoped<IAvatarStorageService, AvatarStorageService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();

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
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Innovator Profile Service", Version = "v1" });
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
    var db = scope.ServiceProvider.GetRequiredService<ProfileDbContext>();
    await db.Database.MigrateAsync();

    // New multi-value profile columns added outside EF migrations. Idempotent.
    await db.Database.ExecuteSqlRawAsync(@"
        ALTER TABLE ""UserProfiles"" ADD COLUMN IF NOT EXISTS ""EducationsJson"" text NOT NULL DEFAULT '[]';
        ALTER TABLE ""UserProfiles"" ADD COLUMN IF NOT EXISTS ""OccupationsJson"" text NOT NULL DEFAULT '[]';
        ALTER TABLE ""UserProfiles"" ADD COLUMN IF NOT EXISTS ""LinksJson"" text NOT NULL DEFAULT '[]';
        ALTER TABLE ""UserProfiles"" ADD COLUMN IF NOT EXISTS ""CoverImagePath"" text NULL;
    ");

    // Per-user settings table, created outside EF migrations. Idempotent.
    await db.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS ""UserSettings"" (
            ""Id"" uuid PRIMARY KEY,
            ""UserId"" uuid NOT NULL,
            ""PushEnabled"" boolean NOT NULL DEFAULT TRUE,
            ""NotifyLikes"" boolean NOT NULL DEFAULT TRUE,
            ""NotifyComments"" boolean NOT NULL DEFAULT TRUE,
            ""NotifyFollows"" boolean NOT NULL DEFAULT TRUE,
            ""NotifyMentions"" boolean NOT NULL DEFAULT TRUE,
            ""NotifyMessages"" boolean NOT NULL DEFAULT TRUE,
            ""NotifyReposts"" boolean NOT NULL DEFAULT TRUE,
            ""EmailDigest"" boolean NOT NULL DEFAULT FALSE,
            ""PrivateAccount"" boolean NOT NULL DEFAULT FALSE,
            ""WhoCanMessage"" text NOT NULL DEFAULT 'everyone',
            ""WhoCanComment"" text NOT NULL DEFAULT 'everyone',
            ""ShowActivityStatus"" boolean NOT NULL DEFAULT TRUE,
            ""ShowInSearch"" boolean NOT NULL DEFAULT TRUE,
            ""Language"" text NOT NULL DEFAULT 'en',
            ""Theme"" text NOT NULL DEFAULT 'system',
            ""Timezone"" text NULL,
            ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT (now() at time zone 'utc'),
            ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT (now() at time zone 'utc')
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ""IX_UserSettings_UserId"" ON ""UserSettings"" (""UserId"");
    ");

    // One-time cleanup: clear avatar/cover paths whose file no longer exists on
    // disk (e.g. wiped by a pre-persistence redeploy) so clients fall back to a
    // letter avatar instead of a broken 404 image.
    var storage = scope.ServiceProvider.GetRequiredService<IAvatarStorageService>();
    var stale = await db.UserProfiles
        .Where(p => p.AvatarPath != null || p.CoverImagePath != null)
        .ToListAsync();
    var changed = false;
    foreach (var p in stale)
    {
        if (p.AvatarPath != null && !storage.FileExists(p.AvatarPath))
        {
            p.AvatarPath = null;
            changed = true;
        }
        if (p.CoverImagePath != null && !storage.FileExists(p.CoverImagePath))
        {
            p.CoverImagePath = null;
            changed = true;
        }
    }
    if (changed) await db.SaveChangesAsync();
});

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseStaticFiles();

// Serve uploaded media (avatars, covers) from a persistent path outside the
// ephemeral wwwroot, at the same /avatars/... and /covers/... public URLs.
var mediaRoot = builder.Configuration["MediaStoragePath"];
if (!string.IsNullOrWhiteSpace(mediaRoot))
{
    Directory.CreateDirectory(Path.Combine(mediaRoot, "avatars"));
    Directory.CreateDirectory(Path.Combine(mediaRoot, "covers"));
    app.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(mediaRoot),
        RequestPath = ""
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();