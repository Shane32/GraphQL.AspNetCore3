namespace Chat.Schema
{
    public class Mutation
    {
        public static Message AddMessage([FromServices] ChatService chatService, MessageInput message)
            => chatService.PostMessage(message);

        public static Message? DeleteMessage([FromServices] ChatService chatService, [Id] int id)
            => chatService.DeleteMessage(id);

        public static int ClearMessages([FromServices] ChatService chatService)
            => chatService.ClearMessages();
    }
}
