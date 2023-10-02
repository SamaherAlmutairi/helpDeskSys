using HelpDesk.Models;
using HelpDesk.Utils;
using HelpDesk.ViewModels;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.VisualBasic;
using Org.BouncyCastle.Asn1.X509;
using System.Net.Sockets;
using System.Reflection.Metadata;
using System.Web;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace HelpDesk.Controllers
{
    public class AgentController : Controller
    {
        private readonly HelpDeskContext ctx;

        public AgentController(HelpDeskContext ctx)
        {
            this.ctx = ctx;
        }

        public IActionResult Index()
        {
            Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
            Response.Headers.Add("Pragma", "no-cache");
            Response.Headers.Add("Expires", "0");

            if (HttpContext.Session.GetString("Role") == null || HttpContext.Session.GetString("Role") != "Admin")
            {
                return RedirectToAction("Login");
            }

            if (int.TryParse(HttpContext.Session.GetString("Id"), out int agentId))
            {
                var tickets = ctx.Ticket
                                 .AsNoTracking()
                                 .Where(t => t.AgentId == agentId && t.StatusId != 3)
                                 .ToList();

                var numOfHighPriority = tickets.Where(t => t.PriorityId == 3)
                                               .ToList()
                                               .Count();

                var numOfLate = tickets.Where(t => t.LastChange.HasValue && t.ExpectedHours.HasValue && t.LastChange.Value.AddHours((double)t.ExpectedHours.Value) < DateTime.Now)
                                       .ToList()
                                       .Count();

                var model = new AgentIndexViewModel
                {
                    numOfHighPriority = numOfHighPriority,
                    numOfLate = numOfLate
                };

                return View(model);
            }

            return View(null);
        }

        public IActionResult Login()
        {
            HttpContext.Session.Clear();
            HttpContext.Response.Cookies.Append("SessionName", "", new CookieOptions()
            {
                Expires = DateTime.Now.AddDays(-1)
            });
            HttpContext.Response.Cookies.Delete("SessionName");
            return View();
        }

        [HttpPost]
        public IActionResult LoginAgent(String Email, String Password)
        {
            if (Email == null || Password == null)
            {
                ViewData["Error"] = "Wrong email or password";
                return RedirectToAction("Login");
            }

            var agents = ctx.Agent.AsNoTracking();

            Agent? agent = null;
            foreach (var a in agents)
            {
                String hashedPassword = CryptoUtils.HashPassword(Password, Convert.FromBase64String(a.PasswordSalt));
                if (a.Email.Equals(Email) && a.Password.Equals(hashedPassword))
                {
                    agent = a;
                    break;
                }
            }

            if (agent != null)
            {
                HttpContext.Session.SetString("Name", agent.Name);
                HttpContext.Session.SetString("Surname", agent.Surname);
                HttpContext.Session.SetString("Id", agent.AgentId.ToString());
                HttpContext.Session.SetString("Role", "Admin");

                return RedirectToAction("Index");
            }
            else
            {
                ViewData["Error"] = "Wrong email or password";
                return RedirectToAction("Login");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Tickets()
        {
            if (int.TryParse(HttpContext.Session.GetString("Id"), out int agentId))
            {
                var tickets = ctx.Ticket
                                 .AsNoTracking()
                                 .Where(t => t.AgentId == agentId && t.StatusId != 3)
                                 .Select(t => new TicketsViewModel
                                 {
                                     TicketId = t.TicketId,
                                     Subject = t.Subject,
                                     Description = t.Description,
                                     DateTime = t.DateTime,
                                     LastChange = t.LastChange,
                                     ExpectedHours = t.ExpectedHours,
                                     UserName = t.User.Name + " " + t.User.Surname,
                                     Priority = t.Priority.Name,
                                     Status = t.Status.Name,
                                     Category = t.Category.Name,
                                     TrackRecord = t.TrackRecord
                                 })
                                 .ToList();

                if (tickets.Count == 0)
                {
                    return View(null);
                }
                else
                {
                    var model = new ListTicketsViewModel
                    {
                        Tickets = tickets
                    };
                    await PrepareDropDownLists();
                    return View(model);
                }
            }
            else
            {
                return BadRequest();
            }
        }

        [HttpGet]
        public async Task<IActionResult> ReviewTicket(int id)
        {
            var ticket = await ctx.Ticket
                                  .AsNoTracking()
                                  .Where(t => t.TicketId == id)
                                  .Include(t => t.Message)
                                  .FirstOrDefaultAsync();

            if (ticket == null)
            {
                return NotFound();
            }
            else
            {
                var model = new AgentReviewViewModel
                {
                    TicketId = ticket.TicketId,
                    Subject = ticket.Subject,
                    Description = ticket.Description,
                    DateTime = ticket.DateTime,
                    ExpectedHours = ticket.ExpectedHours,
                    PriorityId = ticket.PriorityId,
                    StatusId = ticket.StatusId,
                    CategoryId = ticket.CategoryId,
                    TrackRecord = ticket.TrackRecord,
                    Messages = ticket.Message.Select(m => new MessageViewModel
                    {
                        MessageId = m.MessageId,
                        MessageContent = m.MessageContent,
                        DateTime = m.DateTime,
                        Sender = m.Sender
                    })
                };

                await PrepareDropDownLists();

                return View(model);
            }
        }

        [HttpPost, ActionName("ReviewTicket")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitReview(int id)
        {
            string help = Request.Form["givenText"];

            try
            {
                var ticket = await ctx.Ticket
                                 .Where(t => t.TicketId == id)
                                 .Include(u => u.User)
                                 .FirstOrDefaultAsync();

                if (ticket == null)
                {
                    return NotFound();
                }

                ticket.LastChange = DateTime.Now;

                if (await TryUpdateModelAsync<Ticket>(ticket, "", t => t.Description, t => t.DateTime, t => t.Subject, t => t.TrackRecord, t => t.LastChange, t => t.ExpectedHours, t => t.UserId, t => t.PriorityId, t => t.StatusId, t => t.CategoryId, t => t.AgentId))
                {
                    try
                    {   
                        if(ticket.TrackRecord != null)
                        {
                            string currentDate = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
                            ticket.TrackRecord += $"{currentDate} - {help}\n";
                        }

                        if (ticket.StatusId == 2)
                        {
                            EmailUtils.SendEmail(ticket.User.Email, $"Ticket #{ticket.TicketId}", $"Our agent has started reviewing your ticket #{ticket.TicketId} and its review is currently in progress. Expected time needed to resolve your problem is {ticket.ExpectedHours} hours.");
                        }
                        else if(ticket.StatusId == 3)
                        {
                            EmailUtils.SendEmail(ticket.User.Email, $"Ticket #{ticket.TicketId}", $"Our agent has finished reviewing your ticket #{ticket.TicketId}. Check his reply in app.");
                        }
                        else if(ticket.StatusId == 1002)
                        {
                            EmailUtils.SendEmail(ticket.User.Email, $"Ticket #{ticket.TicketId}", $"Our agent has stoped reviewing your ticket #{ticket.TicketId} and its review is currently on hold.");
                        }

                        await ctx.SaveChangesAsync();

                        return View(nameof(Tickets));
                    }
                    catch
                    {
                        ViewData["Error"] = "Couldn't save ticket review, try again later";

                        await PrepareDropDownLists();
                        return View(ticket);
                    }
                }
                else
                {
                    ViewData["Error"] = "Couldn't save ticket review, try again later";

                    await PrepareDropDownLists();
                    return View(ticket);
                }
            }
            catch
            {
                ViewData["Error"] = "There is no ticket";

                return RedirectToAction("ReviewTicket");
            }
        }

        [HttpGet]
        public IActionResult ClosedTickets()
        {
            if (int.TryParse(HttpContext.Session.GetString("Id"), out int agentId))
            {
                var tickets = ctx.Ticket
                                 .AsNoTracking()
                                 .Where(t => t.AgentId == agentId && t.StatusId == 3)
                                 .Select(t => new TicketsViewModel
                                 {
                                     TicketId = t.TicketId,
                                     Subject = t.Subject,
                                     Description = t.Description,
                                     DateTime = t.DateTime,
                                     LastChange = t.LastChange,
                                     ExpectedHours = t.ExpectedHours,
                                     UserName = t.User.Name + " " + t.User.Surname,
                                     Priority = t.Priority.Name,
                                     Status = t.Status.Name,
                                     Category = t.Category.Name,
                                     TrackRecord = t.TrackRecord
                                 })
                                 .ToList();

                if (tickets.Count == 0)
                {
                    return View(null);
                }
                else
                {
                    var model = new ListTicketsViewModel
                    {
                        Tickets = tickets
                    };
                    return View(model);
                }
            }
            else
            {
                return BadRequest();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(AgentReviewViewModel ticket)
        {
            try
            {
                if (int.TryParse(HttpContext.Session.GetString("Id"), out int agentId))
                {
                    Message message = new Message
                    {
                        MessageId = 0,
                        MessageContent = ticket.NewMessage,
                        DateTime = DateTime.Now,
                        Sender = HttpContext.Session.GetString("Name") + " " + HttpContext.Session.GetString("Surname"),
                        TicketId = ticket.TicketId
                    };

                    ctx.Add(message);
                    await ctx.SaveChangesAsync();

                    var help = await ctx.Ticket
                                        .Where(t => t.TicketId == ticket.TicketId)
                                        .Include(t => t.User)
                                        .FirstOrDefaultAsync();

                    EmailUtils.SendEmail(help.User.Email, $"Ticket #{ticket.TicketId} message", $"You have new message for ticket #{ticket.TicketId} from our agent.");

                    return RedirectToAction("ReviewTicket", new { id = ticket.TicketId });
                }
                else
                {
                    return BadRequest();
                }
            }
            catch
            {
                ViewData["Error"] = "Error with sending message";
                return RedirectToAction("CheckReview", new { id = ticket.TicketId });
            }
        }

        [HttpGet]
        public async Task<IActionResult> NewKnowledgeBase()
        {
            await PrepareDropDownLists();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NewKnowledgeBase(KnowledgeBase knowledgeBase)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    if (int.TryParse(HttpContext.Session.GetString("Id"), out int userId))
                    {
                        knowledgeBase.Date = DateTime.Now;
                        ctx.Add(knowledgeBase);
                        await ctx.SaveChangesAsync();

                        return RedirectToAction("Index");
                    }
                    else
                    {
                        return BadRequest();
                    }
                }
                catch
                {
                    ViewData["Error"] = "Error with creating new FAQ";
                    return View(knowledgeBase);
                }
            }
            else
            {
                await PrepareDropDownLists();
                return View(knowledgeBase);
            }
        }

        [HttpGet]
        public IActionResult KnowledgeBase()
        {
            if (int.TryParse(HttpContext.Session.GetString("Id"), out int userId))
            {
                var knowledge = ctx.KnowledgeBase
                                   .AsNoTracking()
                                   .Include(kb => kb.Category)
                                   .ToList();

                if (knowledge.Count == 0)
                {
                    return RedirectToAction("Index");
                }
                else
                {
                    var model = new ListKnowledgeBaseViewModel
                    {
                        knowledgeBases = knowledge,
                        categories = knowledge.Select(kb => kb.Category.Name).Distinct().ToList()
                    };
                    return View(model);
                }
            }
            else
            {
                return BadRequest();
            }
        }

        [HttpGet]
        public IActionResult Search(string query, string source)
        {
            if (int.TryParse(HttpContext.Session.GetString("Id"), out int agentId))
            {
                List<TicketsViewModel> tickets;
                if (source.Equals("Tickets"))
                {
                    tickets = ctx.Ticket
                                 .AsNoTracking()
                                 .Where(t => t.Subject.Contains(query) && t.AgentId == agentId && t.StatusId != 3)
                                 .Select(t => new TicketsViewModel
                                 {
                                     TicketId = t.TicketId,
                                     Subject = t.Subject,
                                     Description = t.Description,
                                     DateTime = t.DateTime,
                                     LastChange = t.LastChange,
                                     ExpectedHours = t.ExpectedHours,
                                     UserName = t.User.Name + " " + t.User.Surname,
                                     Priority = t.Priority.Name,
                                     Status = t.Status.Name,
                                     Category = t.Category.Name,
                                     TrackRecord = t.TrackRecord
                                 })
                                 .ToList();
                }
                else
                {
                    tickets = ctx.Ticket
                                 .AsNoTracking()
                                 .Where(t => t.Subject.Contains(query) && t.AgentId == agentId && t.StatusId == 3)
                                 .Select(t => new TicketsViewModel
                                 {
                                     TicketId = t.TicketId,
                                     Subject = t.Subject,
                                     Description = t.Description,
                                     DateTime = t.DateTime,
                                     LastChange = t.LastChange,
                                     ExpectedHours = t.ExpectedHours,
                                     UserName = t.User.Name + " " + t.User.Surname,
                                     Priority = t.Priority.Name,
                                     Status = t.Status.Name,
                                     Category = t.Category.Name,
                                     TrackRecord = t.TrackRecord
                                 })
                                 .ToList();
                }

                if (tickets.Count == 0)
                {
                    return View(source, null);
                }
                else
                {
                    var model = new ListTicketsViewModel
                    {
                        Tickets = tickets
                    };
                    return View(source, model);
                }
            }
            else
            {
                return BadRequest();
            }
        }

        private async Task PrepareDropDownLists()
        {
            var entity1 = await ctx.Priority
                                   .Select(p => new { p.PriorityId, p.Name })
                                   .FirstOrDefaultAsync();
            var priorities = await ctx.Priority
                                      .OrderBy(p => p.PriorityId)
                                      .Select(p => new { p.PriorityId, p.Name })
                                      .ToListAsync();
            ViewBag.Priorities = new SelectList(priorities, nameof(entity1.PriorityId), nameof(entity1.Name));

            var entity2 = await ctx.Status
                                   .Select(s => new { s.StatusId, s.Name })
                                   .FirstOrDefaultAsync();
            var statuses = await ctx.Status
                                    .OrderBy(s => s.StatusId)
                                    .Select(s => new { s.StatusId, s.Name })
                                    .ToListAsync();
            ViewBag.Statuses = new SelectList(statuses, nameof(entity2.StatusId), nameof(entity2.Name));

            var entity3 = await ctx.Category
                                   .Select(c => new { c.CategoryId, c.Name })
                                   .FirstOrDefaultAsync();
            var categories = await ctx.Category
                                      .OrderBy(c => c.CategoryId)
                                      .Select(c => new { c.CategoryId, c.Name })
                                      .ToListAsync();
            ViewBag.Categories = new SelectList(categories, nameof(entity3.CategoryId), nameof(entity3.Name));

            var entity4 = await ctx.Agent
                                   .Select(a => new { a.AgentId, FullName = a.Name + " " + a.Surname})
                                   .FirstOrDefaultAsync();

            var agents = await ctx.Agent
                                  .OrderBy(a => a.AgentId)
                                  .Select(a => new { a.AgentId, FullName = a.Name + " " + a.Surname })
                                  .ToListAsync();

            ViewBag.Agents = new SelectList(agents, nameof(entity4.AgentId), nameof(entity4.FullName));
        }
    }
}
