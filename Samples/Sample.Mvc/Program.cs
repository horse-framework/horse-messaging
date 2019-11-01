using Twino.Mvc;
using Twino.Mvc.Auth;
using Twino.Mvc.Auth.Jwt;
using Twino.Mvc.Middlewares;
using Sample.Mvc.Models;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using Twino.Mvc.Results;
using Twino.Server;
using Twino.Server.WebSockets;

namespace Sample.Mvc
{
    class Program
    {
        static void Main(string[] args)
        {
            using TwinoMvc mvc = new TwinoMvc();

            mvc.IsDevelopment = true;
            mvc.Init(twino =>
            {
                twino.Services.Add<IDemoService, DemoService>();

                twino.AddJwt(options =>
                {
                    options.Key = "Very_very_secret_key";
                    options.Issuer = "localhost";
                    options.Audience = "localhost";
                    options.Lifetime = TimeSpan.FromHours(1);
                    options.ValidateAudience = false;
                    options.ValidateIssuer = false;
                    options.ValidateLifetime = true;
                });

                twino.Policies.Add(Policy.RequireRole("Admin", "Admin"));
                twino.Policies.Add(Policy.RequireClaims("IT", "Database", "Cloud", "Source"));
                twino.Policies.Add(Policy.Custom("Custom", (d, c) => true));

                twino.UseFiles("/download", "/home/mehmet/files");

                twino.StatusCodeResults.Add(HttpStatusCode.Unauthorized, new JsonResult(new {Message = "Access denied"}));
            });

            CorsMiddleware cors = new CorsMiddleware();
            cors.AllowAll();

            mvc.Run(app => { app.UseMiddleware(cors); });
        }
    }
}