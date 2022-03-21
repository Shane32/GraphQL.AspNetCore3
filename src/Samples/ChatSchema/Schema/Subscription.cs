namespace Chat.Schema
{
    public class Subscription
    {
        public static IObservable<Message> NewMessages([FromServices] ChatService chatService, string? from = null)
            => from == null ? chatService.SubscribeAll() : chatService.SubscribeFromUser(from);

        public static IObservable<Event> Events([FromServices] ChatService chatService)
            => chatService.SubscribeEvents();
    }
}
