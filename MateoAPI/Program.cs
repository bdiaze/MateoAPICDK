using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

string cognitoAppClientId = Environment.GetEnvironmentVariable("COGNITO_APP_CLIENT_ID") ?? throw new ArgumentNullException("COGNITO_APP_CLIENT_ID");
string cognitoUserPoolId = Environment.GetEnvironmentVariable("COGNITO_USER_POOL_ID") ?? throw new ArgumentNullException("COGNITO_USER_POOL_ID");
string cognitoRegion = Environment.GetEnvironmentVariable("COGNITO_REGION") ?? throw new ArgumentNullException("COGNITO_REGION");
string[] allowedDomains = Environment.GetEnvironmentVariable("ALLOWED_DOMAINS")?.Split(",") ?? throw new ArgumentNullException("ALLOWED_DOMAINS");


builder.Services.AddControllers();

builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

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
        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateAudience = true,
            ValidAudience = cognitoAppClientId,
            ValidateLifetime = true
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
