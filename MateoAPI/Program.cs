using MateoAPI.Entities.Contexts;
using MateoAPI.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

string secretNameConnectionString = Environment.GetEnvironmentVariable("SECRET_NAME_CONNECTION_STRING") ?? throw new ArgumentNullException("SECRET_NAME_CONNECTION_STRING");
string parameterNameCognitoRegion = Environment.GetEnvironmentVariable("PARAMETER_NAME_COGNITO_REGION") ?? throw new ArgumentNullException("PARAMETER_NAME_COGNITO_REGION");
string parameterNameCognitoUserPoolId = Environment.GetEnvironmentVariable("PARAMETER_NAME_COGNITO_USER_POOL_ID") ?? throw new ArgumentNullException("PARAMETER_NAME_COGNITO_USER_POOL_ID");
string parameterNameCognitoUserPoolClientId = Environment.GetEnvironmentVariable("PARAMETER_NAME_COGNITO_USER_POOL_CLIENT_ID") ?? throw new ArgumentNullException("PARAMETER_NAME_COGNITO_USER_POOL_CLIENT_ID");
string parameterNameApiAllowedDomains = Environment.GetEnvironmentVariable("PARAMETER_NAME_API_ALLOWED_DOMAINS") ?? throw new ArgumentNullException("PARAMETER_NAME_API_ALLOWED_DOMAINS");

dynamic connectionString = await SecretManager.ObtenerSecreto(secretNameConnectionString);
string cognitoRegion = await ParameterStore.ObtenerParametro(parameterNameCognitoRegion);
string cognitoUserPoolId = await ParameterStore.ObtenerParametro(parameterNameCognitoUserPoolId);
string[] cognitoAppClientId = (await ParameterStore.ObtenerParametro(parameterNameCognitoUserPoolClientId)).Split(",");
string[] allowedDomains = (await ParameterStore.ObtenerParametro(parameterNameApiAllowedDomains)).Split(",");

builder.Services.AddControllers();

builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

builder.Services.AddDbContextPool<MateoDbContext>(options => options.UseNpgsql(
    $"Server={connectionString.Host};Port={connectionString.Port};SslMode=prefer;" +
    $"Database={connectionString.MateoDatabase};User Id={connectionString.MateoUsername};Password='{connectionString.MateoPassword}';"
));

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
