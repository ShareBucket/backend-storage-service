namespace StorageMicroService.Models.Dto.Responses
{
    public class FileDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Extension { get; set; }
        public DateTime DataCreation { get; set; }
    }
}
