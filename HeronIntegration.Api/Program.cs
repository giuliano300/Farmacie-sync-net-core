using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<IMongoClient>(
    _ => new MongoClient(builder.Configuration["Mongo:ConnectionString"])
);

var app = builder.Build();

builder.Services.AddControllers();
app.MapControllers(); 

app.Run();
