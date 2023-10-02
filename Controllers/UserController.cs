using HelpDesk.Models;
using HelpDesk.Utils;
using HelpDesk.ViewModels;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Controllers
{
    public class UserController : Controller
    {
        private readonly HelpDeskContext ctx;

        public UserController(HelpDeskContext ctx)
        {
            this.ctx = ctx;
        }

        public IActionResult Index()
        {
            Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
            Response.Headers.Add("Pragma", "no-cache");
            Response.Headers.Add("Expires", "0");

            if (HttpContext.Session.GetString("Role") == null || HttpContext.Session.GetString("Role") != "User")
            {
                return RedirectToAction("Login");
            }

            if (int.TryParse(HttpContext.Session.GetString("Id"), out int userId))
            {
                var tickets = ctx.Ticket
                                 .AsNoTracking()
                                 .Where(t => t.UserId == userId && t.StatusId != 3)
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
                                     Category = t.Category.Name
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
        public IActionResult LoginUser(String email, String password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ViewData["Error"] = "Wrong email or password";
                return RedirectToAction("Login");
            }

            var users = ctx.User.AsNoTracking();

            User? user = null;
            foreach (var u in users)
            {
                String hashedPassword = CryptoUtils.HashPassword(password, Convert.FromBase64String(u.PasswordSalt));
                if (u.Email.Equals(email) && u.Password.Equals(hashedPassword))
                {
                    user = u;
                    break;
                }
            }

            if (user != null)
            {
                HttpContext.Session.SetString("Name", user.Name);
                HttpContext.Session.SetString("Surname", user.Surname);
                HttpContext.Session.SetString("Id", user.UserId.ToString());
                HttpContext.Session.SetString("Role", "User");
                HttpContext.Session.SetString("Email", user.Email);

                return RedirectToAction("Index");
            }
            else
            {
                ViewData["Error"] = "Wrong email or password";
                return RedirectToAction("Login");
            }
        }

        [HttpGet]
        public async Task<IActionResult> NewTicket()
        {
            await PrepareDropDownLists();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NewTicket(Ticket ticket)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    if(int.TryParse(HttpContext.Session.GetString("Id"), out int userId))
                    {
                        ticket.DateTime = DateTime.Now;
                        ticket.UserId = userId;
                        ticket.StatusId = 1;
                        ticket.AgentId = ChooseAvailableAgent();
                        ctx.Add(ticket);
                        await ctx.SaveChangesAsync();

                        EmailUtils.SendEmail(HttpContext.Session.GetString("Email"), "New Ticket", "You have submited a new ticket for a review.");

                        return RedirectToAction("Index");
                    }
                    else
                    {
                        return BadRequest();
                    }
                }
                catch
                {
                    ViewData["Error"] = "Error with creating ticket";
                    return View(ticket);
                }
            }
            else
            {
                await PrepareDropDownLists();
                return View(ticket);
            }
        }

        [HttpGet]
        public IActionResult ClosedTickets()
        {
            if (int.TryParse(HttpContext.Session.GetString("Id"), out int userId))
            {
                var tickets = ctx.Ticket
                                 .AsNoTracking()
                                 .Where(t => t.UserId == userId && t.StatusId == 3)
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
                                     Category = t.Category.Name
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

        [HttpGet]
        public async Task<IActionResult> CheckReview(int id)
        {
            var ticket = await ctx.Ticket
                                  .AsNoTracking()
                                  .Where(t => t.TicketId == id)
                                  .Include(t => t.Status)
                                  .Include(t => t.Message)
                                  .FirstOrDefaultAsync();

            if (ticket == null)
            {
                return NotFound();
            }
            else
            {
                var model = new TicketsViewModel
                {
                    TicketId = ticket.TicketId,
                    Subject = ticket.Subject,
                    Status = ticket.Status.Name,
                    Description = ticket.Description,
                    LastChange = ticket.LastChange,
                    ExpectedHours = ticket.ExpectedHours,
                    Messages = ticket.Message.Select(m => new MessageViewModel
                    {
                        MessageId = m.MessageId,
                        MessageContent = m.MessageContent,
                        DateTime = m.DateTime,
                        Sender = m.Sender
                    })
                };

                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(TicketsViewModel ticket)
        {
            try
            {
                if (int.TryParse(HttpContext.Session.GetString("Id"), out int userId))
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
                                        .Include(t => t.Agent)
                                        .FirstOrDefaultAsync();

                    EmailUtils.SendEmail(help.Agent.Email, $"Ticket #{ticket.TicketId} message", $"You have new message for ticket #{ticket.TicketId}");

                    return RedirectToAction("CheckReview", new {id = ticket.TicketId});
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
        public IActionResult Search(string query, string source)
        {
            if (int.TryParse(HttpContext.Session.GetString("Id"), out int userId))
            {
                List<TicketsViewModel> tickets;
                if (source.Equals("Index"))
                {
                    tickets = ctx.Ticket
                                 .AsNoTracking()
                                 .Where(t => t.Subject.Contains(query) && t.UserId == userId && t.StatusId != 3)
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
                                     Category = t.Category.Name
                                 })
                                 .ToList();
                }
                else
                {
                    tickets = ctx.Ticket
                                 .AsNoTracking()
                                 .Where(t => t.Subject.Contains(query) && t.UserId == userId && t.StatusId == 3)
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
                                     Category = t.Category.Name
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

            var entity2 = await ctx.Category
                                   .Select(c => new { c.CategoryId, c.Name })
                                   .FirstOrDefaultAsync();
            var categories = await ctx.Category
                                      .OrderBy(c => c.CategoryId)
                                      .Select(c => new { c.CategoryId, c.Name })
                                      .ToListAsync();
            ViewBag.Categories = new SelectList(categories, nameof(entity2.CategoryId), nameof(entity2.Name));
        }

        private int ChooseAvailableAgent()
        {
            var agent = ctx.Agent
                           .OrderBy(a => a.Ticket.Count)
                           .FirstOrDefault();

            if(agent != null)
            {
                return agent.AgentId;
            }
            else
            {
                return 0;
            }
        }
    }
}
