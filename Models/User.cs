﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace HelpDesk.Models
{
    public partial class User
    {
        public User()
        {
            Ticket = new HashSet<Ticket>();
        }

        public int UserId { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string PasswordSalt { get; set; }
        public string PhoneNumber { get; set; }

        public virtual ICollection<Ticket> Ticket { get; set; }
    }
}