using HelpDesk.Models;
using HelpDesk.Utils;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System;
using System.Security.Cryptography;

public static class TaskInitializer
{
	public static WebApplication Seed(this WebApplication app)
	{
		using (var scope = app.Services.CreateScope())
		{
			using var context = scope.ServiceProvider.GetRequiredService<HelpDeskContext>();
			try
			{
				context.Database.EnsureCreated();

				var users = context.User.FirstOrDefault();
				if(users == null)
				{
					byte[] salt1 = CryptoUtils.GetSalt();
					byte[] salt2 = CryptoUtils.GetSalt();
                    context.User.AddRange(
						new User { UserId = 0, Name = "Luka", Surname = "Modrić", Email = "zavrsni.user@gmail.com", Password = CryptoUtils.HashPassword("user", salt1), PasswordSalt = Convert.ToBase64String(salt1), PhoneNumber = "0912345678"},
						new User { UserId = 0, Name = "Mateo", Surname = "Kovačić", Email = "zavrsni.user3@gmail.com", Password = CryptoUtils.HashPassword("user", salt2), PasswordSalt = Convert.ToBase64String(salt2), PhoneNumber = "0952558069"}
						);
					context.SaveChanges();
				}

				var agents = context.Agent.FirstOrDefault();
				if(agents == null)
				{
					byte[] salt3 = CryptoUtils.GetSalt();
					byte[] salt4 = CryptoUtils.GetSalt();
					context.Agent.AddRange(
						new Agent { AgentId = 0, Name = "Zlatko", Surname = "Dalić", Email = "zavrsni.agent@gmail.com", Password = CryptoUtils.HashPassword("admin", salt3), PasswordSalt = Convert.ToBase64String(salt3)},
                        new Agent { AgentId = 0, Name = "Miroslav", Surname = "Blažević", Email = "zavrsni.agent2@gmail.com", Password = CryptoUtils.HashPassword("admin", salt4), PasswordSalt = Convert.ToBase64String(salt4) }
                        );
					context.SaveChanges();
				}
			}
			catch (Exception)
			{
				throw;
			}

			return app;
		}
	}
}
