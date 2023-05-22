using Microsoft.AspNetCore.WebUtilities;

namespace StorageMicroService.Models.Services.Application.Storage
{
    public interface IStreamFileService
    {
        Task<bool> UploadFileAsync(MultipartReader reader, MultipartSection section);
        Task<bool> DownloadFileAsync(string filename, Stream body);
    }
}
