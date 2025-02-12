using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

[Route("api/exchangeRates")]
[ApiController]
public class ExchangeRatesController : ControllerBase
{
    private readonly ExchangeRateService _exchangeRateService;

    public ExchangeRatesController(ExchangeRateService exchangeRateService)
    {
        _exchangeRateService = exchangeRateService;
    }

    [HttpGet("{currency}")]
    public async Task<IActionResult> GetExchangeRates(string currency)
    {
        var exchangeRates = await _exchangeRateService.GetExchangeRates(currency);

        if (exchangeRates == null || exchangeRates.Count == 0)
        {
            return NotFound(new { message = "No exchange rates found for the given currency." });
        }

        return Ok(exchangeRates);
    }
}
