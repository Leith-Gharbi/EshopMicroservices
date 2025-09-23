#region Before Building the app
var builder = WebApplication.CreateBuilder(args);
// Add services to the container DI

#endregion

var app = builder.Build();

#region After Building the app

// Configure the HTTP request pipeline.

#endregion

app.Run();
