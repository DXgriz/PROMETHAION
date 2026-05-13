using Microsoft.EntityFrameworkCore;
using Promethaion.API.BackgroundServices;
using Promethaion.API.Extensions;
using Promethaion.API.Hubs;
using Promethaion.Core.Interfaces;
using Promethaion.Core.Services;
using Promethaion.Data;
using Promethaion.Data.Harvesters;
using Promethaion.Data.Repositories;
using Promethaion.ML;
using Promethaion.ML.Pipelines;
using Promethaion.ML.Services;


var builder = WebApplication.CreateBuilder(args);

// ── Database Registration with retry logic for transient faults
builder.Services.AddDbContext<PAionDbContext>(opt =>
    opt.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql =>
        {
            sql.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        }));

// ── Repositories 
builder.Services.AddScoped<IPatterneventRepository, PatternEventRepository>();
builder.Services.AddScoped<IPatternForecastRepository, ForecastRepository>();
builder.Services.AddScoped<ITrainingMetricsRepository, TrainingMetricsRepository>();

// ── Core Services 
builder.Services.AddScoped<IAdaptiveIntelligenceEngine, AdaptiveIntelligenceEngine>();

// ── ML Pipelines 
var modelsDir = builder.Configuration.GetValue<string>("ML:ModelsDirectory") ?? "models";

builder.Services.AddSingleton<FrequencyAnalysisPipeline>(_ => new FrequencyAnalysisPipeline(modelsDir));
builder.Services.AddSingleton<SequenceAnalysisPipeline>(_ => new SequenceAnalysisPipeline(modelsDir));
builder.Services.AddSingleton<PositionalanalysisPipeline>(_ => new PositionalanalysisPipeline(modelsDir));

// Register all as IAnalysisPipeline so they can be injected as IEnumerable<IAnalysisPipeline>.
builder.Services.AddSingleton<IAnalysisPipeline>(sp => sp.GetRequiredService<FrequencyAnalysisPipeline>());
builder.Services.AddSingleton<IAnalysisPipeline>(sp => sp.GetRequiredService<SequenceAnalysisPipeline>());
builder.Services.AddSingleton<IAnalysisPipeline>(sp => sp.GetRequiredService<PositionalanalysisPipeline>());

// Ensemble wraps all pipelines.
builder.Services.AddSingleton<IEnsembleEngine>(sp =>
    new EnsembleEngine(sp.GetRequiredService<IEnumerable<IAnalysisPipeline>>()));

// Pipeline trainer (scoped because it uses scoped ITrainingMetricsRepository indirectly).
builder.Services.AddScoped<PipelineTrainer>();

// ── Background Service 
builder.Services.AddSingleton<LearningBackgroundService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<LearningBackgroundService>());


//Automated Data Harversters 
builder.Services.AddHttpClient<IDataHarvester, HistoryResultsHarvester>(client => {
    client.BaseAddress = new Uri("https://www.lotteryresults.co.za");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; PromethaionHarvester/1.0)");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHostedService<HarvestBackgroundService>();

// ── SignalR 
builder.Services.AddSignalR();

// ── API / Swagger 
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Promethaion API",
        Version = "v1",
        Description = "Self-aware pattern recognition engine for historical data analysis. " +
                      "Combines FastForest, SDCA, and LightGBM pipelines in a weighted ensemble."
    });
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});

builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));



var app = builder.Build();

// Run EF Core migrations on startup.
//await app.ApplySafeMigrationsAsync();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PAionDbContext>();

    try
    {
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider
            .GetRequiredService<ILogger<Program>>();

        logger.LogError(ex, "Database migration failed.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Promethaion v1"));
}

app.UseHttpsRedirection();
app.UseCors();

app.UseAuthorization();

app.MapControllers();
app.MapHub<LearningHub>("/hubs/training");

app.Run();