using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class ExchangeRatesController : ControllerBase
{
    private readonly TcmbService _tcmbService;
    private readonly ApplicationDbContext _context;

    public ExchangeRatesController(TcmbService tcmbService, ApplicationDbContext context)
    {
        _tcmbService = tcmbService;
        _context = context;
    }

    [HttpGet("fetch")]
    public async Task<IActionResult> FetchExchangeRates()
    {
        var endDate = DateTime.Today;
        var startDate = endDate.AddMonths(-2);

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var rates = await _tcmbService.GetExchangeRatesAsync(date);
            if (rates != null)
            {
                _context.ExchangeRates.AddRange(rates);
                await _context.SaveChangesAsync();
            }
        }

        return Ok("Exchange rates fetched and saved successfully.");
    }

    [HttpGet("{currencyCode}")]
    [Produces("application/json", "application/xml")] // Allow both JSON and XML
    public async Task<IActionResult> GetExchangeRates(string currencyCode)
    {
        var rates = await _context.ExchangeRates
            .Where(r => r.CurrencyCode == currencyCode)
            .ToListAsync();

        if (!rates.Any())
        {
            return NotFound(new { Message = $"No exchange rates found for currency: {currencyCode}" });
        }

        return Ok(rates); // ASP.NET Core will return XML if the client requests it
    }


}