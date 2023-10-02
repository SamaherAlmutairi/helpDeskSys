namespace HelpDesk.ViewModels
{
    public class AgentReviewViewModel
    {
        public int TicketId { get; set; }
        public string Subject { get; set; }
        public string Description { get; set; }
        public DateTime DateTime { get; set; }
        public int? ExpectedHours { get; set; }
        public int PriorityId { get; set; }
        public int StatusId { get; set; }
        public int CategoryId { get; set; }
        public string TrackRecord { get; set; }
        public int AgentId { get; set; }
        public IEnumerable<MessageViewModel> Messages { get; set; }
        public string? NewMessage { get; set; }
    }
}
