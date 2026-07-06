
using Microsoft.AspNetCore.Http;
namespace Otrade.Application.DTOs.Kyc;

public class KycUploadRequest
{
    public IFormFile? NationalIdImage { get; set; }

    public IFormFile? SelfieImage { get; set; }
}