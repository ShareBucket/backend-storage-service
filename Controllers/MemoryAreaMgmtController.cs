using Microsoft.AspNetCore.Mvc;
using ShareBucket.DataAccessLayer.Data;
using ShareBucket.JwtMiddlewareClient.Attributes;
using static Humanizer.In;
using System.IdentityModel.Tokens.Jwt;
using System.Diagnostics;
using ShareBucket.DataAccessLayer.Models.Entities;
using StorageMicroService.Models.Services.Infrastructure;
using StorageMicroService.Models.Dto.Responses;
using Microsoft.EntityFrameworkCore;
using StorageMicroService.Models.Services.Application.Storage;

namespace StorageMicroService.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class MemoryAreaMgmtController : Controller
    {
        private readonly DataContext _context;

        public MemoryAreaMgmtController(DataContext context)
        {
            _context = context;
            //_userId = int.Parse(JwtMiddlewareClient.JwtMiddleware.DecodeToken(userToken)["id"]);

        }
        // GET: MemoryAreaMgmtController
        // Get the list of memory areas
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            if (Request.HttpContext.Items["User"] is not User user)
            {
                return Unauthorized();
            }

            var userEntry = _context.Entry(user);
            await userEntry.Collection(u => u.MemoryAreasPartecipated).LoadAsync();

            
            return Ok(
                user.MemoryAreasPartecipated.Select(ma => new MemoryAreaDto()
                {
                    Id = ma.Id,
                    Name = ma.Name,
                    MaxGB = ma.MaxGB,
                    CreationDate = ma.CreationDate
                })
            );
        }
        // Return the memory area with the specified id and the list of files and folders inside it

        [HttpGet]
        [Route("GetContent")]
        public IActionResult Get(int idMemoryArea, string? filePath)
        {
            // Get the memory area with the given id and return the list of files/folders in that memory area
            // if filepath is provided, it also search for that folder

            // Test if user has access to the MemoryArea provided

            if (Request.HttpContext.Items["User"] is not User user ||
                !_context.MemoryAreas.Any(m => m.Id == idMemoryArea && m.Users.Contains(user)))
            {
                return Unauthorized();
            }
            if (filePath is null)
            {
                filePath = "";
            }
            if (!filePath.EndsWith('/'))
            {
                filePath += '/';
            }
            if (filePath.EndsWith("\\"))
            {
                filePath = filePath.Substring(0, filePath.Length - 2) + "/";
            }


            string localFilePath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, $"UploadedFiles/{idMemoryArea}"));

            localFilePath = (filePath == "/") ? localFilePath : Path.Combine(localFilePath, filePath);

            if (!Directory.Exists(localFilePath))
            {
                return NotFound();
            }

            // Get the memory area with the given id 
            var memoryArea = _context.MemoryAreas.Find(idMemoryArea);
            if (memoryArea == null)
            {
                return NotFound();
            }

            // Get the list of files and folders in the memory area
            var memoryAreaEntry = _context.Entry(memoryArea);
            memoryAreaEntry.Collection(m => m.Metadatas).Load();

            // Get the list of files and folders in the memory area
            // Filter it iterating it and verify if it is a folder based on the file extension
            var files = memoryArea.Metadatas.Where(m => m.Path == filePath).Select(m => new FileDto()
            {
                Id = m.Id,
                Name = m.Filename,
                Extension = m.FileExtension,
                DataCreation = m.DataCreation,
            });
            var folders = Directory.GetDirectories(localFilePath).Select(Path.GetFileName).ToList();

            // Return the list of files and folders in the memory area
            var result = new
            {
                files,
                folders,
                Path = filePath
            };
            return Ok(result);
        }

        [HttpDelete]
        [Route("DeleteFolder")]
        public IActionResult DeleteFolder(int idMemoryArea, string filePath)
        {
            if (Request.HttpContext.Items["User"] is not User user ||
                !_context.MemoryAreas.Any(m => m.Id == idMemoryArea && m.Users.Contains(user)))
            {
                return Unauthorized();
            }
            if(string.IsNullOrEmpty(filePath))
            {
                return BadRequest("FilePath must be defined and differs from null or empty");
            }
            if (!filePath.EndsWith('/'))
            {
                return BadRequest("FilePath must end with /");
            }
            
            string localFilePath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, $"UploadedFiles/{idMemoryArea}"));

            localFilePath = Path.Combine(localFilePath, filePath);

            if (!Directory.Exists(localFilePath))
            {
                return NotFound("Folder not found");
            }

            // Delete the folder
            Directory.Delete(localFilePath, true);

            // Delete all the metadatas of the files inside the folder
            var userEntry = _context.Entry(user);
            userEntry.Collection(m => m.MemoryAreasPartecipated).Load();
            
            var memoryArea = user.MemoryAreasPartecipated.Where(m => m.Id == idMemoryArea).First();

            var memoryAreaEntry = _context.Entry(memoryArea);
            memoryAreaEntry.Collection(m => m.Metadatas).Load();

            memoryArea.Metadatas.Where(m => m.Path.StartsWith(filePath)).ToList().ForEach(m => _context.Metadatas.Remove(m));
            _context.SaveChanges();

            return Ok();
        }


        [HttpPost]
        public IActionResult Post(string memoryAreaName)
        {
            User user = Request.HttpContext.Items["User"] as User;

            if (user == null)
            {
                return Unauthorized();
            }
            
            // create mock memory area
            MemoryArea memoryArea = new MemoryArea();
            memoryArea.Name = memoryAreaName;
            memoryArea.MaxGB = 30;
            memoryArea.Users = new List<User>
            {
                _context.Users.Find(user.Id)
            };
            memoryArea.EncryptionKey = AesEncryptionService.RandomByteArray(32);
            memoryArea.UserOwner = user;
            memoryArea.CreationDate = DateTime.Now;
            
            // load to db
            _context.MemoryAreas.Add(memoryArea);
            _context.SaveChanges();

            return Ok();
        }

        

        
    }
}
