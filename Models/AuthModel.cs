using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LaLlamaDelBosque.Models
{

	public class AuthModel
	{

		public int Id { get; set; }

		public string Password { get; set; } = String.Empty;

		public string Email { get; set; } = String.Empty;

		public string Salt { get; set; } = String.Empty;

		public string Name { get; set; } = String.Empty;

		public string LastName { get; set; } = String.Empty;
	}

}