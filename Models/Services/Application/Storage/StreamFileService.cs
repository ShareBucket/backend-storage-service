using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using StorageMicroService.Models.Services.Application.Storage;
using System.Diagnostics;
using System.Net.Mime;
using static System.Collections.Specialized.BitVector32;

public class StreamFileService : IStreamFileService
{
    public async Task<bool> UploadFileAsync(MultipartReader multipartReader, MultipartSection? section)
    {
        while (section != null)
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

                    string filePath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "UploadedFiles"));

                    using (var fsIn = new FileStream(Path.Combine(filePath, contentDisposition.FileName.Value + ".enc"), FileMode.Create))
                    {
                        // time diagnostic
                        var watch = Stopwatch.StartNew();
                        Debug.WriteLine("Start encryption");

                        var salt = AesEncryptionService.RandomByteArray(16);
                        await fsIn.WriteAsync(salt, 0, salt.Length);
                        try
                        {
                            await AesEncryptionService.EncryptStreamAsync(section.Body, fsIn, new byte[] { 5, 4, 3, 2, 1, 2, 1, 6, 3, 6 }, salt);
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
            section = await multipartReader.ReadNextSectionAsync();
        }
        return true;
    }
    public async Task<bool> DownloadFileAsync(string filepath, Stream body)
    {
        
        string filePath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "UploadedFiles"));

        byte[] salt = new byte[16];
        
        using (var fsIn = new FileStream(Path.Combine(filePath, filepath + ".enc"), FileMode.Open))
        {
            await fsIn.ReadAsync(salt, 0, salt.Length);

            // time diagnostic
            var watch = Stopwatch.StartNew();
            Debug.WriteLine("Start decryption");

            try
            {
                await AesEncryptionService.DecryptStreamAsync(fsIn, body, new byte[] { 5, 4, 3, 2, 1, 2, 1, 6, 3, 6 }, salt);
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
}