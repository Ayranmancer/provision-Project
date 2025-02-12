using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

[ApiController]
[Route("api/BusinessApi")]
public class ExchangeRatesController : ControllerBase
{
    private readonly ExchangeRateService _exchangeRateService;

    public ExchangeRatesController(ExchangeRateService exchangeRateService)
    {
        _exchangeRateService = exchangeRateService;
    }

    [HttpGet("{currencyCode}")]
    public async Task<IActionResult> GetExchangeRates(string currencyCode)
    {
        var exchangeRates = await _exchangeRateService.GetExchangeRates(currencyCode);

        if (exchangeRates == null || exchangeRates.Count == 0)
        {
            return NotFound(new { message = "No exchange rates found for the given currency." });
        }

        return Ok(exchangeRates);
    }
}
