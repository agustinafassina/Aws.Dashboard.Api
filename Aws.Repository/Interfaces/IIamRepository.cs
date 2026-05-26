using Aws.Models.Dto;

namespace Aws.Repository.Interfaces;

public interface IIamRepository
{
    Task<IamAccessKeysResponseDto> GetAccessKeysAsync(CancellationToken cancellationToken = default);
}