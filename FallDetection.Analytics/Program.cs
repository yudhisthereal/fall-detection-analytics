using FallDetection.Analytics.Services;

var builder = WebApplication.CreateBuilder(args);

// Disable HTTPS redirection to prevent the warning
builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
    options.HttpsPort = 0; // Disable HTTPS redirection by setting port to 0
});

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register services
builder.Services.AddSingleton<PoseEstimationService>();
builder.Services.AddSingleton<FallDetectionService>();
builder.Services.AddSingleton<CameraRegistryService>();

// Configure Kestrel to listen on port 5000
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(5000);
});

var app = builder.Build();

// Configure HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Create data directory if it doesn't exist
var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
if (!Directory.Exists(dataDir))
{
    Directory.CreateDirectory(dataDir);
}

app.Run();