namespace Chat.Schema;

public class Event
{
    public EventType Type { get; set; }
    public Message? Message { get; set; }
}
