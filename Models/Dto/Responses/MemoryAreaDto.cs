namespace StorageMicroService.Models.Dto.Responses
{
    public class MemoryAreaDto
    {
        public int IdAreaMemoria { get; set; }
        public string Name { get; set; }
        public int MaxGB { get; set; }
        public DateTime CreationDate { get; set; }
        public string UserOwner{ get; set; }
    }

}
