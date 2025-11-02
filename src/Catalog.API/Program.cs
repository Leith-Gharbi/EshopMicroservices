#region Before Building the app






using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using BuildingBlocks.Logging;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog logging with Elasticsearch, File, and Console sinks
builder.AddSerilogLogging();

// ===== PHASE 1: CONFIGURATION DES SERVICES =====
// (L'app n'existe pas encore, on configure juste le container DI)
// Add services to the container DI

var assembly = typeof(Program).Assembly; // Get the current assembly ( Catalog.API )


builder.Services.AddMediatR(config =>
{
    config.RegisterServicesFromAssembly(assembly); // register MediatR handlers in the DI container ( Detecte tous les classes qui impl�mentent IRequestHandler<T> dans l'assembly courant )
    config.AddOpenBehavior(typeof(ValidationBehavior<,>));
    config.AddOpenBehavior(typeof(LoggingBehavior<,>));
}
); // register MediatR in the DI container

builder.Services.AddValidatorsFromAssembly(assembly); // register FluentValidation validators in the DI container ( Detecte tous les classes qui impl�mentent Abstractvalidator<T> dans l'assembly courant )

builder.Services.AddCarter();  // register Carter in the DI container

builder.Services.AddMarten(options =>
{
    options.Connection(builder.Configuration.GetConnectionString("Database")!);

}).UseLightweightSessions(); // register Marten in the DI container

if (builder.Environment.IsDevelopment())
    builder.Services.InitializeMartenWith<CatalogInitialData>(); // Initialize Marten with CatalogInitialData ( pour initialiser la base de donn�es avec des donn�es de test en d�veloppement )

builder.Services.AddExceptionHandler<CustomExceptionHandler>(); // register CustomExceptionHandler in the DI container ( pour g�rer les exceptions globalement )


builder.Services.AddHealthChecks().AddNpgSql(builder.Configuration.GetConnectionString("Database")!); // register HealthChecks in the DI container ( pour v�rifier la sant� de l'application )

#endregion

// ===== CONSTRUCTION DE L'APPLICATION =====
var app = builder.Build();

#region After Building the app

// ===== PHASE 2: CONFIGURATION DU PIPELINE HTTP =====
// (Maintenant l'app existe, on configure comment elle traite les requ�tes)

// Configure the HTTP request pipeline.

// Add HTTP logging middleware for Elasticsearch enrichment
app.UseElasticsearchHttpLogging();

app.MapCarter(); // Map Carter endpoints ( Map tous les endpoints Carter [ tous les classes qui implement ICarterModule)


app.UseExceptionHandler(options => { }); // Use the exception handler middleware ( pour g�rer les exceptions globalement) [ doit etre ajout� pour que AddExceptionHandler fonctionne (Appelle l�IExceptionHandler que tu as enregistr� (CustomExceptionHandler dans ton cas))  ] 

app.UseHealthChecks("/health" , new HealthCheckOptions 
{ 
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse

} ); // Map the health check endpoint ( pour v�rifier la sant� de l'application)

#endregion

// ===== D�MARRAGE =====
app.Run(); // Serveur d�marr� !
