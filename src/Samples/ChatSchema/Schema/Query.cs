namespace Chat.Schema
{
    public class Query
    {
        public static Message? LastMessage([FromServices] ChatService chatService)
            => chatService.LastMessage;

        public static IEnumerable<Message> AllMessages([FromServices] ChatService chatService, string? from = null)
            => from == null ? chatService.GetAllMessages() : chatService.GetMessageFromUser(from);

        public static int Count([FromServices] ChatService chatService)
            => chatService.Count;
    }
}
