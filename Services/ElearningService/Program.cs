using System.Text;
using System.Text.Json.Serialization;
using ElearningService.Common;
using ElearningService.Data;
using ElearningService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ElearningDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IElearningAdminService, ElearningAdminService>();
builder.Services.AddScoped<IVendorService, VendorService>();
builder.Services.AddScoped<IBannerService, BannerService>();

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
            ClockSkew = TimeSpan.Zero
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
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Innovator E-learning Service", Version = "v1" });
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
    var db = scope.ServiceProvider.GetRequiredService<ElearningDbContext>();
    await db.Database.EnsureCreatedAsync();

    // EnsureCreated won't add new tables to a DB that already exists, so the
    // vendor-accounts table is created explicitly here. Idempotent.
    await db.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS ""Vendors"" (
            ""Id"" uuid PRIMARY KEY,
            ""Name"" text NOT NULL DEFAULT '',
            ""Email"" text NOT NULL DEFAULT '',
            ""Username"" text NOT NULL DEFAULT '',
            ""PasswordHash"" text NOT NULL DEFAULT '',
            ""IsActive"" boolean NOT NULL DEFAULT true,
            ""CreatedAt"" timestamptz NOT NULL DEFAULT now(),
            ""UpdatedAt"" timestamptz NOT NULL DEFAULT now()
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Vendors_Username"" ON ""Vendors"" (""Username"");

        CREATE TABLE IF NOT EXISTS ""Banners"" (
            ""Id"" uuid PRIMARY KEY,
            ""Title"" text NOT NULL DEFAULT '',
            ""Image"" text NOT NULL DEFAULT '',
            ""CourseId"" uuid NULL,
            ""IsActive"" boolean NOT NULL DEFAULT true,
            ""SortOrder"" integer NOT NULL DEFAULT 0,
            ""CreatedAt"" timestamptz NOT NULL DEFAULT now(),
            ""UpdatedAt"" timestamptz NOT NULL DEFAULT now()
        );
    ");

    await ElearningSeeder.SeedAsync(db);
});

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
