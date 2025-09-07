using System.Collections.Concurrent;

namespace InventoryService.Data;

public class InMemoryInventory
{
    // Fake stock levels
    private readonly ConcurrentDictionary<string, int> _stock = new(new[]
    {
        new KeyValuePair<string,int>("SKU-1", 10),
        new KeyValuePair<string,int>("SKU-2", 0),
        new KeyValuePair<string,int>("SKU-3", 25)
    });

    public int GetAvailable(string sku) => _stock.TryGetValue(sku, out var qty) ? qty : 0;
}
