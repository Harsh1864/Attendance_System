using Npgsql;
using project.Middleware;
using project.Services;
using try_project_casepoint.Services;
using MVC.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Secrets never live in appsettings.json. Supply the connection string with
// one of (later wins): appsettings.Local.json (git-ignored), user secrets
// (Development only), or the ConnectionStrings__Default environment variable.
builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
if (builder.Environment.IsDevelopment())
    builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Configuration.AddEnvironmentVariables();

if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Default")))
{
    throw new InvalidOperationException(
        "Connection string 'Default' is not set. Use one of:\n" +
        "  dotnet user-secrets set \"ConnectionStrings:Default\" \"Host=...;Database=...;Username=...;Password=...\"\n" +
        "  set ConnectionStrings__Default=Host=...;Database=...;Username=...;Password=...\n" +
        "  or create appsettings.Local.json (git-ignored). See README.md.");
}

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddSingleton<DbConnectionFactory>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<HolidayService>();
builder.Services.Configure<LeaveOptions>(builder.Configuration.GetSection("Leave"));
builder.Services.AddScoped<LeaveService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<AttendanceEditService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<CorrectionService>();
builder.Services.AddScoped<AuthServices>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<EmployeeViewService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<ProfileUpdateService>();
builder.Services.Configure<ShiftOptions>(builder.Configuration.GetSection("Shift"));
builder.Services.AddHostedService<DailyTaskService>();
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMQ"));
builder.Services.AddSingleton<RabbitMqService>();

// Redis options and services
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection("Redis"));
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RedisOptions>>().Value;
    return ConnectionMultiplexer.Connect(opts.Configuration);
});
builder.Services.AddSingleton<IChatService, RedisChatService>();

// Session via Redis
builder.Services.AddStackExchangeRedisCache(options =>
{
    var cfg = builder.Configuration.GetSection("Redis").GetValue<string>("Configuration");
    var instance = builder.Configuration.GetSection("Redis").GetValue<string>("InstanceName") ?? "MVC:";
    options.Configuration = cfg;
    options.InstanceName = instance + "session:";
});


builder.Services.AddScoped<NpgsqlConnection>(con =>
{
    var s = con.GetRequiredService<IConfiguration>().GetConnectionString("Default");
    return new NpgsqlConnection(s);
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // session timeout
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();


app.UseRouting();
app.UseSession();

app.UseAuthorization();
app.UseMiddleware<AuthMiddleware>();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
