using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class ExchangeRatesController : ControllerBase
{
    private readonly TcmbService _tcmbService;
    private readonly ExchangeRateContext _context;

    public ExchangeRatesController(TcmbService tcmbService, ExchangeRateContext context)
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

    [HttpGet]
    public async Task<IActionResult> GetExchangeRates()
    {
        var rates = await _context.ExchangeRates.ToListAsync();
        return Ok(rates);
    }
}