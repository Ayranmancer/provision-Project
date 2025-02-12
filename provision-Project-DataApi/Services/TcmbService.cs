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

            while (!stoppingToken.IsCancellationRequested)
            {
                await FetchAndUpdateExchangeRates(dbContext);
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
    }

    public async Task<List<ExchangeRate>> GetExchangeRatesInitial(DateTime date)
    {
        var cacheKey = $"{CACHE_KEY_PREFIX}{date:yyyyMMdd}";
        var db = _redis.GetDatabase();

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var rates = await dbContext.ExchangeRates
            .Where(r => r.Date.Date == date.Date)
            .ToListAsync();

        if (rates.Any())
        {
            Console.WriteLine($"Found rates in database for date: {date:yyyy-MM-dd}");
            return rates;
        }

        Console.WriteLine($"Fetching rates from TCMB for date: {date:yyyy-MM-dd}");
        return await GetExchangeRatesAsync(date);
    }

    public async Task<List<ExchangeRate>> GetExchangeRatesForDate(DateTime date, string currencyCode)
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
            await CacheExchangeRates(rates, date, currencyCode);
            return rates;
        }

        Console.WriteLine($"Fetching rates from TCMB for date: {date:yyyy-MM-dd}");
        return await GetExchangeRatesAsync(date);
    }

    private async Task CacheExchangeRates(List<ExchangeRate> rates, DateTime date, string currencyCode)
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


    private async Task<List<ExchangeRate>> GetExchangeRatesAsync(DateTime date)
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
            var endDate = DateTime.Today;
            var startDate = endDate.AddMonths(-2);

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var rates = await GetExchangeRatesAsync(date);
                if (rates != null && rates.Any())
                {
                    dbContext.ExchangeRates.AddRange(rates);
                    await dbContext.SaveChangesAsync();
                }
            }
            Console.WriteLine("Database initialization completed");
        }
    }

    private async Task FetchAndUpdateExchangeRates(ApplicationDbContext dbContext)
    {
        var today = DateTime.Today;

        if (IsWeekend(today) || IsHoliday(today))
        {
            Console.WriteLine($"Skipping update for {today:yyyy-MM-dd}: Weekend or Holiday");
            return;
        }

        var existingRate = await dbContext.ExchangeRates.AnyAsync(r => r.Date == today);
        if (!existingRate)
        {
            var rates = await GetExchangeRatesAsync(today);
            if (rates != null && rates.Any())
            {
                dbContext.ExchangeRates.AddRange(rates);
                await dbContext.SaveChangesAsync();
                Console.WriteLine($"Updated rates for {today:yyyy-MM-dd}");
            }
        }
    }

    public bool IsWeekend(DateTime date) =>
        date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;

    public bool IsHoliday(DateTime date)
    {
        var holidays = new List<DateTime>
        {
            new DateTime(date.Year, 1, 1),
            new DateTime(date.Year, 4, 23),
            new DateTime(date.Year, 5, 1),
            new DateTime(date.Year, 5, 19),
            new DateTime(date.Year, 7, 15),
            new DateTime(date.Year, 8, 30),
            new DateTime(date.Year, 10, 29)
        };

        return holidays.Contains(date);
    }
}
