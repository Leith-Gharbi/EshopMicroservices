#region Before Building the app






using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// ===== PHASE 1: CONFIGURATION DES SERVICES =====
// (L'app n'existe pas encore, on configure juste le container DI)
// Add services to the container DI

var assembly = typeof(Program).Assembly; // Get the current assembly ( Catalog.API )


builder.Services.AddMediatR(config =>
{
    config.RegisterServicesFromAssembly(assembly); // register MediatR handlers in the DI container ( Detecte tous les classes qui implémentent IRequestHandler<T> dans l'assembly courant )
    config.AddOpenBehavior(typeof(ValidationBehavior<,>));
    config.AddOpenBehavior(typeof(LoggingBehavior<,>));
}
); // register MediatR in the DI container

builder.Services.AddValidatorsFromAssembly(assembly); // register FluentValidation validators in the DI container ( Detecte tous les classes qui implémentent Abstractvalidator<T> dans l'assembly courant )

builder.Services.AddCarter();  // register Carter in the DI container

builder.Services.AddMarten(options =>
{
    options.Connection(builder.Configuration.GetConnectionString("Database")!);

}).UseLightweightSessions(); // register Marten in the DI container

if (builder.Environment.IsDevelopment())
    builder.Services.InitializeMartenWith<CatalogInitialData>(); // Initialize Marten with CatalogInitialData ( pour initialiser la base de données avec des données de test en développement )

builder.Services.AddExceptionHandler<CustomExceptionHandler>(); // register CustomExceptionHandler in the DI container ( pour gérer les exceptions globalement )


builder.Services.AddHealthChecks().AddNpgSql(builder.Configuration.GetConnectionString("Database")!); // register HealthChecks in the DI container ( pour vérifier la santé de l'application )
#endregion

// ===== CONSTRUCTION DE L'APPLICATION =====
var app = builder.Build();

#region After Building the app

// ===== PHASE 2: CONFIGURATION DU PIPELINE HTTP =====
// (Maintenant l'app existe, on configure comment elle traite les requêtes)

// Configure the HTTP request pipeline.



app.MapCarter(); // Map Carter endpoints ( Map tous les endpoints Carter [ tous les classes qui implement ICarterModule)


app.UseExceptionHandler(options => { }); // Use the exception handler middleware ( pour gérer les exceptions globalement) [ doit etre ajouté pour que AddExceptionHandler fonctionne (Appelle l’IExceptionHandler que tu as enregistré (CustomExceptionHandler dans ton cas))  ] 

app.UseHealthChecks("/health" , new HealthCheckOptions 
{ 
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse

} ); // Map the health check endpoint ( pour vérifier la santé de l'application)

#endregion

// ===== DÉMARRAGE =====
app.Run(); // Serveur démarré !
