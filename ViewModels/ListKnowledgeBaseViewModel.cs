using HelpDesk.Models;

namespace HelpDesk.ViewModels
{
    public class ListKnowledgeBaseViewModel
    {
        public IEnumerable<KnowledgeBase>? knowledgeBases { get; set; }
        public int selectedCategoryId { get; set; }
        public IEnumerable<string> categories { get; set;}
    }
}
