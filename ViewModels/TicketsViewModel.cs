namespace HelpDesk.ViewModels
{
    public class TicketsViewModel
    {
        public int TicketId { get; set; }
        public string Subject { get; set; }
        public string Description { get; set; }
        public DateTime DateTime { get; set; }
        public DateTime? LastChange { get; set; }
        public int? ExpectedHours { get; set; }
        public string UserName { get; set; }
        public string Priority { get; set; }
        public string Status { get; set; }
        public string Category { get; set; }
        public string TrackRecord { get; set; }
        public IEnumerable<MessageViewModel>? Messages { get; set; }
        public string? NewMessage { get; set; }
    }
}
