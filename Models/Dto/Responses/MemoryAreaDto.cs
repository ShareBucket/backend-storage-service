namespace StorageMicroService.Models.Dto.Responses
{
    public class MemoryAreaDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int MaxGB { get; set; }
        public DateTime CreationDate { get; set; }
    }

}
