using Aws.Models.Dto;

namespace Aws.Services.Interfaces
{
    public interface IItemService
    {
        IEnumerable<ItemDto> GetAllItems();
        ItemDto? GetItemById(int id);
        ItemDto CreateItem(ItemCreateDto newItem);
    }
}
