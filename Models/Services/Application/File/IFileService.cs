using Microsoft.AspNetCore.WebUtilities;
using ShareBucket.DataAccessLayer.Models.Entities;

namespace StorageMicroService.Models.Services.Application.Storage
{
    public interface IFileService
    {
        public Task<bool> UploadFileAsync(MultipartSection section, int idMemoryArea, string filepath);
        public Task<bool> DownloadFileAsync(string filename, Stream body, int idMemoryArea);
        public void DeleteFileIfCreated(string filename, string? filepath = null);
        public bool HasMemoryAreaAccess(int idMemoryArea, string token);
        public bool CreateMetadataOnDb(Metadata metadata);
        public bool VerifyMemoryAreaAccess(int idMemoryArea, User user);
        public bool DeleteFileAsync(string fileName, string filePath, int idMemoryArea);
        public bool DoesMetadataExists(string filepath, string fileName, int idMemoryArea);
    }
}
