using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class TcmbService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HttpClient _httpClient;

    public TcmbService(IServiceScopeFactory scopeFactory, HttpClient httpClient)
    {
        _scopeFactory = scopeFactory;
        _httpClient = httpClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Step 1: Initial Data Fetch on Startup
            await InitializeDatabaseIfEmpty(dbContext);

            // Step 2: Start Daily Update Loop
            while (!stoppingToken.IsCancellationRequested)
            {
                await FetchAndUpdateExchangeRates(dbContext);
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken); // Run every 24 hours
            }
        }
    }

    public async Task<List<ExchangeRate>> GetExchangeRatesAsync(DateTime date)
    {
        var url = $"https://www.tcmb.gov.tr/kurlar/{date:yyyyMM}/{date:ddMMyyyy}.xml";
        var response = await _httpClient.GetAsync(url);
        Console.WriteLine($"Fetching exchange rates for {date:dd/MM/yyyy}...");

        if (!response.IsSuccessStatusCode)
            return null;

        var xml = await response.Content.ReadAsStringAsync();
        var doc = XDocument.Parse(xml);
        Console.WriteLine($"Exchange rates fetched for {date:dd/MM/yyyy}.");

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
        if (!await dbContext.ExchangeRates.AnyAsync()) // Check if database is empty
        {
            Console.WriteLine("No exchange rate data found. Fetching initial data...");
            var endDate = DateTime.Today;
            var startDate = endDate.AddMonths(-2);

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var rates = await GetExchangeRatesAsync(date);
                if (rates != null)
                {
                    dbContext.ExchangeRates.AddRange(rates);
                    await dbContext.SaveChangesAsync();
                }
            }
            Console.WriteLine("Initial exchange rate data fetched successfully.");
        }
    }

    private async Task FetchAndUpdateExchangeRates(ApplicationDbContext dbContext)
    {
        var today = DateTime.Today;

        if (IsWeekend(today) || IsHoliday(today))
        {
            Console.WriteLine($"Skipping update for {today}: Weekend or Holiday.");
            return;
        }

        var existingRate = await dbContext.ExchangeRates.AnyAsync(r => r.Date == today);
        if (!existingRate)
        {
            var rates = await GetExchangeRatesAsync(today);
            if (rates != null)
            {
                dbContext.ExchangeRates.AddRange(rates);
                await dbContext.SaveChangesAsync();
                Console.WriteLine($"Exchange rates updated for {today}.");
            }
        }
    }

    private bool IsWeekend(DateTime date) =>
        date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;

    private bool IsHoliday(DateTime date)
    {
        var holidays = new List<DateTime>
        {
            new DateTime(date.Year, 1, 1),  // New Year’s Day
            new DateTime(date.Year, 4, 23), // National Sovereignty and Children's Day (Turkey)
            new DateTime(date.Year, 5, 1),  // Labor Day
            new DateTime(date.Year, 5, 19), // Atatürk Memorial Day
            new DateTime(date.Year, 7, 15), // Democracy and National Unity Day
            new DateTime(date.Year, 8, 30), // Victory Day
            new DateTime(date.Year, 10, 29) // Republic Day
        };

        return holidays.Contains(date);
    }
}
