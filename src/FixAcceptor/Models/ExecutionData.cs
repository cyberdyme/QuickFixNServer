namespace FixAcceptor.Models;

public class ExecutionData
{
    public string ClOrdID { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public decimal OrderQty { get; set; }
    public decimal? Price { get; set; }
}
