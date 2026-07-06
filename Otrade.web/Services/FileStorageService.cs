using Microsoft.AspNetCore.Hosting;
using Otrade.Application.Common.Interfaces;

namespace Otrade.Web.Services;

public class FileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _environment;
     
    public FileStorageService(
        IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string> SaveAsync(IFormFile file, string folder)
    {
        if (file == null || file.Length == 0)
            throw new Exception("File is empty");

        var rootPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "App_Data");

        var uploadPath = Path.Combine(rootPath, folder);

        if (!Directory.Exists(uploadPath))
            Directory.CreateDirectory(uploadPath);

        var extension = Path.GetExtension(file.FileName);
        var fileName = $"{Guid.NewGuid()}{extension}";

        var fullPath = Path.Combine(uploadPath, fileName);

        using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream);

        return Path.Combine(folder, fileName).Replace("\\", "/");
    }
}