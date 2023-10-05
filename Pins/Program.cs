using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pins.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("corspolicy",
        build =>
        {
            build.WithOrigins("http://localhost:3000").AllowAnyMethod().AllowAnyHeader();
        });
});

var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApiDbContext>(options => options.UseSqlite(connStr));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("corspolicy");

// Helper Functions
int GeneratePin()
{
    Random random = new Random();
    int pin;
    // Generate random number while checking if its not obvious
    do
    {
        pin = random.Next(1000, 10000);
    } while (IsObvious(pin));

    return pin;
}

//Check if the pin has repetitive digits
static bool IsObvious(int pin)
{
    string pinStr = pin.ToString();
    char firstChar = pinStr[0];

    for (int i = 1; i < pinStr.Length; i++)
    {
        if (pinStr[i] != firstChar)
        {
            return false;
        }
    }
    return true;
}


//ENDPOINTS
app.MapPost("/pincount", async (CountDto count, ApiDbContext db, HttpContext ctx) =>
{
    // Validate input
    if (count.Count <= 0)
    {
        return Results.BadRequest("Invalid Count Specified");
    }
    
    // Store pins in an array
    List<PinResponseDto> pinResponses = new List<PinResponseDto>();
    
    // Create pins
    for (int i = 0; i < count.Count; i++)
    {
        var pinGen = GeneratePin();
        var pinExists = db.Pins.FirstOrDefault(p => p.Pin == pinGen);
        
        // used to track how long the while loop runs to avoid an infinite loop
        int maxAttempts = 10;
        int attempt = 0;
        
        // Regenerate pin/pins if they exist
        while (pinExists != null && attempt < maxAttempts)
        {
            pinGen = GeneratePin();
            pinExists = db.Pins.FirstOrDefault(p => p.Pin == pinGen);
            attempt++;
        }
        
        // Create object for saving to database
        var pins = new PinManagement()
        {
            Pin = pinGen
        };
            
        db.Pins.Add(pins);
        db.SaveChanges();
            
        pinResponses.Add(new PinResponseDto()
        {
            Pin = pins.Pin
        });
    }
    
    // Save changes to the database
    try
    {
        await db.SaveChangesAsync();
    }
    catch(Exception ex)
    {
        Console.WriteLine($"Error: {ex}");
        return Results.StatusCode(500);
    }
    
    return Results.Ok(pinResponses);
});

app.MapGet("/pins", async (ApiDbContext db, HttpContext ctx) =>
{
    // Fetch all pins from db
    var pins = await db.Pins.ToListAsync();

    return Results.Ok(pins);
});


app.Run();

class ApiDbContext : DbContext
{
    public virtual DbSet<PinManagement> Pins { get; set; }

    public ApiDbContext(DbContextOptions<ApiDbContext> options) : base(options)
    { }
}
