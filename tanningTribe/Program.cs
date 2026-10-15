using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using tanningTribe.Data;
using tanningTribe.Services;

if (args.Length == 2 && args[0] == "hash-password")
{
    Console.WriteLine(PasswordHasher.Hash(args[1]));
    return;
}

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin");
    options.Conventions.AllowAnonymousToPage("/Admin/Login");
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Admin/Login";
        options.LogoutPath = "/Admin/Logout";
        options.AccessDeniedPath = "/Admin/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });
builder.Services.AddAuthorization();
builder.Services.Configure<AdminSettings>(builder.Configuration.GetSection("Admin"));
builder.Services.AddDbContext<StoreDbContext>(options =>
{
    var connectionString = ConnectionStringResolver.Resolve(builder.Configuration)
        ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection (or MYSQL_URL) is not configured.");
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024;
});
builder.Services.AddDataProtection()
    .SetApplicationName("TannedAndToned")
    .PersistKeysToDbContext<StoreDbContext>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<ReferralService>();
builder.Services.AddScoped<ImageService>();
builder.Services.AddScoped<WhatsAppService>();
builder.Services.AddScoped<OrderWorkflowService>();
builder.Services.AddScoped<OrderNumberService>();
builder.Services.Configure<WhatsAppSettings>(builder.Configuration.GetSection("WhatsApp"));
builder.Services.Configure<PaymentSettings>(builder.Configuration.GetSection("Payment"));
builder.Services.Configure<RoyalMailSettings>(builder.Configuration.GetSection("RoyalMail"));
builder.Services.AddHttpClient<RoyalMailService>();
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.AddHttpClient<ResendService>();
builder.Services.AddScoped<ConversationService>();

var app = builder.Build();

app.UseForwardedHeaders();

var culture = new System.Globalization.CultureInfo("en-GB");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(culture),
    SupportedCultures = [culture],
    SupportedUICultures = [culture]
});

await SeedData.InitializeAsync(app.Services);

{
    var admin = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AdminSettings>>().Value;
    var hash = admin.PasswordHash ?? string.Empty;
    var parts = hash.Split('.');
    app.Logger.LogInformation(
        "Admin config: Username='{Username}', PasswordHash length={Length}, parts={Parts}, placeholder={IsPlaceholder}, startsWith='{Prefix}'",
        admin.Username, hash.Length, parts.Length, hash.Contains("SET_IN_USER_SECRETS"), hash.Length >= 8 ? hash[..8] : hash);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.UseStaticFiles(); // serves runtime uploads (product images) that aren't in the build-time asset manifest
app.MapRazorPages()
   .WithStaticAssets();

app.MapPost("/webhooks/resend", async (HttpRequest request, ResendService resend, ConversationService conversations, ILogger<Program> logger) =>
{
    using var reader = new StreamReader(request.Body);
    var payload = await reader.ReadToEndAsync();

    if (!resend.VerifyWebhook(request.Headers, payload))
    {
        return Results.Unauthorized();
    }

    using var json = System.Text.Json.JsonDocument.Parse(payload);
    var root = json.RootElement;
    var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
    if (type != "email.received" || !root.TryGetProperty("data", out var data))
    {
        return Results.Ok();
    }

    var inbound = ResendService.ParseInbound(data);
    if (string.IsNullOrEmpty(inbound.Text) && string.IsNullOrEmpty(inbound.Html) && !string.IsNullOrEmpty(inbound.EmailId))
    {
        // the webhook payload only carries metadata; fetch the body
        inbound = await resend.GetReceivedAsync(inbound.EmailId) ?? inbound;
    }

    await conversations.RecordInboundAsync(inbound);
    return Results.Ok();
}).AllowAnonymous();

app.Run();
