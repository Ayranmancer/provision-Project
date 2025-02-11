using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class ExchangeRatesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ExchangeRatesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetExchangeRates()
    {
        var rates = await _context.ExchangeRates.ToListAsync();
        return Ok(rates);
    }
}