using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/DataApi")]
public class DataApiController : ControllerBase
{
    private readonly TcmbService _tcmbService;
    private readonly ApplicationDbContext _context;

    public DataApiController(TcmbService tcmbService, ApplicationDbContext context)
    {
        _tcmbService = tcmbService;
        _context = context;
    }

    [HttpGet("fetch")]
    public async Task<IActionResult> FetchExchangeRates()
    {
        try
        {
            var endDate = DateTime.Today;
            var startDate = endDate.AddMonths(-2);

            var fetchedDates = new List<DateTime>();
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var rates = await _tcmbService.GetExchangeRatesForDate(date);
                if (rates != null && rates.Any())
                {
                    fetchedDates.Add(date);
                }
            }

            return Ok(new
            {
                Message = "Exchange rates fetched and saved successfully.",
                FetchedDates = fetchedDates
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "An error occurred while fetching exchange rates.", Error = ex.Message });
        }
    }

    [HttpGet("{currencyCode}")]
    [Produces("application/json", "application/xml")]
    public async Task<IActionResult> GetExchangeRates(string currencyCode)
    {
        try
        {
            // Get all rates for the currency
            var rates = await _context.ExchangeRates
                .Where(r => r.CurrencyCode.ToUpper() == currencyCode.ToUpper().Trim())
                .OrderByDescending(r => r.Date)
                .ToListAsync();

            if (!rates.Any())
            {
                return NotFound(new { Message = $"No exchange rates found for currency: {currencyCode}" });
            }

            return Ok(rates);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "An error occurred while retrieving exchange rates.", Error = ex.Message });
        }
    }
}