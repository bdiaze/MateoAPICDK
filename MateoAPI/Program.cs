using MateoAPI.Entities.Contexts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

string cognitoUserPoolId = Environment.GetEnvironmentVariable("COGNITO_USER_POOL_ID") ?? throw new ArgumentNullException("COGNITO_USER_POOL_ID");
string cognitoRegion = Environment.GetEnvironmentVariable("COGNITO_REGION") ?? throw new ArgumentNullException("COGNITO_REGION");
string[] allowedDomains = Environment.GetEnvironmentVariable("ALLOWED_DOMAINS")?.Split(",") ?? throw new ArgumentNullException("ALLOWED_DOMAINS");
string[] cognitoAppClientId = Environment.GetEnvironmentVariable("COGNITO_APP_CLIENT_ID")?.Split(",") ?? throw new ArgumentNullException("COGNITO_APP_CLIENT_ID");

string connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? throw new ArgumentNullException("CONNECTION_STRING");


builder.Services.AddControllers();

builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

builder.Services.AddDbContextPool<MateoDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddCors(item => {
    item.AddPolicy("CORSPolicy", builder => {
        builder.WithOrigins(allowedDomains)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options => {
        options.Authority = $"https://cognito-idp.{cognitoRegion}.amazonaws.com/{cognitoUserPoolId}";
        options.MetadataAddress = $"https://cognito-idp.{cognitoRegion}.amazonaws.com/{cognitoUserPoolId}/.well-known/openid-configuration";
        options.TokenValidationParameters = new TokenValidationParameters {
            ValidAudiences = cognitoAppClientId,
            ValidateIssuerSigningKey = true,
            NameClaimType = ClaimTypes.NameIdentifier,
        };
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("CORSPolicy");

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
