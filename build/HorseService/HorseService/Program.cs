using System.Text.RegularExpressions;
using Horse.Jockey;
using Horse.Jockey.Models.User;
using Horse.Messaging.Data;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Server;
using HorseService;

// Load Options

string optionsFilename = "/etc/horse/options.json";
AppOptions appOptions = null;

if (!Directory.Exists("/etc/horse"))
    Directory.CreateDirectory("/etc/horse");

if (File.Exists(optionsFilename))
{
    string json = File.ReadAllText(optionsFilename);
    appOptions = Newtonsoft.Json.JsonConvert.DeserializeObject<AppOptions>(json);
}
else
{
    appOptions = new AppOptions();
}

string port = Environment.GetEnvironmentVariable("HORSE_PORT");
string jockeyPort = Environment.GetEnvironmentVariable("HORSE_JOCKEY_PORT");
string username = Environment.GetEnvironmentVariable("HORSE_JOCKEY_USERNAME");
string password = Environment.GetEnvironmentVariable("HORSE_JOCKEY_PASSWORD");
string datapath = Environment.GetEnvironmentVariable("HORSE_DATA_PATH");

if (!string.IsNullOrEmpty(port))
    appOptions.Port = Convert.ToInt32(port);

if (!string.IsNullOrEmpty(jockeyPort))
    appOptions.JockeyPort = Convert.ToInt32(jockeyPort);

if (!string.IsNullOrEmpty(username))
    appOptions.JockeyUsername = username;

if (!string.IsNullOrEmpty(password))
    appOptions.JockeyPassword = password;

if (!string.IsNullOrEmpty(datapath))
    appOptions.DataPath = datapath;

// Initialize Server

HorseServer server = new HorseServer();
HorseRider rider = HorseRiderBuilder.Create()
    .ConfigureOptions(o => { o.DataPath = appOptions.DataPath; })
    .ConfigureChannels(c =>
    {
        c.Options.AutoDestroy = true;
        c.Options.AutoChannelCreation = true;
    })
    .ConfigureCache(c =>
    {
        c.Options.DefaultDuration = TimeSpan.FromMinutes(15);
        c.Options.MinimumDuration = TimeSpan.FromHours(6);
    })
    .ConfigureQueues(c =>
    {
        c.Options.AutoQueueCreation = true;
        c.UsePersistentQueues(d => { d.UseAutoFlush(TimeSpan.FromMilliseconds(50)); },
            q =>
            {
                q.Options.AutoDestroy = QueueDestroy.Disabled;
                q.Options.CommitWhen = CommitWhen.AfterSaved;
                q.Options.PutBack = PutBackDecision.Regular;
                q.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
                q.Options.PutBackDelay = 5000;
            });
    })
    .Build();


// Add Jockey

rider.AddJockey(o =>
{
    o.CustomSecret = $"{Guid.NewGuid()}-{Guid.NewGuid()}-{Guid.NewGuid()}";

    o.Port = appOptions.JockeyPort;
    o.AuthAsync = login =>
    {
        if (login.Username == appOptions.JockeyUsername && login.Password == appOptions.JockeyPassword)
            return Task.FromResult(new UserInfo {Id = "*", Name = "Admin"});

        return Task.FromResult<UserInfo>(null);
    };
});


// Run

server.UseRider(rider);
server.Run(appOptions.Port);