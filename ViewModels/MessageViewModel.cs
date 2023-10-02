namespace HelpDesk.ViewModels
{
    public class MessageViewModel
    {
        public int MessageId { get; set; }
        public string MessageContent { get; set; }
        public DateTime DateTime { get; set; }
        public string Sender { get; set; }
    }
}
