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
                mvc.Services.Add<IDemoService, DemoService>();

                mvc.AddJwt(options =>
                {
                    options.Key = "Very_very_secret_key";
                    options.Issuer = "localhost";
                    options.Audience = "localhost";
                    options.Lifetime = TimeSpan.FromHours(1);
                    options.ValidateAudience = false;
                    options.ValidateIssuer = false;
                    options.ValidateLifetime = true;
                });

                mvc.Policies.Add(Policy.RequireRole("Admin", "Admin"));
                mvc.Policies.Add(Policy.RequireClaims("IT", "Database", "Cloud", "Source"));
                mvc.Policies.Add(Policy.Custom("Custom", (d, c) => true));
            });

            CorsMiddleware cors = new CorsMiddleware();
            cors.AllowAll();

            mvc.Run(app => { app.UseMiddleware(cors); });
        }
    }
}