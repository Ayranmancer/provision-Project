using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using static System.Runtime.InteropServices.JavaScript.JSType;

public class TcmbService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HttpClient _httpClient;
    private readonly IConnectionMultiplexer _redis;
    private const string CACHE_KEY_PREFIX = "exchange_rate:";
    private const int CACHE_HOURS = 24;

    public TcmbService(
        IServiceScopeFactory scopeFactory,
        HttpClient httpClient,
        IConnectionMultiplexer redis)
    {
        _scopeFactory = scopeFactory;
        _httpClient = httpClient;
        _redis = redis;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await InitializeDatabaseIfEmpty(dbContext);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var nextRunTime = GetNextRunTime(now);

            var delay = nextRunTime - now;
            Console.WriteLine($"Next update scheduled at: {nextRunTime}");

            await Task.Delay(delay, stoppingToken);

            using (var updateScope = _scopeFactory.CreateScope()) // New scope for each update
            {
                var scopedDbContext = updateScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await FetchAndUpdateExchangeRatesAsync(scopedDbContext, now);
                await DeleteOutdatedEntries(scopedDbContext);
            }
        }
    }


    private DateTime GetNextRunTime(DateTime currentTime)
    {
        var nextRunTime = currentTime.Date.AddHours(15).AddMinutes(35); // Today at 15:30, give 5 minute window

        // If it's already past 15:30, schedule for the next valid day
        if (currentTime > nextRunTime || IsWeekend(currentTime) || IsHoliday(currentTime))
        {
            do
            {
                nextRunTime = nextRunTime.AddDays(1);
            }
            while (IsWeekend(nextRunTime) || IsHoliday(nextRunTime));

            nextRunTime = nextRunTime.Date.AddHours(15).AddMinutes(30);
        }

        return nextRunTime;
    }

    public async Task<List<ExchangeRate>> GetExchangeRatesForCurrencyAsync(DateTime date, string currencyCode)
    {
        var cacheKey = $"{CACHE_KEY_PREFIX}{date:yyyyMMdd}_{currencyCode}";
        var db = _redis.GetDatabase();

        var cachedValue = await db.StringGetAsync(cacheKey);
        if (cachedValue.HasValue)
        {
            Console.WriteLine($"Cache HIT for date: {date:yyyy-MM-dd} and currency: {currencyCode}");
            return JsonSerializer.Deserialize<List<ExchangeRate>>(cachedValue);
        }

        Console.WriteLine($"Cache MISS for date: {date:yyyy-MM-dd} and currency: {currencyCode}");

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var rates = await dbContext.ExchangeRates
            .Where(r => r.Date.Date == date.Date && r.CurrencyCode == currencyCode)
            .ToListAsync();

        if (rates.Any())
        {
            Console.WriteLine($"Found rates in database for date: {date:yyyy-MM-dd} and currency: {currencyCode}");
            await CacheExchangeRatesAsync(rates, date, currencyCode);
            return rates;
        }

        Console.WriteLine($"Fetching rates from TCMB for date: {date:yyyy-MM-dd}");
        await FetchAndUpdateExchangeRatesAsync(dbContext, date);
        return await FetchExchangeRatesAsync(date);
    }

    private async Task CacheExchangeRatesAsync(List<ExchangeRate> rates, DateTime date, string currencyCode)
    {
        if (rates == null || !rates.Any()) return;

        var cacheKey = $"{CACHE_KEY_PREFIX}{date:yyyyMMdd}_{currencyCode}";
        var db = _redis.GetDatabase();

        if (!await db.KeyExistsAsync(cacheKey))
        {
            var serializedRates = JsonSerializer.Serialize(rates);
            await db.StringSetAsync(cacheKey, serializedRates, TimeSpan.FromHours(CACHE_HOURS));
            Console.WriteLine($"Cached {rates.Count} rates for date: {date:yyyy-MM-dd} and currency: {currencyCode}");
        }
    }


    private async Task<List<ExchangeRate>> FetchExchangeRatesAsync(DateTime date)
    {
        var url = $"https://www.tcmb.gov.tr/kurlar/{date:yyyyMM}/{date:ddMMyyyy}.xml";
        var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to fetch rates from TCMB for date: {date:yyyy-MM-dd}");
            return null;
        }

        var xml = await response.Content.ReadAsStringAsync();
        var doc = XDocument.Parse(xml);
        Console.WriteLine($"Successfully fetched rates from TCMB for date: {date:yyyy-MM-dd}");

        var rates = doc.Descendants("Currency")
            .Select(x => new ExchangeRate
            {
                CurrencyCode = x.Attribute("CurrencyCode")?.Value,
                CurrencyName = x.Element("CurrencyName")?.Value,
                ForexBuying = decimal.TryParse(x.Element("ForexBuying")?.Value, out var forexBuying) ? forexBuying : 0,
                Date = date
            }).ToList();

        return rates;
    }

    private async Task InitializeDatabaseIfEmpty(ApplicationDbContext dbContext)
    {
        if (!await dbContext.ExchangeRates.AnyAsync())
        {
            Console.WriteLine("Initializing database with historical data");
            await FetchAndStoreExchangeRatesAsync(dbContext);
            Console.WriteLine("Database initialization completed");
        }
    }

    public async Task<string> FetchAndStoreExchangeRatesAsync(ApplicationDbContext dbContext)
    {
        var endDate = DateTime.Today;
        var startDate = endDate.AddMonths(-2);

        var fetchedDates = new List<DateTime>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            // Check if data for the given date already exists in the database
            var existingRates = await dbContext.ExchangeRates
                .Where(r => r.Date == date)
                .ToListAsync();

            if (existingRates.Any())
            {
                Console.WriteLine($"Exchange rates for {date:yyyy-MM-dd} already exist in the database.");
                continue;
            }

            var rates = await FetchExchangeRatesAsync(date);
            if (rates != null && rates.Any())
            {
                fetchedDates.Add(date);
                dbContext.ExchangeRates.AddRange(rates);
                await dbContext.SaveChangesAsync();
            }
        }

        if (fetchedDates.Any())
        {
            Console.WriteLine("Exchange rates fetched and saved successfully.");
            foreach (var fetchedDate in fetchedDates)
            {
                Console.WriteLine($"Fetched Date: {fetchedDate:yyyy-MM-dd}");
            }
            return "Exchange rates were successfully fetched and stored.";
        }

        return "Exchange rates could not be fetched.";
    }
    private async Task FetchAndUpdateExchangeRatesAsync(ApplicationDbContext dbContext, DateTime date)
    {
        if (IsWeekend(date) || IsHoliday(date))
        {
            Console.WriteLine($"Skipping update for {date:yyyy-MM-dd}: Weekend or Holiday");
            return;
        }

        var existingRate = await dbContext.ExchangeRates.AnyAsync(r => r.Date == date);
        if (!existingRate)
        {
            var rates = await FetchExchangeRatesAsync(date);
            if (rates != null && rates.Any())
            {
                dbContext.ExchangeRates.AddRange(rates);
                await dbContext.SaveChangesAsync();
                Console.WriteLine($"Updated rates for {date:yyyy-MM-dd}");
            }
        }
    }
    private async Task DeleteOutdatedEntries(ApplicationDbContext dbContext)
    {
        var oldestAllowedDate = DateTime.Today.AddMonths(-2);

        var outdatedEntries = await dbContext.ExchangeRates
            .Where(r => r.Date < oldestAllowedDate)
            .ToListAsync();

        if (outdatedEntries.Any())
        {
            dbContext.ExchangeRates.RemoveRange(outdatedEntries);
            await dbContext.SaveChangesAsync();
            Console.WriteLine($"Deleted {outdatedEntries.Count} outdated entries before {oldestAllowedDate:yyyy-MM-dd}");
        }
    }


    public bool IsWeekend(DateTime date) =>
        date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;

    public bool IsHoliday(DateTime date)
    {
        var holidays = new List<DateTime>
    {
        new DateTime(date.Year, 1, 1),  // New Year's Day
        new DateTime(date.Year, 4, 23), // National Sovereignty and Children's Day
        new DateTime(date.Year, 5, 1),  // Labor and Solidarity Day
        new DateTime(date.Year, 5, 19), // Commemoration of Atatürk, Youth and Sports Day
        new DateTime(date.Year, 7, 15), // Democracy and National Unity Day
        new DateTime(date.Year, 8, 30), // Victory Day
        new DateTime(date.Year, 10, 29) // Republic Day
    };

        return holidays.Contains(date);
    }
}
