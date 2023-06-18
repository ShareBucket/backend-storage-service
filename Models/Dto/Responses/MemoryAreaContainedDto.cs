namespace StorageMicroService.Models.Dto.Responses
{
    public class MemoryAreaContainedDto
    {
        public List<FileDto> Files { get; set; }
        public List<FolderDto> Folders { get; set; }
        public string Path { get; set; }
    }
}
