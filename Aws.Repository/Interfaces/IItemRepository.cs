using Aws.Models.Dto;

namespace Aws.Repository.Interfaces
{
    public interface IItemRepository
    {
        IEnumerable<ItemDto> GetAll();
        ItemDto? GetById(int id);
        ItemDto Add(ItemDto item);
    }
}
