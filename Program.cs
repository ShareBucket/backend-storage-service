using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ShareBucket.DataAccessLayer.Data;
using ShareBucket.JwtMiddlewareClient;
using StorageMicroService.Models.Services.Application.Storage;

var builder = WebApplication.CreateBuilder(args);
var _configuration = builder.Configuration;

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Inject services into controllers

// Storage Controller
builder.Services.AddTransient<IStreamFileService, StreamFileService>();
builder.Services.AddDbContext<DataContext>(options =>
    options.UseSqlServer(_configuration.GetConnectionString("DefaultConnection"), b => b.MigrationsAssembly("UserMicroService")));


builder.Services.AddCors(p => p.AddPolicy("corsapp", builder =>
{
    builder
    .WithOrigins("http://localhost:3000",
                 "https://localhost:3000",
                 "http://localhost:7000",
                 "https://localhost:7001")
    .SetIsOriginAllowed(origin => true)
    .AllowCredentials()
    .AllowAnyMethod()
    .AllowAnyHeader();
}));


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<JwtMiddleware>();

app.UseHttpsRedirection();

app.UseCors("corsapp");

app.UseAuthorization();

app.MapControllers();

app.Run();
