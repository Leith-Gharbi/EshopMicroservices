#region Before Building the app
var builder = WebApplication.CreateBuilder(args);

// ===== PHASE 1: CONFIGURATION DES SERVICES =====
// (L'app n'existe pas encore, on configure juste le container DI)
// Add services to the container DI
builder.Services.AddCarter();  // register Carter in the DI container
builder.Services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(Program).Assembly)); // register MediatR in the DI container

builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly); // register FluentValidation validators in the DI container ( Detecte tous les classes qui implémentent Abstractvalidator<T> dans l'assembly courant )
builder.Services.AddMarten(options =>
{
    options.Connection(builder.Configuration.GetConnectionString("Database")!);

}).UseLightweightSessions(); // register Marten in the DI container

#endregion

// ===== CONSTRUCTION DE L'APPLICATION =====
var app = builder.Build();

#region After Building the app

// ===== PHASE 2: CONFIGURATION DU PIPELINE HTTP =====
// (Maintenant l'app existe, on configure comment elle traite les requêtes)

// Configure the HTTP request pipeline.

app.MapCarter(); // Map Carter endpoints ( Map tous les endpoints Carter
#endregion

// ===== DÉMARRAGE =====
app.Run(); // Serveur démarré !
