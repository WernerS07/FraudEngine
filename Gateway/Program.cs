using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

// Load YARP config
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
// Keycloak settings
var kc = builder.Configuration.GetSection("Keycloak");
var authority = kc["Authority"]?.TrimEnd('/');
var clientId = kc["Audience"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"{authority}";
        options.Audience = clientId;
        options.RequireHttpsMetadata = false;
    });


// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("BasicPriv", p => p.RequireRole("basic-role", "higher-priv-role"));
    options.AddPolicy("HigherPriv", p => p.RequireRole("higher-priv-role"));
});

var app = builder.Build();

app.UseRouting();
// Add auth checks to endpoints
app.UseAuthentication();
// Add role checks to endpoints
app.UseAuthorization();

// Map the proxy. mapping fraud and mockdata endpoints
app.MapReverseProxy();

app.Run();
