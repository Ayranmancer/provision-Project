using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using provision_Project.Data;
using provision_Project.Data.Models;

[ApiController]
[Route("api/[controller]")]
public class ExchangeRateFetchController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ExchangeRateFetchController(ApplicationDbContext context)
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