using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using ShareBucket.DataAccessLayer.Data;
using ShareBucket.DataAccessLayer.Models.Entities;
using StorageMicroService.Models.Services.Application.Storage;
using StorageMicroService.Models.Services.Infrastructure;
using System.Diagnostics;
using System.Net.Mime;
using System.Text;
using static System.Collections.Specialized.BitVector32;

public class FileService : IFileService
{
    private readonly DataContext _context;
    public FileService(DataContext dataContext)
    {
        _context = dataContext;
    }

    public bool CreateMetadataOnDb(Metadata metadata)
    {
        try
        {
            _context.Metadatas.Add(metadata);
            _context.SaveChanges();
            return true;
        }
        catch (Exception e)
        {
            Debug.WriteLine(e.Message);
            return false;
        }
    }
    
    public async Task<bool> UploadFileAsync(MultipartSection section, int idMemoryArea, string filepath)
    {
        var hasContentDispositionHeader = ContentDispositionHeaderValue.TryParse(
            section.ContentDisposition, out var contentDisposition
        );
        if (hasContentDispositionHeader && contentDisposition != null)
        {
            if (contentDisposition.DispositionType.Equals("form-data") &&
            (!string.IsNullOrEmpty(contentDisposition.FileName.Value) ||
            !string.IsNullOrEmpty(contentDisposition.FileNameStar.Value)))
            {
                long fileDimension = section.Body.Length;
                
                string filePath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, $"UploadedFiles/{idMemoryArea}", filepath));

                if (!Directory.Exists(filePath))
                {
                    Directory.CreateDirectory(filePath);
                }
                    
                using (var fsIn = new FileStream(Path.Combine(filePath, contentDisposition.FileName.Value + ".enc"), FileMode.Create))
                {
                    // time diagnostic
                    var watch = Stopwatch.StartNew();
                    Debug.WriteLine("Start encryption");

                    var key = getKeyFromMemoryArea(idMemoryArea);

                    var salt = AesEncryptionService.RandomByteArray(16);
                    await fsIn.WriteAsync(salt, 0, salt.Length);
                    try
                    {
                        await AesEncryptionService.EncryptStreamAsync(section.Body, fsIn, key, salt);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                        return false;
                    }

                    // time diagnostic
                    watch.Stop();
                    var elapsedMs = watch.ElapsedMilliseconds;
                    var elapsedDateTime = watch.Elapsed;
                    Debug.WriteLine($"Elapsed time: {elapsedMs} ms");
                    Debug.WriteLine($"Elapsed time: {elapsedDateTime}");
                }
            }
        }
        return true;
    }

    private byte[] getKeyFromMemoryArea(int idMemoryArea)
    {
        var memoryArea = _context.MemoryAreas.Find(idMemoryArea);
        return memoryArea!.EncryptionKey;
    }

    public async Task<bool> DownloadFileAsync(string filepath, Stream body, int idMemoryArea)
    {

        string filePath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, $"UploadedFiles/{idMemoryArea}"));

        if (!Directory.Exists(filePath))
        {
            Debug.WriteLine("Directory not found");
            return false;
        }
        
        using (var fsIn = new FileStream(Path.Combine(filePath, filepath + ".enc"), FileMode.Open, FileAccess.Read))
        {

            var key = getKeyFromMemoryArea(idMemoryArea);
            
            byte[] salt = new byte[16];
            await fsIn.ReadAsync(salt, 0, salt.Length);
            
            // time diagnostic
            var watch = Stopwatch.StartNew();
            Debug.WriteLine("Start decryption");

            try
            {
                await AesEncryptionService.DecryptStreamAsync(fsIn, body, key, salt);
                body.Position = 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return false;


            }

            // time diagnostic
            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            var elapsedDateTime = watch.Elapsed;
            Debug.WriteLine($"Elapsed time: {elapsedMs} ms");
            Debug.WriteLine($"Elapsed time: {elapsedDateTime}");

        }

        return true;
    }
    public bool DeleteFileAsync(string fileName, string filePath, int idMemoryArea)
    {
        string localFilePath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, $"UploadedFiles/{idMemoryArea}", filePath, fileName + ".enc"));

        try
        {
            if (!filePath.EndsWith("/")) filePath += "/";
            var metadata = _context.Metadatas.FirstOrDefault(x => x.Path == filePath && x.Filename == fileName && x.MemoryAreaId == idMemoryArea);
            if (metadata != null)
            {
                File.Delete(localFilePath);
                _context.Metadatas.Remove(metadata);
                _context.SaveChanges();
                return true;
            }
            else
            {
                throw new Exception("Metadata not found");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            return false;
        }
    }
    public void DeleteFileIfCreated(string filename, string? filepath = null)
    {
        // Delete file if created
        string filePath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "UploadedFiles", filepath ?? string.Empty));
        if (File.Exists(Path.Combine(filePath, filename + ".enc")))
        {
            File.Delete(Path.Combine(filePath, filename + ".enc"));
        }
    }


    public bool HasMemoryAreaAccess(int idMemoryArea, string token)
    {
        // test if user has access to id memory area
        
        int userId = JwtDecoder.GetIdFromToken(token);

        if(_context.MemoryAreas.Single((mem) => mem.Id == idMemoryArea).Users.Single((usr) => usr.Id == usr.Id) != null)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public bool VerifyMemoryAreaAccess(int idMemoryArea, User user)
    {
        try
        {
            var userEntry = _context.Entry(user);
            userEntry.Collection(u => u.MemoryAreasPartecipated).Load();
            return user.MemoryAreasPartecipated.SingleOrDefault((mem) => mem.Id == idMemoryArea) != null;
        }
        catch(Exception ex)
        {
            return false;
        }
    }

    public bool DoesMetadataExists(string filePath, string fileName, int idMemoryArea)
    {
        if (!filePath.EndsWith("/")) filePath += "/";
        return _context.Metadatas.FirstOrDefault(x => x.Path == filePath && x.Filename == fileName && x.MemoryAreaId == idMemoryArea) != null;
    }
}