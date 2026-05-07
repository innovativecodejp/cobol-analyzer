using System.Text.Json.Serialization;
using CobolAnalyzer.Engine.Cfg;
using CobolAnalyzer.Engine.Dfg;
using CobolAnalyzer.Engine.Metrics;
using CobolAnalyzer.Engine.Metrics.Calculators;
using CobolAnalyzer.Parser;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.MaxDepth = 256;
        opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<CobolParserFacade>();
builder.Services.Configure<MdiWeights>(builder.Configuration.GetSection("MdiWeights"));
builder.Services.AddSingleton<CfgBuilder>();
builder.Services.AddSingleton<DfgBuilder>();
builder.Services.AddSingleton<MdiCalculator>(sp =>
    new MdiCalculator(sp.GetRequiredService<IOptions<MdiWeights>>().Value));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
