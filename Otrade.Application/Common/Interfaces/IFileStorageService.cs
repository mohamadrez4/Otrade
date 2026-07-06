using Microsoft.AspNetCore.Http;

namespace Otrade.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveAsync(
        IFormFile file,
        string folder);
}