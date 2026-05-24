using Riok.Mapperly.Abstractions;
using Aws.Models.Dto;

namespace Aws.Services.Mappers
{
    [Mapper]
    public partial class ItemMapper
    {
        [MapperIgnoreTarget(nameof(ItemDto.Id))]
        public partial ItemDto ToItemDto(ItemCreateDto source);
    }
}
