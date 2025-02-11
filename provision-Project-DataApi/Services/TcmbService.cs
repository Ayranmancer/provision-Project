using System.Xml.Linq;
using System.Net.Http;

public class TcmbService
{
    private readonly HttpClient _httpClient;

    public TcmbService(HttpClient httpClient)
    {
        _httpClient = httpClient;
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
                //ForexSelling = decimal.Parse(x.Element("ForexSelling")?.Value ?? "0"),
                Date = date
            }).ToList();

        return rates;
    }
}