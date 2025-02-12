public class ExchangeRate
{
    public int Id { get; set; }
    public required string CurrencyCode { get; set; }
    public required string CurrencyName { get; set; }
    public decimal ForexBuying { get; set; }
    public decimal ForexSelling { get; set; }
    public DateTime Date { get; set; }
}