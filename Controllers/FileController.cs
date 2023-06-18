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
using Google.Protobuf.Reflection;
using System.Security.Principal;
using System.Text;
using ShareBucket.DataAccessLayer.Models.Entities;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace StorageMicroService.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class FileController : Controller
    {
        private readonly IFileService _fileService;

        public FileController(IFileService fileService)
        {
            _fileService = fileService;
        }

        [Route("UploadFile")]
        [HttpPost]
        [RequestSizeLimit(5_000_000_000)]
        public async Task<IActionResult> UploadFile()
        {
            // Read the memory area id from the request header
            Request.Headers.TryGetValue("idMemoryArea", out var idMemoryAreaStr);
            if (!int.TryParse(idMemoryAreaStr, out var idMemoryArea))
            {
                return BadRequest("idMemoryArea is not a number");
            }
            // Verify if the memoryArea exists and the user has access to it
            if (Request.HttpContext.Items["User"] is not User user)
            {
                return Unauthorized();
            }
            if (!_fileService.VerifyMemoryAreaAccess(idMemoryArea, user))
            {
                return Unauthorized();
            }

            // Read the Content Dispostion
            Request.Headers.TryGetValue("Content-Disposition", out var ContentDisposition);
            if (string.IsNullOrEmpty(ContentDisposition))
            {
                return BadRequest("Content-Disposition is null or empty");
            }

            int bufferSize = 1024 * 1024; // 1MB
            var boundary = HeaderUtilities.RemoveQuotes(
                MediaTypeHeaderValue.Parse(Request.ContentType).Boundary
            ).Value;
            var reader = new MultipartReader(boundary, Request.Body, bufferSize);
            var section = await reader.ReadNextSectionAsync();

            if(section == null)
            {
                return BadRequest("No file is found");
            }
            try
            {
                var fileName = section.AsFileSection().FileName;
                string regexPattern = @"filepath=([^;]+)";
                Match match = Regex.Match(ContentDisposition, regexPattern);
                string filepath; // Initialize filepath variable to null

                if (match.Success && match.Groups.Count > 1)
                {
                    filepath = match.Groups[1].Value;
                }
                else
                {
                    filepath = string.Empty;
                }
                if (!filepath.EndsWith('/'))
                {
                    filepath += '/';
                }
                if (filepath.EndsWith("\\"))
                {
                    filepath = filepath.Substring(0, filepath.Length - 2) + "/";
                }

                // Check if both filepath and fileName exist
                // And section is not null, and try to upload
                if (filepath is not null && 
                    !string.IsNullOrEmpty(fileName) &&
                    section != null && 
                    await _fileService.UploadFileAsync(section, idMemoryArea, filepath))
                {

                    // Create the metadata
                    var metadata = new Metadata
                    {
                        Filename = fileName,
                        Path = filepath,
                        FileExtension = Path.GetExtension(fileName),
                        DataCreation = DateTime.Now,
                        MemoryAreaId = idMemoryArea,
                    };

                    if (_fileService.CreateMetadataOnDb(metadata))
                    {
                        ViewBag.Message = "File Upload Successful";
                    }
                    else
                    {
                        ViewBag.Message = "File Upload Failed";
                        _fileService.DeleteFileIfCreated(fileName, filepath);
                    }
                    
                }
                else
                {
                    ViewBag.Message = "File Upload Failed";
                    _fileService.DeleteFileIfCreated(fileName, filepath);
                }
            }
            catch (Exception ex)
            {
                var fileName = section.AsFileSection().FileName;
                Debug.WriteLine(ex.Message);
                ViewBag.Message = "File Upload Failed";
                _fileService.DeleteFileIfCreated(fileName);
            }
            
            return Ok(ViewBag.Message);
        }
        [Route("DownloadFile")]
        [HttpPost]
        [RequestSizeLimit(5_000_000_000)]
        public async Task<IActionResult> DownloadFile([FromQuery] string filename, [FromQuery] string? filePath)
        {
            // Read the memory area id from the request header
            Request.Headers.TryGetValue("idMemoryArea", out var idMemoryAreaStr);
            if(!int.TryParse(idMemoryAreaStr, out var idMemoryArea))
            {
                return BadRequest("idMemoryArea is not a number");
            }
            if(filePath is null)
            {
                filePath = string.Empty;
            }
            if (!filePath.EndsWith('/'))
            {
                filePath += '/';
            }
            if (filePath.EndsWith("\\"))
            {
                filePath = filePath.Substring(0, filePath.Length - 2) + "/";
            }

            // Verify if the memoryArea exists and the user has access to it
            if (Request.HttpContext.Items["User"] is not User user)
            {
                return Unauthorized();
            }
            if (!_fileService.VerifyMemoryAreaAccess(idMemoryArea, user))
            {
                return Unauthorized();
            }

            string localFilePath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, $"UploadedFiles/{idMemoryArea}", filePath));

            if (!Directory.Exists(localFilePath))
            {
                Debug.WriteLine("Directory not found");
                return BadRequest("Directory not found");
            }
            string combinedPath = Path.Combine(localFilePath, filename);
            if (!System.IO.File.Exists(combinedPath + ".enc"))
            {
                Debug.WriteLine("File not found");
                return BadRequest("File not found");
            }

            Response.Clear();
            Response.Headers.Add("Content-Type", "application/octet-stream");
            Response.Headers.Add("Content-Disposition", "attachment;filename=" + filename);
            
            try
            {
                if (await _fileService.DownloadFileAsync(combinedPath, Response.Body, idMemoryArea))
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

        [Route("DeleteFile")]
        [HttpDelete]
        public async Task<ActionResult> DeleteFile([FromQuery] string fileName, [FromQuery] string? filePath)
        {
            // Read the memory area id from the request header
            Request.Headers.TryGetValue("idMemoryArea", out var idMemoryAreaStr);
            if (!int.TryParse(idMemoryAreaStr, out var idMemoryArea))
            {
                return BadRequest("idMemoryArea is not a number");
            }
            if (filePath is null)
            {
                filePath = string.Empty;
            }
            if (!filePath.EndsWith('/'))
            {
                filePath += '/';
            }
            if (filePath.EndsWith("\\"))
            {
                filePath = filePath.Substring(0, filePath.Length - 2) + "/";
            }
            
            // Verify if the memoryArea exists and the user has access to it
            if (Request.HttpContext.Items["User"] is not User user)
            {
                return Unauthorized();
            }
            if (!_fileService.VerifyMemoryAreaAccess(idMemoryArea, user))
            {
                return Unauthorized();
            }

            string localFilePath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, $"UploadedFiles/{idMemoryArea}", filePath));

            if (!Directory.Exists(localFilePath))
            {
                Debug.WriteLine("Directory not found");
                return BadRequest("Directory not found");
            }
            string combinedPath = Path.Combine(localFilePath, fileName);
            if (!System.IO.File.Exists(combinedPath + ".enc"))
            {
                Debug.WriteLine("File not found");
                return BadRequest("File not found");
            }

            try
            {
                if (_fileService.DeleteFileAsync(fileName, filePath, idMemoryArea))
                {
                    return Ok("Metadata Eliminato correttamente");
                }
                else
                {
                    return BadRequest("Errore durante l'eliminazione del file");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return BadRequest("Errore durante l'eliminazione del file");

            }

        }


    }
}
