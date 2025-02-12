public class ExchangeRateService
{
    private readonly HttpClient _httpClient;

    public ExchangeRateService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<ExchangeRate>> GetExchangeRates(string currencyCode)
    {
        var response = await _httpClient.GetAsync($"http://localhost:5000/api/DataApi/{currencyCode}");

        if (!response.IsSuccessStatusCode) return null;

        var data = await response.Content.ReadFromJsonAsync<List<ExchangeRate>>();
        return data;
    }
}
