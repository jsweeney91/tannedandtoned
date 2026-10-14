using System.Text.Json;
using tanningTribe.Models;

namespace tanningTribe.Services;

public class CartService(IHttpContextAccessor httpContextAccessor)
{
    private const string CartKey = "Cart";

    private ISession Session => httpContextAccessor.HttpContext?.Session
        ?? throw new InvalidOperationException("Session is not available.");

    public List<CartItem> GetItems()
    {
        var json = Session.GetString(CartKey);
        return string.IsNullOrEmpty(json)
            ? []
            : JsonSerializer.Deserialize<List<CartItem>>(json) ?? [];
    }

    public int Count() => GetItems().Sum(i => i.Quantity);

    public decimal Total() => GetItems().Sum(i => i.LineTotal);

    public void Add(Product product, int quantity = 1)
    {
        var items = GetItems();
        var existing = items.FirstOrDefault(i => i.ProductId == product.Id);
        if (existing is null)
        {
            items.Add(new CartItem
            {
                ProductId = product.Id,
                Name = product.Name,
                ImageUrl = product.ImageUrl,
                UnitPrice = product.Price,
                Quantity = quantity
            });
        }
        else
        {
            existing.Quantity += quantity;
        }
        Save(items);
    }

    public void UpdateQuantity(int productId, int quantity)
    {
        var items = GetItems();
        var item = items.FirstOrDefault(i => i.ProductId == productId);
        if (item is null)
        {
            return;
        }

        if (quantity <= 0)
        {
            items.Remove(item);
        }
        else
        {
            item.Quantity = quantity;
        }
        Save(items);
    }

    public void Remove(int productId)
    {
        var items = GetItems();
        items.RemoveAll(i => i.ProductId == productId);
        Save(items);
    }

    public void Clear() => Session.Remove(CartKey);

    private void Save(List<CartItem> items) =>
        Session.SetString(CartKey, JsonSerializer.Serialize(items));
}
