using System.Text.Json;
using System.Text.Json.Serialization;
using CobolAnalyzer.Engine.Cfg;
using CobolAnalyzer.Engine.Comment;
using CobolAnalyzer.Engine.Dfg;
using CobolAnalyzer.Engine.Metrics;
using CobolAnalyzer.Engine.Metrics.Calculators;
using CobolAnalyzer.Parser;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// 開発環境のみ全オリジン許可
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("DevCors", policy =>
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
    });
}

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        opts.JsonSerializerOptions.MaxDepth = 128;
        opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<CobolParserFacade>();
builder.Services.Configure<MdiWeights>(builder.Configuration.GetSection("MdiWeights"));
builder.Services.AddSingleton<CfgBuilder>();
builder.Services.AddSingleton<DfgBuilder>();
builder.Services.AddSingleton<CommentInserter>();
builder.Services.AddSingleton<CommentRemover>();
builder.Services.AddSingleton<MdiCalculator>(sp =>
    new MdiCalculator(sp.GetRequiredService<IOptions<MdiWeights>>().Value));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

if (app.Environment.IsDevelopment())
{
    app.UseCors("DevCors");
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
