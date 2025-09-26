#region Before Building the app
using BuildingBlocks.Behaviors;

var builder = WebApplication.CreateBuilder(args);

// ===== PHASE 1: CONFIGURATION DES SERVICES =====
// (L'app n'existe pas encore, on configure juste le container DI)
// Add services to the container DI

var assembly = typeof(Program).Assembly; // Get the current assembly ( Catalog.API )


builder.Services.AddMediatR(config =>
{
    config.RegisterServicesFromAssembly(assembly); // register MediatR handlers in the DI container ( Detecte tous les classes qui implémentent IRequestHandler<T> dans l'assembly courant )
    config.AddOpenBehavior(typeof(ValidationBehavior<,>));
}
); // register MediatR in the DI container

builder.Services.AddValidatorsFromAssembly(assembly); // register FluentValidation validators in the DI container ( Detecte tous les classes qui implémentent Abstractvalidator<T> dans l'assembly courant )


builder.Services.AddCarter();  // register Carter in the DI container

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
