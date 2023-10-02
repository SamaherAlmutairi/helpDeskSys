﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace HelpDesk.Models
{
    public partial class Ticket
    {
        public Ticket()
        {
            Message = new HashSet<Message>();
        }

        public int TicketId { get; set; }
        public string Description { get; set; }
        public DateTime DateTime { get; set; }
        public string Subject { get; set; }
        public string TrackRecord { get; set; }
        public DateTime? LastChange { get; set; }
        public int? ExpectedHours { get; set; }
        public int UserId { get; set; }
        public int PriorityId { get; set; }
        public int StatusId { get; set; }
        public int CategoryId { get; set; }
        public int? AgentId { get; set; }

        public virtual Agent Agent { get; set; }
        public virtual Category Category { get; set; }
        public virtual Priority Priority { get; set; }
        public virtual Status Status { get; set; }
        public virtual User User { get; set; }
        public virtual ICollection<Message> Message { get; set; }
    }
}