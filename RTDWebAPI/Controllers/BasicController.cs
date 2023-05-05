using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using RTDWebAPI.Commons.Method.Database;
using RTDWebAPI.Models;
using System.Collections.Concurrent;

namespace RTDWebAPI.Controllers
{
    public class BasicController : Controller
    {
        public static ConcurrentQueue<EventQueue> eventQueue { get; set; }
        public static IConfiguration configuration { get; set; }
        public static DBPool dbPool { get; set; }
    }
}
