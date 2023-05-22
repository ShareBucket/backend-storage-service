using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using StorageMicroService.Models.Services.Application.Storage;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Mime;
using static System.Collections.Specialized.BitVector32;
using System.Reflection.PortableExecutable;
using ShareBucket.JwtMiddlewareClient.Attributes;

namespace StorageMicroService.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class StorageController : Controller
    {
        private readonly IStreamFileService _streamFileService;

        public StorageController(IStreamFileService streamFileUploadService)
        {
            _streamFileService = streamFileUploadService;
        }

        //[HttpPost]
        //public IActionResult UploadFile()
        //{
        //    var file = Request.Form.Files[0];
        //    var result = _storageService.UploadFileAndEncrypt(file);
        //    return Ok(result);
        //}

        [Route("UploadFile")]
        [HttpPost]
        [RequestSizeLimit(100_000_000_000)]
        public async Task<IActionResult> UploadFile()
        {
            int bufferSize = 1024 * 1024; // 1MB
            var boundary = HeaderUtilities.RemoveQuotes(
                MediaTypeHeaderValue.Parse(Request.ContentType).Boundary
            ).Value;
            var reader = new MultipartReader(boundary, Request.Body, bufferSize);
            var section = await reader.ReadNextSectionAsync();
            string response = string.Empty;
            try
            {
                if (section != null && await _streamFileService.UploadFileAsync(reader, section))
                {
                    ViewBag.Message = "File Upload Successful";
                }
                else
                {
                    ViewBag.Message = "File Upload Failed";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                ViewBag.Message = "File Upload Failed";
            }
            return Ok(ViewBag.Message);
        }
        [Route("DownloadFile")]
        [HttpPost]
        [RequestSizeLimit(100_000_000_000)]
        public async Task<IActionResult> DownloadFile(string filename)
        {
            string mimeType = "application/octet-stream";

            var contentType = new MediaTypeHeaderValue(mimeType); 
            var contentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = filename
            };

            Response.Clear();
            Response.Headers.Add("Content-Type", "application/octet-stream");
            Response.Headers.Add("Content-Disposition", "attachment;filename=" + filename);
            
            try
            {
                if (await _streamFileService.DownloadFileAsync(filename, Response.Body))
                {
                    // Download was successful
                }
                else
                {
                    // Download failed
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                
            }

            return new EmptyResult();

        }
    }
}
