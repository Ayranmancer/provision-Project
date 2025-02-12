public class TcmbService
{
    private readonly HttpClient _httpClient;

    public TcmbService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<ExchangeRate>> GetExchangeRates(string currency)
    {
        var response = await _httpClient.GetAsync($"http://localhost:5000/api/ExchangeRates/{currency}");

        if (!response.IsSuccessStatusCode) return null;

        var data = await response.Content.ReadFromJsonAsync<List<ExchangeRate>>();
        return data;
    }
}
